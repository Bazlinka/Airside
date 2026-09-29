"""Shared helpers for the Adelaide Airport (YPAD) OpenStreetMap map-overhaul scripts (ADR 0184).

Data © OpenStreetMap contributors, ODbL 1.0.

Frame — identical to scripts/generate-ypad-layout.py and Simulation/YpadFrame.cs, in metres:
  x along runway 05/23 from the midpoint of its thresholds, positive towards 23 (NE)
  z across it, positive to the left of 05->23 (the north-west / terminal side)

Standard library only, so every generator runs on a bare Python 3.
"""
import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OSM_DIR = os.path.join(ROOT, "docs/data/osm")

LAT0, LON0 = -34.95, 138.53
RWY05 = {"lat": -34.9585244, "lon": 138.5172171}
RWY23 = {"lat": -34.9406962, "lon": 138.5431392}
M_PER_DEG_LON = 111320.0 * math.cos(math.radians(LAT0))
M_PER_DEG_LAT = 110574.0


def _xy(lat, lon):
    return ((lon - LON0) * M_PER_DEG_LON, (lat - LAT0) * M_PER_DEG_LAT)


_A, _B = _xy(RWY05["lat"], RWY05["lon"]), _xy(RWY23["lat"], RWY23["lon"])
_LEN = math.dist(_A, _B)
_U = ((_B[0] - _A[0]) / _LEN, (_B[1] - _A[1]) / _LEN)
_N = (-_U[1], _U[0])
_MID = ((_A[0] + _B[0]) / 2.0, (_A[1] + _B[1]) / 2.0)


def to_local(lat, lon):
    """Latitude/longitude -> runway-frame (x, z) metres."""
    q = _xy(lat, lon)
    v = (q[0] - _MID[0], q[1] - _MID[1])
    return (v[0] * _U[0] + v[1] * _U[1], v[0] * _N[0] + v[1] * _N[1])


def to_latlon(x, z):
    """Runway-frame (x, z) metres -> (lat, lon). Inverse of to_local."""
    e = _MID[0] + x * _U[0] + z * _N[0]
    n = _MID[1] + x * _U[1] + z * _N[1]
    return (LAT0 + n / M_PER_DEG_LAT, LON0 + e / M_PER_DEG_LON)


def load_snapshot(name):
    with open(os.path.join(OSM_DIR, name), encoding="utf-8") as f:
        return json.load(f)


def way_points(way):
    """Runway-frame polyline of a snapshot way (uses its inlined geometry)."""
    return [to_local(p["lat"], p["lon"]) for p in way["geometry"]]


def is_closed(way):
    g = way["geometry"]
    return len(g) > 3 and g[0] == g[-1]


def polyline_length(pts):
    return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))


def resample(pts, step):
    """Even spacing along a polyline, keeping both end points."""
    out = [pts[0]]
    carry = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = math.dist(a, b)
        if seg == 0:
            continue
        d = step - carry
        while d <= seg:
            t = d / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            d += step
        carry = (carry + seg) % step
    if math.dist(out[-1], pts[-1]) > 0.05:
        out.append(pts[-1])
    return out


def simplify(pts, tol):
    """Ramer-Douglas-Peucker, iterative."""
    if len(pts) < 3:
        return list(pts)
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        lo, hi = stack.pop()
        ax, az = pts[lo]
        bx, bz = pts[hi]
        dx, dz = bx - ax, bz - az
        norm = math.hypot(dx, dz)
        far, far_d = -1, tol
        for i in range(lo + 1, hi):
            px, pz = pts[i]
            d = math.hypot(px - ax, pz - az) if norm == 0 else abs(dz * (px - ax) - dx * (pz - az)) / norm
            if d > far_d:
                far, far_d = i, d
        if far >= 0:
            keep[far] = True
            stack.extend([(lo, far), (far, hi)])
    return [p for p, k in zip(pts, keep) if k]
