#!/usr/bin/env python3
"""Read-only 787 pane/profile/FBX regression. No Unity, generation or rendering.

--capture-baseline creates the immutable fixture once; --report saves successful evidence.
Outer glass is .28723404255 × .500 m; its .94 inner seal supports the .270 × .470 m clear profile.
Dimensions are projected shipped Y/Z bounds, not certified Boeing dimensions.
"""
import argparse
import base64
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import zlib

ROOT = Path(__file__).resolve().parent.parent
ART = Path('game/Airside/Assets/Airside/Art/Models/Aircraft')
MIRROR = Path('game/Airside/Assets/StreamingAssets/Airside/Art/Models/Aircraft')
FIXTURE = ROOT / 'docs/testing/787-window-realism-2026-10-06/pre-upgrade-geometry.json'
SOURCE_FIXTURE = FIXTURE.with_name('uncut-source-hulls.json')
SOURCE_FILES = ['generate-air-adelaide-fleet.py', 'generate-air-010-787-10.py',
                'generate-air-009-a350-900.py', 'generate-air-001-v05.py', 'aircraft_skin.py', 'fit-787-cabin-windows.py']
ALLOWED_GROUPS = {'fuselage', 'fuselage_port', 'glazing_cabin_left_gasket', 'glazing_cabin_right_gasket',
                  'glazing_cabin_left_interior', 'glazing_cabin_right_interior'}
MODELS = {'B789': 'mdl_787_9_v01', 'B78X': 'mdl_787_10_v01'}


def check(value, message):
    if not value:
        raise AssertionError(message)


class Kit:
    def __init__(self, stem, baseline=None):
        def read(suffix):
            path = ART / (stem + suffix)
            if baseline:
                return subprocess.check_output(['git', 'show', f'{baseline}:{path}'], cwd=ROOT)
            return (ROOT / path).read_bytes()
        self.gltf = json.loads(read('.gltf'))
        self.blob = read('.bin')
        self.nodes = {n['name']: n for n in self.gltf['nodes'] if 'mesh' in n}
        self.fbx = read('.fbx').decode()

    def values(self, index):
        a = self.gltf['accessors'][index]
        check('sparse' not in a and not a.get('normalized'), 'unsupported accessor encoding')
        view = self.gltf['bufferViews'][a['bufferView']]
        count = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        code = {5121: 'B', 5123: 'H', 5125: 'I', 5126: 'f'}[a['componentType']]
        fmt = '<' + code * count
        start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = view.get('byteStride', struct.calcsize(fmt))
        return [struct.unpack_from(fmt, self.blob, start + stride * i) for i in range(a['count'])]

    def primitive(self, name):
        node = self.nodes[name]
        check(not any(k in node for k in ('matrix', 'translation', 'rotation', 'scale')), 'nonidentity node ' + name)
        primitives = self.gltf['meshes'][node['mesh']]['primitives']
        check(len(primitives) == 1, 'expected one primitive ' + name)
        return primitives[0]

    def positions(self, name):
        return self.values(self.primitive(name)['attributes']['POSITION'])

    def triangles(self, name):
        flat = [v[0] for v in self.values(self.primitive(name)['indices'])]
        check(len(flat) % 3 == 0, 'nontriangle topology ' + name)
        return [flat[i:i + 3] for i in range(0, len(flat), 3)]

    def signature(self, name):
        p = self.primitive(name)
        payload = {'node': {k: v for k, v in self.nodes[name].items() if k != 'mesh'},
                   'mode': p.get('mode', 4), 'material': self.gltf.get('materials', [])[p['material']] if 'material' in p else None,
                   'attributes': {k: self.values(v) for k, v in p['attributes'].items()},
                   'indices': self.values(p['indices'])}
        return hashlib.sha256(json.dumps(payload, sort_keys=True).encode()).hexdigest()


