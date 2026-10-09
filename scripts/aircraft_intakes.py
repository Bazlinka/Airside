"""Open authored engine mouths without changing their envelope or moving parts.

Closed lathe end caps used to cover the recessed jet fans. Keep the lips and
outside cowls, remove only end-cap faces over the opening, and fit an inward
liner to the existing fan. Small prop/rotorcraft inlets get a recessed dark cup.
"""
import numpy as np


def cup(cx, cy, front, back, rx, ry, rear_scale=0.92, capped=False):
    n = 64
    angle = np.arange(n) * (2 * np.pi / n)
    rings = [np.column_stack((cx + rx*s*np.cos(angle), cy + ry*s*np.sin(angle),
                             np.full(n, z))) for z, s in ((front, 1), (back, rear_scale))]
    vertices = np.vstack(rings).astype(np.float32)
    faces = []
    for a in range(n):
        b = (a+1) % n
        # Inward-facing duct wall: visible from the mouth rather than outside.
        faces.extend((a, n+b, b, a, n+a, n+b))
    if capped:
        vertices = np.vstack((vertices, [cx, cy, back])).astype(np.float32)
        for a in range(n):
            faces.extend((2*n, n+a, n+(a+1) % n))
    return vertices, np.asarray(faces, np.uint16)


def open_cap(mesh, fan_front, cx, cy, rx, ry):
    v, indices = mesh
    tris = indices.reshape(-1, 3)
    p = v[tris]
    radius = ((p[:, :, 0]-cx)/rx)**2 + ((p[:, :, 1]-cy)/ry)**2
    # Lathe end caps can be shallow cones rather than coplanar discs. Their
    # entire face is ahead of the fan and reaches inside the opening.
    cap = p[:, :, 2].min(axis=1) > fan_front
    cap &= radius.min(axis=1) < 0.98**2
    return (v, tris[~cap].reshape(-1).astype(np.uint16)), int(cap.sum())


def refine(meshes):
    removed = 0
    for side in ('left', 'right'):
        inlet = 'intake_'+side
        fan = 'fan_'+side
        if fan in meshes and inlet in meshes:
            v = meshes[fan][0]
            lo, hi = v.min(0), v.max(0)
            cx, cy = (lo[:2]+hi[:2])/2
            rx, ry = (hi[:2]-lo[:2])/2
            front = hi[2]
            for key in ('engine_'+side, 'nacelle_'+side, inlet):
                if key in meshes:
                    meshes[key], count = open_cap(meshes[key], front, cx, cy, rx, ry)
                    removed += count
            # Start at the existing intake's inside ring, end just behind the
            # fan face. Outside dimensions and the fan pivot remain unchanged.
            mouth = meshes[inlet][0]
            z = mouth[:, 2].min()
            ring = mouth[np.abs(mouth[:, 2]-z) < 1e-5]
            radius = np.sqrt(((ring[:, 0]-cx)/rx)**2 + ((ring[:, 1]-cy)/ry)**2)
            inner = radius.min()
            meshes['intake_liner_'+side] = cup(cx, cy, z, front-0.008, rx*inner,
                ry*inner, rear_scale=1/inner)
        elif inlet in meshes:
            v = meshes[inlet][0]
            lo, hi = v.min(0), v.max(0)
            cx, cy = (lo[:2]+hi[:2])/2
            rx, ry = (hi[:2]-lo[:2])/2
            back = hi[2]-max(0.04, (hi[2]-lo[2])*0.65)
            meshes[inlet], count = open_cap(meshes[inlet], back, cx, cy, rx*.92, ry*.92)
            removed += count
            meshes['intake_liner_'+side] = cup(cx, cy, hi[2]-0.006, back,
                rx*.92, ry*.92, capped=True)
        elif 'engine_exhaust_'+side in meshes:
            # Bell's forward engine housing has a separate small intake, not a
            # jet fan. The mouth follows its actual elliptical front section.
            key = 'engine_'+side
            v = meshes[key][0]
            for front in sorted(np.unique(v[:, 2]), reverse=True):
                ring = v[np.abs(v[:, 2]-front) < 1e-5]
                if np.ptp(ring[:, 0]) > .025 and np.ptp(ring[:, 1]) > .025:
                    break
            lo, hi = ring.min(0), ring.max(0)
            cx, cy = (lo[:2]+hi[:2])/2
            rx, ry = (hi[:2]-lo[:2])/2
            meshes[key], count = open_cap(meshes[key], front-.16, cx, cy, rx*.92, ry*.92)
            removed += count
            meshes['intake_liner_'+side] = cup(cx, cy, front-.006, front-.16,
                rx*.94, ry*.94, capped=True)
    return removed
