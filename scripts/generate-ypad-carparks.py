#!/usr/bin/env python3
"""Generate Adelaide Airport's real car parks for Airside (ADR 0184, map overhaul P1c).

Reads docs/data/osm/ypad-map-<date>.json (© OpenStreetMap contributors, ODbL) and writes
game/Airside/Assets/Airside/Simulation/AdelaideCarParks.cs:

  Polygons  the surface car parks (amenity=parking) near the terminal, in the runway frame
  Runs      straight rows of 90-degree bays either side of each parking aisle, kept inside the car park,
            clear of other roads and buildings; plus one-bay runs for every mapped parking_space
  Lamps     street lamps (highway=street_lamp), for the whole area

Which bays hold a car, and which car, is decided at runtime by a hash (deterministic), not stored.
Standard library only.

Run: python3 scripts/generate-ypad-carparks.py            (writes)
     python3 scripts/generate-ypad-carparks.py --check    (fails if the file is stale)
"""
import argparse
import collections
import glob
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ypad_osm import ROOT, OSM_DIR, is_closed, load_snapshot, simplify, to_local, way_points  # noqa: E402

OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideCarParks.cs")
CENTRE = (1300.0, 500.0)      # Terminal 1 forecourt
RADIUS = 3500.0               # car parks and bays beyond this are left to the satellite photo
BAY_PITCH = 2.6
BAY_DEPTH = 5.0
AISLE_HALF = 2.75             # half of a 5.5 m aisle
MIN_AREA = 120.0
SKIP_PARKING = {"street_side", "lane", "multi-storey", "underground", "rooftop"}


def latest_snapshot():
    files = sorted(glob.glob(os.path.join(OSM_DIR, "ypad-map-*.json")))
    if not files:
        sys.exit("no docs/data/osm/ypad-map-*.json — run scripts/fetch-ypad-osm.py first")
    return files[-1]


def area(pts):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(pts, pts[1:] + pts[:1]))) / 2.0


def inside(pt, poly):
    x, z = pt
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi:
            hit = not hit
        j = i
    return hit


def bbox(poly):
    xs = [p[0] for p in poly]
    zs = [p[1] for p in poly]
    return (min(xs), min(zs), max(xs), max(zs))