def components(kit, name):
    vertices = kit.positions(name)
    parents = list(range(len(vertices)))
    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i
    for triangle in kit.triangles(name):
        a, b, c = triangle
        check(max(triangle) < len(vertices), 'index outside vertex buffer')
        parents[find(b)] = find(a)
        parents[find(c)] = find(a)
    groups = {}
    for i, point in enumerate(vertices):
        check(all(math.isfinite(v) for v in point), 'nonfinite pane coordinate')
        groups.setdefault(find(i), []).append(point)
    out = []
    for points in groups.values():
        lo = [min(p[d] for p in points) for d in range(3)]
        hi = [max(p[d] for p in points) for d in range(3)]
        out.append({'node': name, 'centre': [(a + b) / 2 for a, b in zip(lo, hi)],
                    'min': lo, 'max': hi, 'size': [b - a for a, b in zip(lo, hi)], 'points': points})
    return out


class Hull:
    def __init__(self, kit, source=None, names=('fuselage', 'fuselage_port')):
        if source is not None:
            self.points, self.triangles = source
        else:
            self.points, self.triangles = [], []
            for name in names:
                if name not in kit.nodes:
                    continue
                offset = len(self.points)
                self.points.extend(kit.positions(name))
                self.triangles.extend([[i + offset for i in t] for t in kit.triangles(name)])
        self.cells = {}
        for tri in self.triangles:
            p = [self.points[i] for i in tri]
            for y in range(math.floor(min(v[1] for v in p) / .25) - 1, math.floor(max(v[1] for v in p) / .25) + 2):
                for z in range(math.floor(min(v[2] for v in p) / .5) - 1, math.floor(max(v[2] for v in p) / .5) + 2):
                    self.cells.setdefault((y, z), []).append(tri)

    def offset(self, point):
        x, y, z = point
        values = []
        for tri in self.cells.get((math.floor(y / .25), math.floor(z / .5)), []):
            a, b, c = [self.points[i] for i in tri]
            det = (b[1] - a[1]) * (c[2] - a[2]) - (c[1] - a[1]) * (b[2] - a[2])
            if abs(det) < 1e-12:
                continue
            u = ((y - a[1]) * (c[2] - a[2]) - (c[1] - a[1]) * (z - a[2])) / det
            v = ((b[1] - a[1]) * (z - a[2]) - (y - a[1]) * (b[2] - a[2])) / det
            if u >= -1e-5 and v >= -1e-5 and u + v <= 1 + 1e-5:
                hx = a[0] + u * (b[0] - a[0]) + v * (c[0] - a[0])
                if hx * x > 0:
                    values.append(abs(hx))
        return abs(x) - max(values) if values else None


def fbx_check(kit, name):
    geometry_id = re.search(r'Geometry: (\d+), \"Geometry::' + re.escape(name) + r'\", \"Mesh\"', kit.fbx)
    model_id = re.search(r'Model: (\d+), \"Model::' + re.escape(name) + r'\", \"Mesh\"', kit.fbx)
    check(geometry_id and model_id, 'FBX mesh/model pair missing ' + name)
    check(re.search(r'C: \"OO\",\s*' + geometry_id.group(1) + r',\s*' + model_id.group(1) + r'\b', kit.fbx), 'FBX geometry/model connection missing ' + name)
    check(re.search(r'C: \"OO\",\s*' + model_id.group(1) + r',\s*0\b', kit.fbx), 'FBX model root connection missing ' + name)
    block = re.search(r'Geometry: \d+, "Geometry::' + re.escape(name) + r'", "Mesh" \{(.*?)(?=    Model:)', kit.fbx, re.S)
    check(block is not None, 'missing FBX geometry ' + name)
    arrays = {}
    for label in ('Vertices', 'Normals', 'PolygonVertexIndex'):
        match = re.search(label + r': \*(\d+) \{\s*a: ([^\n]+)', block.group(1))
        check(match, 'missing FBX array ' + name + ' ' + label)
        values = [float(v.strip()) for v in match.group(2).split(',')]
        check(len(values) == int(match.group(1)), 'FBX array count mismatch')
        check(all(math.isfinite(v) for v in values), 'nonfinite FBX array')
        arrays[label] = values
    positions = [v for p in kit.positions(name) for v in p]
    check(len(positions) == len(arrays['Vertices']), 'FBX/glTF vertex count mismatch')
    check(all(abs(a - b) < 1.1e-6 for a, b in zip(positions, arrays['Vertices'])), 'FBX/glTF positions differ ' + name)
    indices = [i for t in kit.triangles(name) for i in t]
    fbx_indices = [int(v) if v >= 0 else -int(v) - 1 for v in arrays['PolygonVertexIndex']]
    check(fbx_indices == indices, 'FBX/glTF topology differs ' + name)
    check(all((v < 0) == (i % 3 == 2) for i, v in enumerate(arrays['PolygonVertexIndex'])), 'FBX polygon closure mismatch')
    normals = arrays['Normals']
    check(len(normals) == len(positions), 'FBX normal count mismatch')
    check(all(abs(math.sqrt(sum(v * v for v in normals[i:i + 3])) - 1) < 3e-6 for i in range(0, len(normals), 3)), 'nonunit FBX normal')


