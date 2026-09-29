#!/usr/bin/env python3
"""Render the Adelaide Airport map data to a top-down PNG for QA without Unity (ADR 0184).

Draws, in the runway frame: buildings and car parks, aprons/taxiways/runways, every road from the
generated road network (real width, by class colour), junctions and street furniture.
Needs Pillow (pip install pillow). Data © OpenStreetMap contributors, ODbL.

Run: python3 scripts/render-ypad-map.py [--window x0,z0,x1,z1] [--px-per-m 1.0] [--out FILE]
     --airside-only   only roads flagged airside
Default window is the airport core and terminal precinct.
"""
import argparse
import importlib.util
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ypad_osm import ROOT, load_snapshot, way_points  # noqa: E402

spec = importlib.util.spec_from_file_location("gen_roads", os.path.join(os.path.dirname(__file__), "generate-ypad-roads.py"))
gen = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gen)

ROAD_COLOUR = {0: (222, 120, 60), 1: (222, 120, 60), 2: (232, 168, 80), 3: (238, 200, 110), 4: (250, 240, 170),
               5: (235, 235, 235), 6: (250, 250, 250), 7: (240, 235, 220), 8: (185, 190, 200), 9: (170, 150, 110)}
AIRSIDE = (90, 140, 230)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--window", default="-1800,-1200,2600,1400")
    ap.add_argument("--px-per-m", type=float, default=0.5)
    ap.add_argument("--out", default=os.path.join(ROOT, "docs/testing/map-2026-09-29/ypad-roads-overview.png"))
    ap.add_argument("--airside-only", action="store_true")
    args = ap.parse_args()
    x0, z0, x1, z1 = (float(v) for v in args.window.split(","))
    s = args.px_per_m
    W, H = int((x1 - x0) * s), int((z1 - z0) * s)
    img = Image.new("RGB", (W, H), (58, 84, 52))
    d = ImageDraw.Draw(img)

    def px(p):
        return ((p[0] - x0) * s, (z1 - p[1]) * s)  # z up (north-west up)

    snap = gen.latest_snapshot()
    data, roads, points, names, junctions, furniture = gen.build(snap)

    for e in data["elements"]:
        if e["type"] != "way":
            continue
        t = e["tags"]
        pts = [px(p) for p in way_points(e)]
        if len(pts) < 3:
            continue
        if t.get("aeroway") in ("apron", "taxiway", "runway", "taxilane") and pts[0] == pts[-1]:
            d.polygon(pts, fill=(128, 128, 132))
        elif t.get("aeroway") in ("runway",):
            d.line(pts, fill=(60, 60, 64), width=max(2, int(45 * s)))
        elif t.get("amenity") == "parking" and pts[0] == pts[-1]:
            d.polygon(pts, fill=(96, 98, 104))
        elif "building" in t and pts[0] == pts[-1]:
            d.polygon(pts, fill=(196, 182, 160), outline=(120, 108, 92))
    for e in data["elements"]:
        t = e["tags"]
        if e["type"] == "way" and t.get("amenity") == "parking_space":
            pts = [px(p) for p in way_points(e)]
            if len(pts) > 2:
                d.polygon(pts, outline=(230, 230, 200))
        if e["type"] == "way" and t.get("highway") in ("footway", "cycleway", "path", "pedestrian") and t.get("footway") != "crossing":
            d.line([px(p) for p in way_points(e)], fill=(200, 196, 180), width=max(1, int(1.6 * s)))
        if e["type"] == "way" and t.get("building") in ("roof", "carport"):
            pts = [px(p) for p in way_points(e)]
            if len(pts) > 2:
                d.polygon(pts, fill=(150, 160, 170))
        if e["type"] == "way" and t.get("power") == "generator":
            pts = [px(p) for p in way_points(e)]
            if len(pts) > 2:
                d.polygon(pts, fill=(30, 45, 100))
        if e["type"] == "way" and t.get("man_made") == "storage_tank":
            pts = [px(p) for p in way_points(e)]
            if len(pts) > 2:
                d.polygon(pts, fill=(225, 226, 222))
    for e in data["elements"]:
        if e["type"] == "way" and e["tags"].get("aeroway") in ("runway", "taxiway", "taxilane"):
            w = 45 if e["tags"]["aeroway"] == "runway" else 23
            d.line([px(p) for p in way_points(e)], fill=(70, 70, 74), width=max(1, int(w * s)))

    for r in sorted(roads, key=lambda r: r["width"], reverse=True):
        if args.airside_only and not r["flags"] & gen.F_AIRSIDE:
            continue
        pts = [px(p) for p in points[r["start"]:r["start"] + r["count"]]]
        col = AIRSIDE if r["flags"] & gen.F_AIRSIDE else ROAD_COLOUR[r["cls"]]
        d.line(pts, fill=col, width=max(1, int(r["width"] * s)), joint="curve")
    for x, z, hw, deg in junctions:
        cx, cy = px((x, z))
        rr = max(1.0, hw * s)
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(200, 200, 205) if deg > 2 else None)
    for kind, x, z, yaw, _w in furniture:
        cx, cy = px((x, z))
        col = {0: (255, 255, 255), 1: (255, 60, 60), 4: (60, 200, 255), 5: (255, 230, 0)}.get(kind)
        if col:
            d.ellipse([cx - 1.5, cy - 1.5, cx + 1.5, cy + 1.5], fill=col)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    img.save(args.out)
    print(f"wrote {os.path.relpath(args.out, ROOT)} {W}x{H}: {len(roads)} roads")


if __name__ == "__main__":
    main()