class Cells:
    """Spatial hash of items with a bounding box, for containment and proximity queries."""

    def __init__(self, cell=60.0):
        self.cell = cell
        self.map = collections.defaultdict(list)

    def add(self, box, item):
        c = self.cell
        for ix in range(int(box[0] // c), int(box[2] // c) + 1):
            for iz in range(int(box[1] // c), int(box[3] // c) + 1):
                self.map[(ix, iz)].append(item)

    def at(self, x, z):
        return self.map.get((int(x // self.cell), int(z // self.cell)), ())


def seg_dist(p, a, b):
    vx, vz = b[0] - a[0], b[1] - a[1]
    l2 = vx * vx + vz * vz
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vz) / l2))
    return math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vz * t))


def build(snapshot):
    data = load_snapshot(os.path.basename(snapshot))
    els = data["elements"]

    parks = []
    for e in els:
        t = e["tags"]
        if e["type"] != "way" or t.get("amenity") != "parking" or not is_closed(e):
            continue
        if t.get("parking") in SKIP_PARKING or t.get("building"):
            continue
        pts = way_points(e)[:-1]
        if math.dist(pts[0], CENTRE) > RADIUS or area(pts) < MIN_AREA:
            continue
        parks.append({"id": e["id"], "pts": pts, "box": bbox(pts)})
    parks.sort(key=lambda p: p["id"])
    park_cells = Cells()
    for p in parks:
        park_cells.add(p["box"], p)

    buildings = Cells(50.0)
    for e in els:
        t = e["tags"]
        if e["type"] == "way" and ("building" in t or "building:part" in t) and is_closed(e):
            pts = way_points(e)[:-1]
            if len(pts) >= 3:
                buildings.add(bbox(pts), pts)

    # every road centreline, to keep bays off other roads (the parent aisle is excluded by way id)
    road_cells = Cells(40.0)
    for e in els:
        t = e["tags"]
        if e["type"] == "way" and "highway" in t and t["highway"] not in ("footway", "cycleway", "path", "steps",
                                                                            "corridor", "pedestrian"):
            pts = simplify(way_points(e), 0.5)
            for a, b in zip(pts, pts[1:]):
                road_cells.add((min(a[0], b[0]), min(a[1], b[1]), max(a[0], b[0]), max(a[1], b[1])), (e["id"], a, b))

    def in_park(pt):
        for p in park_cells.at(*pt):
            if p["box"][0] <= pt[0] <= p["box"][2] and p["box"][1] <= pt[1] <= p["box"][3] and inside(pt, p["pts"]):
                return True
        return False

    def in_building(pt):
        for poly in buildings.at(*pt):
            if inside(pt, poly):
                return True
        return False

    def near_other_road(pt, own, clearance):
        for wid, a, b in road_cells.at(*pt):
            if wid != own and seg_dist(pt, a, b) < clearance:
                return True
        return False

    taken = Cells(4.0)

    def free_bay(pt):
        for q in taken.at(*pt):
            if math.dist(pt, q) < 2.1:
                return False
        return True

    runs = []
    aisles = [e for e in els if e["type"] == "way" and e["tags"].get("highway") == "service"
              and e["tags"].get("service") == "parking_aisle"]
    aisles.sort(key=lambda e: e["id"])

    # mapped spaces first: they are the truth where they exist
    spaces = [e for e in els if e["type"] == "way" and e["tags"].get("amenity") == "parking_space" and is_closed(e)]
    spaces.sort(key=lambda e: e["id"])
    for e in spaces:
        pts = way_points(e)[:-1]
        c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
        if math.dist(c, CENTRE) > RADIUS or len(pts) < 3 or in_building(c):
            continue
        best = max(zip(pts, pts[1:] + pts[:1]), key=lambda s: math.dist(s[0], s[1]))
        ang = math.degrees(math.atan2(best[1][1] - best[0][1], best[1][0] - best[0][0]))
        runs.append((c[0], c[1], 1.0, 0.0, 1, round(ang % 180.0, 0), 0))
        taken.add((c[0] - 1, c[1] - 1, c[0] + 1, c[1] + 1), c)

    for e in aisles:
        line = simplify(way_points(e), 0.4)
        mid = line[len(line) // 2]
        if math.dist(mid, CENTRE) > RADIUS or not in_park(mid):
            continue
        for side in (1.0, -1.0):
            for a, b in zip(line, line[1:]):
                seg = math.dist(a, b)
                if seg < BAY_PITCH:
                    continue
                dx, dz = (b[0] - a[0]) / seg, (b[1] - a[1]) / seg
                nx, nz = -dz * side, dx * side
                off = AISLE_HALF + BAY_DEPTH * 0.5
                n = int(seg // BAY_PITCH)
                start = (seg - n * BAY_PITCH) * 0.5
                current = None
                for j in range(n):
                    t = start + (j + 0.5) * BAY_PITCH
                    c = (a[0] + dx * t + nx * off, a[1] + dz * t + nz * off)
                    ok = (in_park(c) and not in_building(c) and free_bay(c)
                          and not near_other_road(c, e["id"], 3.4))
                    if ok:
                        taken.add((c[0] - 1, c[1] - 1, c[0] + 1, c[1] + 1), c)
                        if current is None:
                            current = [c[0], c[1], dx, dz, 0]
                        current[4] += 1
                    elif current is not None:
                        runs.append(finish(current, dx, dz))
                        current = None
                if current is not None:
                    runs.append(finish(current, dx, dz))

    lamps = []
    for e in els:
        if e["type"] == "node" and e["tags"].get("highway") == "street_lamp":
            p = to_local(e["lat"], e["lon"])
            if math.dist(p, CENTRE) <= RADIUS * 1.6:
                lamps.append(p)
    lamps.sort()
    return data, parks, runs, lamps


def finish(run, dx, dz):
    # the car axis is across the row (nose toward the aisle), stored as the bay row direction; runtime turns it 90 degrees
    ang = math.degrees(math.atan2(dz, dx)) % 180.0
    return (run[0], run[1], dx, dz, run[4], round(ang, 0), 1)


def f(v):
    return f"{v:.1f}f"


def wrap(items, per_line, indent):
    return "\n".join(" " * indent + ", ".join(items[i:i + per_line]) + "," for i in range(0, len(items), per_line))


def emit(snapshot, data, parks, runs, lamps):
    starts, pts = [], []
    for p in parks:
        starts.append(len(pts) // 2)
        for x, z in p["pts"]:
            pts.extend([f(x), f(z)])
    starts.append(len(pts) // 2)
    run_flat = []
    for x, z, dx, dz, n, ang, lines in runs:
        run_flat.extend([f(x), f(z), f"{dx:.3f}f", f"{dz:.3f}f", f"{n}f", f"{ang:.0f}f", f"{lines}f"])
    lamp_flat = []
    for x, z in lamps:
        lamp_flat.extend([f(x), f(z)])
    bays = sum(r[4] for r in runs)
    src = os.path.relpath(snapshot, ROOT)
    return f"""// GENERATED by scripts/generate-ypad-carparks.py — do not edit by hand.
// Source: {src}
// © OpenStreetMap contributors, ODbL. OSM base {data.get('retrieved', '')}.
// {len(parks)} car parks, {len(runs)} bay runs ({bays} bays), {len(lamps)} street lamps.

namespace Airside.Simulation
{{
    /// <summary>
    /// Adelaide Airport's surface car parks in the game's runway frame (metres), from OpenStreetMap: the
    /// car-park outlines, straight rows of 90-degree bays either side of each parking aisle (kept inside the
    /// car park, clear of roads and buildings), and street lamps. Which bays are occupied is decided at runtime
    /// by hash, so the picture is the same every run. No UnityEngine types (ADR 0184).
    /// </summary>
    public static class AdelaideCarParks
    {{
        public const string Attribution = "Car parks © OpenStreetMap contributors";
        public const float BayPitchMetres = {BAY_PITCH}f;
        public const float BayDepthMetres = {BAY_DEPTH}f;
        public const int RunStride = 7;

        /// <summary>Start index (in points) of each car-park outline in <see cref="Points"/>; one extra entry ends the last.</summary>
        public static readonly int[] PolygonStarts =
        {{
{wrap([str(s) for s in starts], 16, 12)}
        }};

        /// <summary>Car-park outline vertices as x, z pairs.</summary>
        public static readonly float[] Points =
        {{
{wrap(pts, 12, 12)}
        }};

        /// <summary>
        /// Bay runs, <see cref="RunStride"/> floats each: x, z of the first bay centre, unit direction along the run
        /// (x, z), bay count, angle of that direction in degrees (0..180), and 1 when the bays get painted lines
        /// (0 for a single mapped space). Bays are <see cref="BayPitchMetres"/> apart; a car sits across the run.
        /// </summary>
        public static readonly float[] Runs =
        {{
{wrap(run_flat, 7, 12)}
        }};

        /// <summary>Street lamps as x, z pairs.</summary>
        public static readonly float[] Lamps =
        {{
{wrap(lamp_flat, 12, 12)}
        }};

        public static int PolygonCount => PolygonStarts.Length - 1;
        public static int RunCount => Runs.Length / RunStride;
        public static int LampCount => Lamps.Length / 2;
    }}
}}
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    snapshot = args.snapshot or latest_snapshot()
    data, parks, runs, lamps = build(snapshot)
    text = emit(snapshot, data, parks, runs, lamps)
    if args.check:
        current = open(OUTPUT, encoding="utf-8").read() if os.path.exists(OUTPUT) else ""
        if current != text:
            sys.exit("AdelaideCarParks.cs is out of date: run scripts/generate-ypad-carparks.py")
        print("AdelaideCarParks.cs is up to date")
        return
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    bays = sum(r[4] for r in runs)
    print(f"wrote {os.path.relpath(OUTPUT, ROOT)}: {len(parks)} car parks, {len(runs)} runs, {bays} bays, "
          f"{len(lamps)} lamps, {len(text) / 1e3:.0f} KB")


if __name__ == "__main__":
    main()
