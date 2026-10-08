"""Continuous aircraft body lofts, with skin details carried onto the revised hull.

Project-owned station profiles retain their dimensions and family proportions.
Shape-preserving cubic interpolation rounds the straight nose/tail transitions;
shared ring vertices keep the surface smooth without multiplying draw calls.
"""
from __future__ import annotations

import numpy as np


class BodyProfile:
    def __init__(self, mesh):
        vertices, _ = mesh
        rows = []
        for z in np.unique(vertices[:, 2]):
            ring = vertices[vertices[:, 2] == z]
            rx = float(np.max(np.abs(ring[:, 0])))
            lo, hi = float(ring[:, 1].min()), float(ring[:, 1].max())
            # Ignore cap centres: the lathe's ring stations define the skin.
            if rx > 1e-5 and hi - lo > 1e-5:
                rows.append((z, rx, (hi - lo) * .5, (hi + lo) * .5))
        self.original = np.asarray(rows, np.float64)
        if len(rows) < 3:
            raise ValueError('body needs at least three cross-sections')
        # Dense linear source lofts contain redundant intermediate rings. Recover
        # their actual design stations before deriving continuous tangents.
        keep = [0]
        for k in range(1, len(rows) - 1):
            a, b, c = self.original[k-1:k+2]
            expected = a[1:] + (c[1:] - a[1:]) * ((b[0]-a[0])/(c[0]-a[0]))
            if np.max(np.abs(expected-b[1:])) > 2e-5:
                keep.append(k)
        keep.append(len(rows)-1)
        self.knots = self.original[keep]
        x, y = self.knots[:, 0], self.knots[:, 1:]
        h = np.diff(x)
        d = np.diff(y, axis=0) / h[:, None]
        m = np.zeros_like(y)
        for k in range(1, len(x)-1):
            active = d[k-1] * d[k] > 0
            w1, w2 = 2*h[k]+h[k-1], h[k]+2*h[k-1]
            m[k, active] = (w1+w2)/(w1/d[k-1, active]+w2/d[k, active])
        # Limited end derivatives prevent overshoot or a pinched cabin.
        for end, first, second, ha, hb in ((0, d[0], d[1], h[0], h[1]),
                                           (-1, d[-1], d[-2], h[-1], h[-2])):
            slope = ((2*ha+hb)*first-ha*second)/(ha+hb)
            slope = np.where(slope*first <= 0, 0, slope)
            slope = np.where((first*second <= 0) & (np.abs(slope)>3*np.abs(first)), 3*first, slope)
            m[end] = slope
        self.slopes = m
        self.minimum = float(vertices[:, 2].min())
        self.maximum = float(vertices[:, 2].max())

    def linear(self, z):
        return np.stack([np.interp(z, self.original[:, 0], self.original[:, i])
                         for i in (1, 2, 3)], axis=-1)

    def _cubic(self, z):
        z = np.asarray(z, np.float64)
        x, y = self.knots[:, 0], self.knots[:, 1:]
        z = np.clip(z, x[0], x[-1])
        k = np.clip(np.searchsorted(x, z, side='right')-1, 0, len(x)-2)
        h = x[k+1]-x[k]
        t = ((z-x[k])/h)[..., None]
        return ((2*t**3-3*t**2+1)*y[k] + (t**3-2*t**2+t)*h[..., None]*self.slopes[k]
                + (-2*t**3+3*t**2)*y[k+1] + (t**3-t**2)*h[..., None]*self.slopes[k+1])

    def sample(self, z):
        values = np.asarray(z, np.float64)
        result = self._cubic(values)
        # Close finite-radius source rings as rounded tips, rather than a flat
        # disc or the old 12 cm cone. Interpolate radius squared to a zero tip:
        # the square root gives a rounded nose with a vertical end tangent.
        for end, direction, width in ((self.minimum, 1, .25), (self.maximum, -1, .55)):
            width = min(width, (self.maximum-self.minimum)*.035)
            join = end+direction*width
            inner = self._cubic(join)
            derivative = (self._cubic(join+1e-4)-self._cubic(join-1e-4))/(2e-4)
            t = np.clip((join-values)/(direction*width), 0, 1)
            q = inner[:2]**2
            tangent = -direction*width*2*inner[:2]*derivative[:2]
            # Clamp to monotone Hermite derivatives, preventing cap bulges.
            tangent = np.clip(tangent, -3*q, 0)
            tt = t[..., None]
            squared = ((2*tt**3-3*tt**2+1)*q + (tt**3-2*tt**2+tt)*tangent
                       + (tt**3-tt**2)*(-2*q))
            cap = np.sqrt(np.maximum(squared, 0))
            active = (values-join)*direction < 0
            result[..., :2] = np.where(active[..., None], cap, result[..., :2])
        return result

    def carry(self, vertices):
        """Preserve angular placement and millimetre proud offsets of skin parts."""
        old = self.linear(vertices[:, 2])
        new = self.sample(vertices[:, 2])
        angle = np.arctan2((vertices[:, 1]-old[:, 2])/old[:, 1], vertices[:, 0]/old[:, 0])
        moved = vertices.astype(np.float64).copy()
        moved[:, 0] += (new[:, 0]-old[:, 0])*np.cos(angle)
        moved[:, 1] += new[:, 2]-old[:, 2] + (new[:, 1]-old[:, 1])*np.sin(angle)
        return moved.astype(np.float32)

    def loft(self, *, segments=72, start=None, end=None, offset=0.0, caps=True):
        start = self.minimum if start is None else start
        end = self.maximum if end is None else end
        # Subdivide curved sections to a 2.5 mm chord error. Cabin rings are
        # capped at 60 cm so the aperture cutter does not bisect long thin faces.
        anchors = np.unique(np.r_[start, end,
            self.knots[(self.knots[:, 0]>=start)&(self.knots[:, 0]<=end), 0],
            np.linspace(start, min(end, start+.25), 8),
            np.linspace(max(start, end-.55), end, 12)])
        stations = [start]

        def divide(a, b, depth=0):
            tests = np.asarray((a+(b-a)*.25, (a+b)*.5, a+(b-a)*.75))
            va, vb = self.sample(a), self.sample(b)
            linear = va[None]+(vb-va)[None]*np.asarray((.25, .5, .75))[:, None]
            error = np.max(np.abs(self.sample(tests)-linear))
            if (error > .0025 or b-a > .6) and depth < 8:
                middle = (a+b)*.5
                divide(a, middle, depth+1)
                divide(middle, b, depth+1)
            else:
                stations.append(b)

        for a, b in zip(anchors[:-1], anchors[1:]):
            divide(a, b)
        zs = np.asarray(stations)
        values = self.sample(zs)
        values[:, :2] = np.maximum(values[:, :2], 1e-5)
        theta = np.arange(segments)*2*np.pi/segments
        v = np.empty((len(zs), segments, 3), np.float32)
        v[:, :, 0] = (values[:, 0, None]+offset)*np.cos(theta)
        v[:, :, 1] = values[:, 2, None]+(values[:, 1, None]+offset)*np.sin(theta)
        v[:, :, 2] = zs[:, None]
        v = v.reshape(-1, 3)
        faces = []
        for ring in range(len(zs)-1):
            for j in range(segments):
                a = ring*segments+j
                b = ring*segments+(j+1)%segments
                # Increasing Z: these wind outward along the ellipse.
                faces.extend((a, b, b+segments, a, b+segments, a+segments))
        if caps:
            for ring, z in ((0, self.minimum), (len(zs)-1, self.maximum)):
                centre = len(v)
                cy = values[ring, 2]
                v = np.vstack((v, (0, cy, z))).astype(np.float32)
                for j in range(segments):
                    a, b = ring*segments+j, ring*segments+(j+1)%segments
                    faces.extend((centre, b, a) if ring == 0 else (centre, a, b))
        if len(v) >= 65536:
            raise ValueError('body exceeds 16-bit index budget')
        return v, np.asarray(faces, np.uint16)


