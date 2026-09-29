#!/usr/bin/env python3
"""Generate Adelaide Airport's precinct furniture for Airside (ADR 0184, map overhaul P2/P3).

Reads docs/data/osm/ypad-map-<date>.json (© OpenStreetMap contributors, ODbL) and writes
game/Airside/Assets/Airside/Simulation/AdelaidePrecinct.cs:

  Canopies  open roofs on posts (building=roof|carport): the taxi and bus ranks, car-park entrances
  Solar     photovoltaic arrays (power=generator, solar): the roof and ground arrays
  Tanks     fuel and water storage tanks (man_made=storage_tank), as circles
  Masts     apron floodlight masts and the navaid/communication masts
  BusStops  bus stops near the terminal, with their shelter flag and the heading of the road
  Paths     footpaths, sidewalks, cycleways and steps near the terminal

Standard library only.

Run: python3 scripts/generate-ypad-precinct.py            (writes)
     python3 scripts/generate-ypad-precinct.py --check    (fails if the file is stale)
"""
import argparse
import glob
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ypad_osm import ROOT, OSM_DIR, is_closed, load_snapshot, polyline_length, simplify, to_local, way_points  # noqa: E402

OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaidePrecinct.cs")
CENTRE = (1300.0, 500.0)
CANOPY_RADIUS = 4500.0
PATH_RADIUS = 2600.0
BUS_RADIUS = 2600.0
PATH_KINDS = {"footway": 0, "cycleway": 1, "path": 2, "pedestrian": 3, "steps": 4}
PATH_WIDTH = {0: 1.8, 1: 2.2, 2: 1.2, 3: 4.0, 4: 1.5}
CANOPY_HEIGHT = {"roof": 4.6, "carport": 2.7}


def latest_snapshot():
    files = sorted(glob.glob(os.path.join(OSM_DIR, "ypad-map-*.json")))
    if not files:
        sys.exit("no docs/data/osm/ypad-map-*.json — run scripts/fetch-ypad-osm.py first")
    return files[-1]


def area(pts):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(pts, pts[1:] + pts[:1]))) / 2.0


def number(text):
    try:
        return float(str(text).split()[0].replace(",", "."))
    except (ValueError, IndexError):
        return None


