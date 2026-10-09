#!/usr/bin/env python3
"""Bounded geometry checks for the fleet's continuous aircraft body lofts."""
from pathlib import Path
import importlib.util
import numpy as np
from aircraft_body import BodyProfile, SKIN_PARTS, refine

HERE = Path(__file__).resolve().parent

def module(filename):
    spec = importlib.util.spec_from_file_location('body_check_'+filename.replace('-', '_'), HERE/filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def check(cid, meshes):
    baseline = {name: (v.copy(), i.copy()) for name, (v, i) in meshes.items()}
    profile = refine(meshes)
    z = np.linspace(profile.minimum, profile.maximum, 4096)
    values = profile.sample(z)
    assert np.isfinite(values).all() and (values[:, :2] >= 0).all(), cid
    # No swollen or pinched parallel cabin: no radius exceeds its authored max.
    assert np.all(values[:, :2].max(0) <= profile.original[:, 1:3].max(0)+1e-5), cid
    assert np.allclose(profile.sample([profile.minimum, profile.maximum])[:, :2], 0), cid
    # First derivatives agree on both sides of interior design stations.
    for knot in profile.knots[1:-1, 0]:
        if knot < profile.minimum+.25 or knot > profile.maximum-.55:
            continue
        e = 1e-5
        a, b, c = profile.sample(np.array([knot-e, knot, knot+e]))
        assert np.max(np.abs((b-a)/e-(c-b)/e)) < .002, (cid, knot)
    vertices, indices = meshes['fuselage']
    assert len(vertices) < 65536 and indices.max() < len(vertices), cid
    assert np.allclose([vertices[:, 2].min(), vertices[:, 2].max()],
                       [profile.minimum, profile.maximum], atol=1e-5), cid
    triangles = vertices[indices.reshape(-1, 3)]
    volume = np.einsum('ij,ij->i', triangles[:, 0], np.cross(triangles[:, 1], triangles[:, 2])).sum()/6
    assert volume > 0, cid
    for name, (v, i) in meshes.items():
        assert np.isfinite(v).all() and i.max() < len(v), (cid, name)
        if name != 'fuselage' and not name.startswith(SKIN_PARTS):
            # Wing/gear/engine/fan geometry and animation datums are unchanged.
            assert np.array_equal(v, baseline[name][0]) and np.array_equal(i, baseline[name][1]), (cid, name)
    print(f'{cid}: {len(vertices)} body vertices, positive volume, bounded radii, C1 joins, fixed moving parts')


if __name__ == '__main__':
    glazing = module('polish-aircraft-glazing.py')
    for cid, (file, function, _) in glazing.SOURCES.items():
        check(cid, getattr(module(file), function)())
    # Bell's entry point already refines its body. Check its actual output here;
    # feeding its opened/finished meshes through the refinement twice is invalid.
    bell = module('generate-air-017-bell-412.py')
    bell.validate(bell.bell_412_meshes())
    print('B412: retained dimensional envelope, four main blades and opposed tail blades')
    # Trainer is a direct generator; inspect its committed source asset without
    # importing that generator (which intentionally writes files on execution).
    import json
    path = HERE.parent/'game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_parafield_trainer_v01.gltf'
    doc = json.loads(path.read_text())
    blob = path.with_suffix('.bin').read_bytes()
    positions = {}
    for node in doc['nodes']:
        primitive = doc['meshes'][node['mesh']]['primitives'][0]
        accessor = doc['accessors'][primitive['attributes']['POSITION']]
        view = doc['bufferViews'][accessor['bufferView']]
        v = np.frombuffer(blob, dtype='<f4', count=accessor['count']*3,
            offset=view.get('byteOffset', 0)+accessor.get('byteOffset', 0)).reshape(-1, 3)
        assert np.isfinite(v).all(), node['name']
        positions[node['name']] = v
    all_vertices = np.concatenate(list(positions.values()))
    assert 10.9 <= np.ptp(all_vertices[:, 0]) <= 11.1
    assert 8.2 <= np.ptp(all_vertices[:, 2]) <= 8.5
    for side in ('left', 'right'):
        assert np.ptp(positions['window_glass_'+side][:, 0]) > .01
    print('YPPF trainer: retained envelope, finite geometry and curved side glazing')