SKIN_PARTS = ('radome', 'flightdeck', 'windscreen', 'cockpit', 'cabin_window',
              'door_', 'cargo_door', 'livery_', 'antenna', 'beacon_', 'taxi_light',
              'landing_light', 'pitot', 'static_port', 'rescue_red_belly',
              'sliding_door', 'window_glass', 'door_seam', 'door_handle')


def refine(meshes):
    """Refine one raw airframe before glazing apertures and fitted paint are built."""
    profile = BodyProfile(meshes['fuselage'])
    meshes['fuselage'] = profile.loft()
    for name, (vertices, indices) in list(meshes.items()):
        if name.startswith(SKIN_PARTS):
            moved = profile.carry(vertices)
            if name.startswith(('door_', 'cargo_door')) and not any(
                    key in name for key in ('handle', 'latch')):
                # Some variant doors originated on a narrower donor airframe.
                # Fit both leaf faces to this type's actual hull, not that donor.
                radii = profile.sample(moved[:, 2])
                sign = -1 if np.mean(moved[:, 0]) < 0 else 1
                q = 1-((moved[:, 1]-radii[:, 2])/np.maximum(radii[:, 1], 1e-6))**2
                valid = q >= 0
                surface_x = radii[:, 0]*np.sqrt(np.maximum(q, 0))
                half = len(moved)//2
                proud = .006 if 'outline' in name else .010
                offsets = np.r_[np.full(half, proud), np.full(len(moved)-half, proud-.004)]
                moved[valid, 0] = sign*(surface_x[valid]+offsets[valid])
            if name.startswith(('door_', 'cargo_door')) and any(
                    key in name for key in ('handle', 'latch')):
                radii = profile.sample(moved[:, 2])
                q = 1-((moved[:, 1]-radii[:, 2])/np.maximum(radii[:, 1], 1e-6))**2
                sign = -1 if np.mean(moved[:, 0]) < 0 else 1
                relief = np.abs(moved[:, 0])-np.median(np.abs(moved[:, 0]))
                moved[:, 0] = sign*(radii[:, 0]*np.sqrt(np.maximum(q, 0))+.025+relief)
            meshes[name] = moved, indices
    # A separately capped polygonal radome would preserve the old nose facets.
    # Its open aft edge overlaps the continuous hull, retaining its material node.
    if 'radome' in meshes:
        v, _ = meshes['radome']
        start = max(float(v[:, 2].min()), float(profile.original[0, 0]))
        end = min(float(v[:, 2].max()), float(profile.original[-1, 0]))
        if end > start:
            meshes['radome'] = profile.loft(start=start, end=end, offset=.006, caps=False)
    return profile