def convex_outline(points):
    points = sorted(set((p[2], p[1]) for p in points))
    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    lower, upper = [], []
    for point in points:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], point) <= 0:
            lower.pop()
        lower.append(point)
    for point in reversed(points):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], point) <= 0:
            upper.pop()
        upper.append(point)
    return lower[:-1] + upper[:-1]


def profile_for(code):
    source = (ROOT / 'game/Airside/Assets/Airside/Presentation/PassengerCabinProfile.cs').read_text()
    match = re.search(r'new\("' + code + r'",\s*([^\n]+)', source)
    check(match, 'missing cabin profile ' + code)
    prefix, suffix = match.group(1).split('}', 1)
    numbers = [float(v) for v in re.findall(r'(-?\d+(?:\.\d+)?|-?\.\d+)f', prefix)]
    tail = [float(v) for v in re.findall(r'(-?\d+(?:\.\d+)?|-?\.\d+)f', suffix)]
    return dict(zip(('half_width', 'y', 'z', 'seat_pitch', 'window_pitch', 'width', 'height'), numbers[:4] + tail[:3]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-baseline', help='One-time read-only git SHA used to write the immutable fixture')
    parser.add_argument('--capture-source-hulls', action='store_true', help='Opt-in source snapshot; requires generator NumPy dependency')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    if args.capture_source_hulls:
        output = {'source_sha256': {name: hashlib.sha256((ROOT / 'scripts' / name).read_bytes()).hexdigest() for name in SOURCE_FILES}, 'aircraft': {}}
        for code in MODELS:
            module_path = ROOT / 'scripts' / ('generate-air-adelaide-fleet.py' if code == 'B789' else 'generate-air-010-787-10.py')
            spec = importlib.util.spec_from_file_location('source_' + code, module_path)
            generator = importlib.util.module_from_spec(spec); spec.loader.exec_module(generator)
            source = (generator.boeing_787_9_meshes() if code == 'B789' else generator.boeing_787_10_meshes())['fuselage']
            data = [source[0].tolist(), source[1].reshape(-1, 3).tolist()]
            output['aircraft'][code] = base64.b64encode(zlib.compress(json.dumps(data, separators=(',', ':')).encode(), 9)).decode()
        SOURCE_FIXTURE.write_text(json.dumps(output, separators=(',', ':')) + '\n')
        print('Captured exact uncut generator hulls:', SOURCE_FIXTURE)
        return
    if args.capture_baseline:
        fixture = {'baseline': args.capture_baseline, 'aircraft': {}}
        for code, stem in MODELS.items():
            kit = Kit(stem, args.capture_baseline)
            nodes, panes = {}, {}
            for name in kit.nodes:
                if not name.startswith('cabin_window_'):
                    nodes[name] = kit.signature(name)
                    continue
                parts = sorted(components(kit, name), key=lambda p: p['centre'][2])
                for pane in parts:
                    pane['shape'] = [[round((v[d] - pane['centre'][d]) / pane['size'][d], 6) for d in (1, 2)] for v in pane.pop('points')]
                panes[name] = {'components': parts, 'triangles': kit.triangles(name)}
            fixture['aircraft'][code] = {'nodes': nodes, 'panes_zlib_base64': base64.b64encode(zlib.compress(json.dumps(panes, separators=(',', ':')).encode(), 9)).decode()}
        FIXTURE.parent.mkdir(parents=True, exist_ok=True)
        FIXTURE.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
        print('Captured immutable pre-upgrade fixture:', FIXTURE)
        return
    fixture = json.loads(FIXTURE.read_text())
    report = {'baseline': fixture['baseline'], 'scope': 'static mesh geometry; no native appearance/compilation/performance evidence', 'aircraft': {}}
    for code, stem in MODELS.items():
        kit = Kit(stem)
        previous = fixture['aircraft'][code]
        previous['panes'] = json.loads(zlib.decompress(base64.b64decode(previous['panes_zlib_base64'])))
        original_names = set(previous['nodes']) | set(previous['panes'])
        check(not original_names - set(kit.nodes), code + ' existing node removed')
        check(set(kit.nodes) - original_names <= {'fuselage_port'}, code + ' unexpected new node')
        panes, old = [], []
        source_data = json.loads(SOURCE_FIXTURE.read_text())
        for name, digest in source_data['source_sha256'].items():
            check(hashlib.sha256((ROOT / 'scripts' / name).read_bytes()).hexdigest() == digest, 'uncut source hull snapshot is stale: ' + name)
        source = json.loads(zlib.decompress(base64.b64decode(source_data['aircraft'][code])))
        hull = Hull(kit, source)
        cut_hull = Hull(kit, names=('fuselage', 'fuselage_port', 'glazing_cabin_left_gasket', 'glazing_cabin_right_gasket'))
        for name in kit.nodes:
            primitive = kit.primitive(name)
            index_accessor = kit.gltf['accessors'][primitive['indices']]
            positions = kit.positions(name)
            check(index_accessor['componentType'] == 5123 and len(positions) < 65536, code + ' runtime UInt16 mesh limit exceeded: ' + name)
            indices = [i for t in kit.triangles(name) for i in t]
            check(indices and max(indices) < len(positions), code + ' invalid indices: ' + name)
            if not name.startswith('cabin_window_'):
                if name in ALLOWED_GROUPS:
                    fbx_check(kit, name)
                else:
                    check(kit.signature(name) == previous['nodes'][name], code + ' unrelated geometry/material changed: ' + name)
                continue
            check(kit.triangles(name) == previous['panes'][name]['triangles'], code + ' pane topology changed')
            current, baseline = components(kit, name), previous['panes'][name]['components']
            check(len(current) == len(baseline) and len(current) in (1, 2), code + ' connected pane count changed: ' + name)
            current.sort(key=lambda p: p['centre'][2]); baseline.sort(key=lambda p: p['centre'][2])
            for pane, before in zip(current, baseline):
                check(abs(pane['centre'][2] - before['centre'][2]) < 2e-5, code + ' pane station moved')
                check(abs(pane['centre'][1] - before['centre'][1]) < 2e-5, code + ' projected Y centre moved')
                check(abs(pane['size'][2] - .28723404255) < 2e-5, code + ' projected longitudinal outer width differs from .28723404255')
                check(abs(pane['size'][1] - .500) < 2e-5, code + ' projected outer Y height differs from .500')
                check(len(pane['points']) == len(before['shape']), code + ' pane vertex count changed')
                for point, original in zip(pane['points'], before['shape']):
                    for axis in (1, 2):
                        normalized = (point[axis] - pane['centre'][axis]) / pane['size'][axis]
                        old_normalized = original[axis - 1]
                        check(abs(normalized - old_normalized) < 2e-5, code + ' rounded pane silhouette distorted')
                deviations = [hull.offset(p) for p in pane['points']]
                check(all(v is not None for v in deviations), code + ' pane misses uncut source hull')
                check(min(deviations) >= .002 and max(deviations) <= .025, code + ' buried/floating pane shell')
                centre_ray = cut_hull.offset(pane['centre'])
                check(centre_ray is None or centre_ray > .10, code + ' shipped hull blocks pane centre')
                pane['cut_hull_centre_clear'] = True
                pane['hull_offset_range'] = [min(deviations), max(deviations)]
                pane.pop('points')
            panes.extend(current); old.extend(baseline)
            fbx_check(kit, name)
        for side in (-1, 1):
            belt = sorted((p for p in panes if p['centre'][0] * side > 0), key=lambda p: p['centre'][2])
            prior = sorted((p for p in old if p['centre'][0] * side > 0), key=lambda p: p['centre'][2])
            check(len(belt) == len(prior), code + ' pane count changed')
            for i in range(1, len(belt)):
                check(belt[i]['min'][2] > belt[i - 1]['max'][2], code + ' overlapping adjacent panes')
                check(abs((belt[i]['centre'][2] - belt[i - 1]['centre'][2]) - (prior[i]['centre'][2] - prior[i - 1]['centre'][2])) < 3e-5, code + ' pitch changed')
        profile = profile_for(code)
        hero = min((p for p in panes if p['centre'][0] < 0), key=lambda p: abs(p['centre'][2] - profile['z']))
        clear_probes = [(0, 0), (-.43, 0), (.43, 0), (0, -.43), (0, .43), (-.28, -.28), (-.28, .28), (.28, -.28), (.28, .28)]
        for across, up in clear_probes:
            point = [hero['centre'][0], hero['centre'][1] + up * profile['height'], hero['centre'][2] + across * profile['width']]
            depth = cut_hull.offset(point)
            check(depth is None or depth > .10, code + ' hull/gasket masks clear-aperture probe ' + str((across, up)))
        original_hero = min(components(kit, hero['node']), key=lambda p: abs(p['centre'][2] - profile['z']))
        outline = convex_outline(original_hero['points'])
        outline = [(hero['centre'][2] + (z - hero['centre'][2]) * .94, hero['centre'][1] + (y - hero['centre'][1]) * .94) for z, y in outline]
        grid_count = 0
        for iz in range(-10, 11):
            for iy in range(-10, 11):
                z = hero['centre'][2] + iz / 20 * profile['width'] * .98
                y = hero['centre'][1] + iy / 20 * profile['height'] * .98
                crosses = [(b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (z - a[0]) for a, b in zip(outline, outline[1:] + outline[:1])]
                if not all(v >= -1e-10 for v in crosses):
                    continue
                depth = cut_hull.offset([hero['centre'][0], y, z])
                check(depth is None or depth > .10, code + ' hull/gasket masks achieved clear-outline grid probe ' + str((iz, iy)))
                grid_count += 1
        check(grid_count >= 300, code + ' clear aperture sampling unexpectedly sparse')
        hero['clear_aperture_ray_probes'] = len(clear_probes) + grid_count
        check(abs(hero['centre'][2] - profile['z']) < 2e-5, code + ' profile misses hero pane centre')
        check(abs(hero['centre'][1] - profile['y']) < .0005, code + ' profile Y misses achieved projected centre')
        check(abs(hero['size'][2] * .94 - profile['width']) < 2e-5 and abs(hero['size'][1] * .94 - profile['height']) < 2e-5, code + ' clear profile differs from achieved .94 seal opening')
        check(abs(abs(hero['centre'][0]) - profile['half_width'] - .007) < .001, code + ' profile lateral datum misses achieved pane')
        near = sorted((p for p in panes if p['centre'][0] < 0), key=lambda p: abs(p['centre'][2] - profile['z']))[:3]
        distances = [abs(p['centre'][2] - profile['z']) for p in near[1:]]
        check(all(abs(v - profile['window_pitch']) < 3e-5 for v in distances), code + ' profile local pitch differs from mesh')
        for suffix in ('.gltf', '.bin'):
            check((ROOT / ART / (stem + suffix)).read_bytes() == (ROOT / MIRROR / (stem + suffix)).read_bytes(), code + ' packaged mirror differs')
        report['aircraft'][code] = {'pane_count': len(panes), 'profile': profile, 'hero': hero, 'panes': panes, 'non_window_nodes_unchanged': sum(not n.startswith('cabin_window_') and n not in ALLOWED_GROUPS for n in kit.nodes)}
        print(f'{code}: {len(panes)} connected panes; .287234×.500 glass/.270×.470 clear aperture; stations/pitch retained; hull/FBX/mirror/profile fit pass')
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
