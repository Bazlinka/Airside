#!/usr/bin/env python3
"""Refine/check the existing fleet kits in place, preserving unrelated geometry."""
import argparse
import importlib.util
from pathlib import Path
import numpy as np
from aircraft_intakes import refine

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('fleet_kits', HERE/'refine-aircraft-tails.py')
kits = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kits)


def verify(meshes):
    for name, (v, i) in meshes.items():
        assert np.isfinite(v).all() and i.max() < len(v), name
    for side in ('left', 'right'):
        fan = 'fan_'+side
        if fan not in meshes: continue
        v = meshes[fan][0]; lo, hi = v.min(0), v.max(0)
        centre = (lo[:2]+hi[:2])/2; radii = (hi[:2]-lo[:2])/2
        # Probe the visible fan aperture, not just its centre. These rays fail
        # on the original widebody's shallow conical cap.
        for angle in np.linspace(0, 2*np.pi, 12, endpoint=False):
            point = centre + radii*.65*np.array([np.cos(angle), np.sin(angle)])
            for key in ('engine_'+side, 'nacelle_'+side, 'intake_'+side):
                v, indices = meshes[key]; p = v[indices.reshape(-1, 3)]
                a, b, c = p[:, 0], p[:, 1], p[:, 2]
                det = (b[:, 1]-c[:, 1])*(a[:, 0]-c[:, 0])+(c[:, 0]-b[:, 0])*(a[:, 1]-c[:, 1])
                valid = np.abs(det) > 1e-8
                u = np.zeros(len(p)); w = u.copy()
                u[valid] = ((b[valid, 1]-c[valid, 1])*(point[0]-c[valid, 0])+(c[valid, 0]-b[valid, 0])*(point[1]-c[valid, 1]))/det[valid]
                w[valid] = ((c[valid, 1]-a[valid, 1])*(point[0]-c[valid, 0])+(a[valid, 0]-c[valid, 0])*(point[1]-c[valid, 1]))/det[valid]
                z = u*a[:, 2]+w*b[:, 2]+(1-u-w)*c[:, 2]
                blocked = valid & (u >= 0) & (w >= 0) & (u+w <= 1) & (z > hi[2]+.001)
                assert not blocked.any(), (key, angle, 'fan occluded')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    writer = kits.module('generate-authored-fbx-turboprop-terminal.py')
    for cid, path in kits.models().items():
        meshes = kits.readkit(path); old = dict(meshes)
        removed = refine(meshes)
        verify(meshes)
        if args.check:
            assert old.keys() == meshes.keys() and all(np.array_equal(v, old[n][0]) and np.array_equal(i, old[n][1])
                for n, (v, i) in meshes.items()), cid+' regeneration differs'
            print(cid+': deterministic finish; visible fan aperture; finite geometry')
        elif removed or old.keys() != meshes.keys():
            writer.write_kit(path.parent, path.stem, meshes)
            print(cid+': removed '+str(removed)+' blocking faces')


if __name__ == '__main__': main()
