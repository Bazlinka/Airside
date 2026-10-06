#!/usr/bin/env python3
"""Static regression for the shipped Bell's two-blade tail rotor; no Unity/rendering."""
import json
import math
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parent.parent
RELATIVE = Path('Airside/Art/Models/Aircraft/mdl_bell_412_rescue_v01')
ASSETS = ROOT / 'game/Airside/Assets'
MODEL = ASSETS / RELATIVE


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    gltf = json.loads(MODEL.with_suffix('.gltf').read_text())
    blob = MODEL.with_suffix('.bin').read_bytes()
    fbx = MODEL.with_suffix('.fbx').read_text()

    def positions(node):
        primitive = gltf['meshes'][node['mesh']]['primitives'][0]
        accessor = gltf['accessors'][primitive['attributes']['POSITION']]
        view = gltf['bufferViews'][accessor['bufferView']]
        check(accessor['componentType'] == 5126 and accessor['type'] == 'VEC3', 'expected float XYZ')
        start = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
        stride = view.get('byteStride', 12)
        return [struct.unpack_from('<fff', blob, start + i * stride) for i in range(accessor['count'])]

    nodes = [n for n in gltf['nodes'] if n.get('name', '').startswith('tail_rotor_blade')]
    check({n['name'] for n in nodes} == {'tail_rotor_blade_1', 'tail_rotor_blade_2'} and len(nodes) == 2,
          'expected exactly two retained rig blade names')
    hub = positions(next(n for n in gltf['nodes'] if n['name'] == 'tail_rotor_hub'))
    axis_yz = [(min(p[d] for p in hub) + max(p[d] for p in hub)) / 2 for d in (1, 2)]
    centres = []
    for node in nodes:
        check(not any(k in node for k in ('matrix', 'rotation', 'translation', 'scale')),
              'fixture expects metre-authored identity node transforms')
        points = positions(node)
        check(all(math.isfinite(v) for p in points for v in p), 'non-finite blade position')
        check(abs(max(p[0] for p in points) - min(p[0] for p in points) - .08) < 1e-5,
              'tail blade is not shaft-aligned: its thin dimension must be X')
        radial = [(p[1] - axis_yz[0], p[2] - axis_yz[1]) for p in points]
        centre = tuple(sum(p[d] for p in radial) / len(radial) for d in (0, 1))
        length = math.hypot(*centre)
        check(abs(length - .6625) < 1e-5, 'half-span centre must sit half a blade radius from hub')
        unit = [v / length for v in centre]
        along = [p[0] * unit[0] + p[1] * unit[1] for p in radial]
        across = [p[0] * unit[1] - p[1] * unit[0] for p in radial]
        check(abs(min(along)) < 1e-5 and abs(max(along) - 1.325) < 1e-5,
              'blade must connect to hub and occupy only one 1.325 m half-span')
        check(max(abs(v) for v in across) <= .11001, 'blade exceeds retained .22 m chord')
        check(abs(max(math.hypot(*p) for p in radial) - math.hypot(1.325, .11)) < 1e-5,
              'tail rotor radius changed')
        centres.append(centre)
        block = re.search(r'Geometry: \d+, "Geometry::' + re.escape(node['name']) +
                          r'", "Mesh" \{(.*?)(?=    Model:)', fbx, re.S)
        check(block is not None, 'FBX is missing tail blade')
        arrays = {}
        for name in ('Vertices', 'Normals'):
            match = re.search(name + r': \*(\d+) \{\s*a: ([^\n]+)', block.group(1))
            check(match is not None, 'FBX array missing: ' + name)
            values = [float(v.strip()) for v in match.group(2).split(',')]
            check(len(values) == int(match.group(1)) == len(points) * 3, 'FBX array count differs')
            check(all(math.isfinite(v) for v in values), 'non-finite FBX ' + name)
            arrays[name] = values
        check(all(abs(a - b) < 1.1e-6 for a, b in zip(arrays['Vertices'], (v for p in points for v in p))),
              'FBX and shipped glTF blade positions differ')
        for i in range(0, len(arrays['Normals']), 3):
            check(abs(math.sqrt(sum(v * v for v in arrays['Normals'][i:i + 3])) - 1) < 2e-6,
                  'FBX blade normal is not finite unit length')
    check(all(abs(a + b) < 1e-5 for a, b in zip(*centres)), 'two blades must be diametrically opposed')
    mirror = ASSETS / 'StreamingAssets' / RELATIVE
    for extension in ('.gltf', '.bin'):
        check(MODEL.with_suffix(extension).read_bytes() == mirror.with_suffix(extension).read_bytes(),
              'packaged Bell mirror differs: ' + extension)
    print('Bell tail rotor: two connected opposed half-spans; X shaft/YZ plane; retained radius; '
          'finite FBX normals; FBX/glTF positions and packaged mirrors match.')


if __name__ == '__main__':
    main()
