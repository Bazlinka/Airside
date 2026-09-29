#!/usr/bin/env python3
"""Audit the map data for empty spots (ADR 0184): buildings with no street near them.

Builds the road network exactly as generate-ypad-roads.py does, then measures, for every building in the newest
docs/data/osm/adelaide-suburb-buildings-*.json, the distance from its centre to the nearest non-airside road.
Reports how many are farther than --reach metres and where they cluster; exits 1 above --max-percent.

  python3 scripts/audit-ypad-map.py [--reach 60] [--max-percent 0.5]
"""
import argparse
import collections
import glob
import importlib.util
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ypad_osm import OSM_DIR, to_local  # noqa: E402

spec = importlib.util.spec_from_file_location("gen_roads", os.path.join(os.path.dirname(__file__), "generate-ypad-roads.py"))
gen = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gen)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--reach", type=float, default=60.0)
    ap.add_argument("--max-percent", type=float, default=0.5)
    args = ap.parse_args()
    _, roads, points, _, _, _ = gen.build(gen.latest_snapshot())
    cell = 60.0
    grid = collections.defaultdict(list)
    for r in roads:
        if r["flags"] & gen.F_AIRSIDE:
            continue
        pts = points[r["start"]:r["start"] + r["count"]]
        for a, b in zip(pts, pts[1:]):
            n = int(math.dist(a, b) / cell) + 1
            for i in range(n + 1):
                t = i / n
                grid[(int((a[0] + (b[0] - a[0]) * t) // cell), int((a[1] + (b[1] - a[1]) * t) // cell))].append((a, b))

    def nearest(p):
        best = 1e9
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for a, b in grid.get((int(p[0] // cell) + dx, int(p[1] // cell) + dz), ()):
                    vx, vz = b[0] - a[0], b[1] - a[1]
                    l2 = vx * vx + vz * vz
                    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vz) / l2))
                    best = min(best, math.hypot(p[0] - a[0] - vx * t, p[1] - a[1] - vz * t))
        return best

    boundary = gen.load_snapshot(gen.BOUNDARY)
    outline = next(gen.way_points(e) for e in boundary["elements"] if e["id"] == gen.BOUNDARY_WAY)
    files = sorted(glob.glob(os.path.join(OSM_DIR, "adelaide-suburb-buildings-*.json")))
    buildings = json.load(open(files[-1], encoding="utf-8"))["buildings"]
    stranded, total = [], 0
    for b in buildings:
        pts = [to_local(p[1], p[0]) for p in b["geometry"]]
        c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
        if abs(c[0]) > 4200 or abs(c[1]) > 3600:      # beyond the band the game draws buildings in
            continue
        if gen.point_in_polygon(c, outline):          # airport buildings are reached by airside roads
            continue
        total += 1
        radius = max(math.dist(c, q) for q in pts)          # a big building is reached from its edge, not its middle
        if nearest(c) - radius > args.reach:
            stranded.append((round(c[0]), round(c[1]), b["tags"].get("building")))
    pct = 100.0 * len(stranded) / max(1, total)
    clusters = collections.Counter((x // 300 * 300, z // 300 * 300) for x, z, _ in stranded)
    print(f"{os.path.basename(files[-1])}: {total} buildings, {len(stranded)} ({pct:.2f} %) farther than {args.reach:g} m from a road")
    print("clusters (300 m cells):", clusters.most_common(8))
    sys.exit(1 if pct > args.max_percent else 0)


if __name__ == "__main__":
    main()