def build(snapshot):
    data = load_snapshot(os.path.basename(snapshot))
    els = data["elements"]

    canopies, solar, tanks, masts, paths, roads = [], [], [], [], [], []
    for e in els:
        t = e["tags"]
        if e["type"] == "way":
            if "highway" in t and t["highway"] not in PATH_KINDS and t["highway"] != "corridor":
                roads.append(e)
            if t.get("power") == "generator" and t.get("generator:source") == "solar" and is_closed(e):
                pts = way_points(e)[:-1]
                on_roof = t.get("building") == "roof" or t.get("location") in ("roof", "rooftop")
                solar.append((e["id"], pts, on_roof))
            if t.get("building") in CANOPY_HEIGHT and is_closed(e):
                pts = way_points(e)[:-1]
                if math.dist(pts[0], CENTRE) <= CANOPY_RADIUS and len(pts) >= 3 and area(pts) >= 12:
                    h = number(t.get("height")) or CANOPY_HEIGHT[t["building"]]
                    canopies.append((e["id"], simplify(pts + [pts[0]], 0.3)[:-1], min(h, 12.0)))
            if t.get("man_made") == "storage_tank" and is_closed(e):
                pts = way_points(e)[:-1]
                c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
                r = math.sqrt(area(pts) / math.pi)
                tanks.append((e["id"], c[0], c[1], r, 12.0 if r > 12 else 8.0))
            if t.get("highway") in PATH_KINDS and t.get("footway") != "crossing":
                pts = way_points(e)
                if math.dist(pts[0], CENTRE) <= PATH_RADIUS and polyline_length(pts) >= 4.0:
                    kind = PATH_KINDS[t["highway"]]
                    width = number(t.get("width")) or PATH_WIDTH[kind]
                    if t.get("footway") == "sidewalk":
                        width = min(width, 1.6)
                    paths.append((e["id"], kind, max(0.8, min(width, 6.0)), simplify(pts, 0.4)))
        elif e["type"] == "node":
            if t.get("man_made") == "mast":
                p = to_local(e["lat"], e["lon"])
                name = t.get("name", "")
                if t.get("tower:type") == "lighting":
                    kind, h = 0, number(t.get("height")) or 25.0
                elif "VOR" in name or "DME" in name:
                    kind, h = 2, 6.0
                elif "Localizer" in name or "Glide" in name:
                    kind, h = 2, 4.0
                else:
                    kind, h = 1, number(t.get("height")) or 30.0
                masts.append((e["id"], p[0], p[1], kind, h))

    # bus stops need the heading of the road they stand beside
    segs = []
    for e in roads:
        pts = simplify(way_points(e), 0.6)
        segs.extend((a, b) for a, b in zip(pts, pts[1:]))
    cell = 60.0
    grid = {}
    for a, b in segs:
        for ix in range(int(min(a[0], b[0]) // cell), int(max(a[0], b[0]) // cell) + 1):
            for iz in range(int(min(a[1], b[1]) // cell), int(max(a[1], b[1]) // cell) + 1):
                grid.setdefault((ix, iz), []).append((a, b))

    def nearest_heading(p):
        best, best_d = None, 25.0
        for a, b in grid.get((int(p[0] // cell), int(p[1] // cell)), ()):
            vx, vz = b[0] - a[0], b[1] - a[1]
            l2 = vx * vx + vz * vz
            if l2 == 0:
                continue
            t = max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vz) / l2))
            d = math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vz * t))
            if d < best_d:
                best, best_d = math.degrees(math.atan2(vz, vx)) % 180.0, d
        return best

    bus = []
    for e in els:
        if e["type"] == "node" and e["tags"].get("highway") == "bus_stop":
            p = to_local(e["lat"], e["lon"])
            if math.dist(p, CENTRE) <= BUS_RADIUS:
                yaw = nearest_heading(p)
                bus.append((e["id"], p[0], p[1], 0.0 if yaw is None else yaw, 1 if e["tags"].get("shelter") == "yes" else 0))

    canopies.sort()
    solar.sort(key=lambda s: s[0])
    tanks.sort()
    masts.sort()
    bus.sort()
    paths.sort(key=lambda p: p[0])
    return data, canopies, solar, tanks, masts, bus, paths


def f(v):
    return f"{v:.1f}f"


def wrap(items, per_line, indent):
    return "\n".join(" " * indent + ", ".join(items[i:i + per_line]) + "," for i in range(0, len(items), per_line))


def polys(items, height_of=None):
    """Packed polygons: starts (points), flat x,z, and one value per polygon."""
    starts, pts, vals = [], [], []
    for it in items:
        starts.append(len(pts) // 2)
        for x, z in it["pts"]:
            pts.extend([f(x), f(z)])
        vals.append(f(it["v"]))
    starts.append(len(pts) // 2)
    return starts, pts, vals


def emit(snapshot, data, canopies, solar, tanks, masts, bus, paths):
    c_starts, c_pts, c_vals = polys([{"pts": p, "v": h} for _, p, h in canopies])
    s_starts, s_pts, s_vals = polys([{"pts": p, "v": 1.0 if roof else 0.0} for _, p, roof in solar])
    tank_flat = []
    for _, x, z, r, h in tanks:
        tank_flat.extend([f(x), f(z), f(r), f(h)])
    mast_flat = []
    for _, x, z, kind, h in masts:
        mast_flat.extend([f(x), f(z), f"{kind}f", f(h)])
    bus_flat = []
    for _, x, z, yaw, shelter in bus:
        bus_flat.extend([f(x), f(z), f"{yaw:.0f}f", f"{shelter}f"])
    p_starts, p_pts, p_info = [], [], []
    for _, kind, width, pts in paths:
        p_starts.append(len(p_pts) // 2)
        p_info.extend([f"{kind}f", f(width)])
        for x, z in pts:
            p_pts.extend([f(x), f(z)])
    p_starts.append(len(p_pts) // 2)
    src = os.path.relpath(snapshot, ROOT)
    km = sum(polyline_length(p) for _, _, _, p in paths) / 1000.0
    return f"""// GENERATED by scripts/generate-ypad-precinct.py — do not edit by hand.
// Source: {src}
// © OpenStreetMap contributors, ODbL. OSM base {data.get('retrieved', '')}.
// {len(canopies)} canopies, {len(solar)} solar arrays, {len(tanks)} tanks, {len(masts)} masts, {len(bus)} bus stops, {len(paths)} paths ({km:.0f} km).

namespace Airside.Simulation
{{
    /// <summary>
    /// Adelaide Airport's precinct furniture in the game's runway frame (metres), from OpenStreetMap: open canopies,
    /// solar arrays, storage tanks, floodlight and navaid masts, bus stops and footpaths. No UnityEngine types (ADR 0184).
    /// </summary>
    public static class AdelaidePrecinct
    {{
        public const string Attribution = "Precinct furniture © OpenStreetMap contributors";

        /// <summary>Start (in points) of each canopy in <see cref="CanopyPoints"/>; one extra entry ends the last.</summary>
        public static readonly int[] CanopyStarts =
        {{
{wrap([str(s) for s in c_starts], 16, 12)}
        }};
        public static readonly float[] CanopyPoints =
        {{
{wrap(c_pts, 12, 12)}
        }};
        /// <summary>Roof height above the ground, metres, per canopy.</summary>
        public static readonly float[] CanopyHeights =
        {{
{wrap(c_vals, 12, 12)}
        }};

        public static readonly int[] SolarStarts =
        {{
{wrap([str(s) for s in s_starts], 16, 12)}
        }};
        public static readonly float[] SolarPoints =
        {{
{wrap(s_pts, 12, 12)}
        }};
        /// <summary>1 when the array is on a roof (canopy), 0 when it stands on the ground.</summary>
        public static readonly float[] SolarOnRoof =
        {{
{wrap(s_vals, 12, 12)}
        }};

        /// <summary>Tanks as x, z, radius, height.</summary>
        public static readonly float[] Tanks =
        {{
{wrap(tank_flat, 12, 12)}
        }};

        /// <summary>Masts as x, z, kind (0 floodlight, 1 communication, 2 navaid), height.</summary>
        public static readonly float[] Masts =
        {{
{wrap(mast_flat, 12, 12)}
        }};

        /// <summary>Bus stops as x, z, heading of the road beside (degrees, 0..180), 1 when there is a shelter.</summary>
        public static readonly float[] BusStops =
        {{
{wrap(bus_flat, 12, 12)}
        }};

        public static readonly int[] PathStarts =
        {{
{wrap([str(s) for s in p_starts], 16, 12)}
        }};
        public static readonly float[] PathPoints =
        {{
{wrap(p_pts, 12, 12)}
        }};
        /// <summary>Per path: kind (0 footway, 1 cycleway, 2 path, 3 pedestrian area, 4 steps) and width in metres.</summary>
        public static readonly float[] PathInfo =
        {{
{wrap(p_info, 12, 12)}
        }};

        public static int CanopyCount => CanopyStarts.Length - 1;
        public static int SolarCount => SolarStarts.Length - 1;
        public static int TankCount => Tanks.Length / 4;
        public static int MastCount => Masts.Length / 4;
        public static int BusStopCount => BusStops.Length / 4;
        public static int PathCount => PathStarts.Length - 1;
    }}
}}
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    snapshot = args.snapshot or latest_snapshot()
    parts = build(snapshot)
    text = emit(snapshot, *parts)
    if args.check:
        current = open(OUTPUT, encoding="utf-8").read() if os.path.exists(OUTPUT) else ""
        if current != text:
            sys.exit("AdelaidePrecinct.cs is out of date: run scripts/generate-ypad-precinct.py")
        print("AdelaidePrecinct.cs is up to date")
        return
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    _, canopies, solar, tanks, masts, bus, paths = parts
    print(f"wrote {os.path.relpath(OUTPUT, ROOT)}: {len(canopies)} canopies, {len(solar)} solar, {len(tanks)} tanks, "
          f"{len(masts)} masts, {len(bus)} bus stops, {len(paths)} paths, {len(text) / 1e3:.0f} KB")


if __name__ == "__main__":
    main()
