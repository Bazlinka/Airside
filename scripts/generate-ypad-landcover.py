#!/usr/bin/env python3
"""Generate Adelaide Airport (YPAD) land-cover + arterial roads for Airside.

Reads docs/data/osm/ypad-landcover-2026-09-15.json (© OpenStreetMap contributors, ODbL)
and writes game/Airside/Assets/Airside/Simulation/AdelaideLandCover.cs.

Uses the runway frame from scripts/generate-ypad-layout.py.

Run: python3 scripts/generate-ypad-landcover.py
"""
from __future__ import annotations

import base64
import importlib.util
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "docs/data/osm/ypad-landcover-2026-09-15.json")
OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideLandCover.cs")

spec = importlib.util.spec_from_file_location(
    "ypad_layout", os.path.join(ROOT, "scripts/generate-ypad-layout.py")
)
layout = importlib.util.module_from_spec(spec)
spec.loader.exec_module(layout)

HALF_EXTENT = 6500.0
CELL = 50.0
SIZE = int(round(2 * HALF_EXTENT / CELL))  # 260
ORIGIN = -HALF_EXTENT
CORE_HALF_X = 1600.0
CORE_HALF_Z = 550.0

NONE, RESIDENTIAL, COMMERCIAL, PARK, PARKING, WATER, SAND, SCRUB = range(8)
PRIORITY = {
    NONE: 0, SCRUB: 1, RESIDENTIAL: 2, SAND: 3, COMMERCIAL: 4,
    PARK: 5, PARKING: 6, WATER: 7,
}
CLASS_NAME = {
    NONE: "None", RESIDENTIAL: "Residential", COMMERCIAL: "Commercial",
    PARK: "Park", PARKING: "Parking", WATER: "Water", SAND: "Sand", SCRUB: "Scrub",
}
KEEP_HIGHWAY = {
    "motorway", "trunk", "primary", "secondary",
    "motorway_link", "trunk_link", "primary_link", "secondary_link",
}
ROAD_WIDTH = {
    "motorway": 18.0, "motorway_link": 10.0,
    "trunk": 14.0, "trunk_link": 9.0,
    "primary": 12.0, "primary_link": 8.0,
    "secondary": 10.0, "secondary_link": 7.0,
}


def classify(tags):
    if tags.get("natural") in ("water", "wetland") or tags.get("water"):
        return WATER
    if tags.get("natural") in ("beach", "sand"):
        return SAND
    if tags.get("amenity") == "parking":
        return PARKING
    leisure = tags.get("leisure")
    if leisure in ("park", "golf_course", "nature_reserve", "recreation_ground", "pitch"):
        return PARK
    landuse = tags.get("landuse")
    if landuse in (
        "recreation_ground", "recreation_grounds", "forest", "village_green",
        "cemetery", "allotments", "flowerbed", "orchard", "vineyard",
        "meadow", "grass", "greenfield",
    ):
        return PARK
    if landuse == "residential":
        return RESIDENTIAL
    if landuse in (
        "commercial", "retail", "industrial", "construction", "brownfield",
        "railway", "garages", "depot",
    ):
        return COMMERCIAL
    if tags.get("natural") in ("scrub", "heath", "grassland"):
        return SCRUB
    return None


def to_local(geom):
    return [tuple(map(float, layout.local({"lat": p["lat"], "lon": p["lon"]}))) for p in geom]


def simplify(pts, tol):
    if len(pts) < 3:
        return pts
    ax, az = pts[0]
    bx, bz = pts[-1]
    dx, dz = bx - ax, bz - az
    length = math.hypot(dx, dz) or 1e-9
    worst, index = -1.0, 0
    for i in range(1, len(pts) - 1):
        px, pz = pts[i]
        d = abs((px - ax) * dz - (pz - az) * dx) / length
        if d > worst:
            worst, index = d, i
    if worst <= tol:
        return [pts[0], pts[-1]]
    return simplify(pts[: index + 1], tol)[:-1] + simplify(pts[index:], tol)


def close_ring(pts):
    if len(pts) < 3:
        return pts
    if math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) > 0.5:
        return pts + [pts[0]]
    return pts


def paint_polygon(grid, ring, kind):
    if len(ring) < 4:
        return 0
    xs = [p[0] for p in ring]
    zs = [p[1] for p in ring]
    min_x, max_x = min(xs), max(xs)
    min_z, max_z = min(zs), max(zs)
    if max_x < ORIGIN or min_x > ORIGIN + SIZE * CELL or max_z < ORIGIN or min_z > ORIGIN + SIZE * CELL:
        return 0
    zi0 = max(0, int(math.floor((min_z - ORIGIN) / CELL)))
    zi1 = min(SIZE - 1, int(math.floor((max_z - ORIGIN) / CELL)))
    painted = 0
    pri = PRIORITY[kind]
    n = len(ring) - 1
    for zi in range(zi0, zi1 + 1):
        z = ORIGIN + (zi + 0.5) * CELL
        crossings = []
        for i in range(n):
            x0, z0 = ring[i]
            x1, z1 = ring[i + 1]
            if z0 == z1:
                continue
            if (z0 <= z < z1) or (z1 <= z < z0):
                t = (z - z0) / (z1 - z0)
                crossings.append(x0 + t * (x1 - x0))
        if not crossings:
            continue
        crossings.sort()
        for a, b in zip(crossings[0::2], crossings[1::2]):
            xi0 = max(0, int(math.floor((a - ORIGIN) / CELL)))
            xi1 = min(SIZE - 1, int(math.floor((b - ORIGIN) / CELL)))
            for xi in range(xi0, xi1 + 1):
                x = ORIGIN + (xi + 0.5) * CELL
                if abs(x) < CORE_HALF_X and abs(z) < CORE_HALF_Z:
                    continue
                idx = zi * SIZE + xi
                if PRIORITY[grid[idx]] < pri:
                    grid[idx] = kind
                    painted += 1
    return painted


def open_ring(pts):
    """Drop a duplicate closing vertex so simplify is not fed a zero-length baseline."""
    if len(pts) >= 2 and math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]) < 0.5:
        return pts[:-1]
    return pts


def polygon_rings(e):
    if e["type"] == "way":
        geom = e.get("geometry") or []
        if len(geom) < 3:
            return []
        ring = close_ring(simplify(open_ring(to_local(geom)), 8.0))
        return [ring] if len(ring) >= 4 else []
    if e["type"] == "relation":
        rings = []
        for m in e.get("members") or []:
            if (m.get("role") or "outer") != "outer":
                continue
            geom = m.get("geometry") or []
            if len(geom) < 3:
                continue
            ring = close_ring(simplify(open_ring(to_local(geom)), 8.0))
            if len(ring) >= 4:
                rings.append(ring)
        return rings
    return []


def road_points(e):
    tags = e.get("tags") or {}
    hw = tags.get("highway")
    if hw not in KEEP_HIGHWAY:
        return None
    geom = e.get("geometry") or []
    if len(geom) < 2:
        return None
    pts = simplify(to_local(geom), 12.0)
    if not any(ORIGIN <= x <= ORIGIN + SIZE * CELL and ORIGIN <= z <= ORIGIN + SIZE * CELL for x, z in pts):
        return None
    return ROAD_WIDTH[hw], pts


def format_roads(roads):
    parts = []
    for width, pts in roads:
        parts.append(f"{width:.1f}f")
        parts.append(f"{len(pts)}")
        for x, z in pts:
            parts.append(f"{x:.1f}f")
            parts.append(f"{z:.1f}f")
    parts.append("0f")
    lines = []
    row = []
    for p in parts:
        row.append(p)
        if len(row) >= 10:
            lines.append("            " + ", ".join(row) + ",")
            row = []
    if row:
        lines.append("            " + ", ".join(row) + ",")
    return "\n".join(lines)


def sample_kind(grid, x, z):
    if x < ORIGIN or z < ORIGIN:
        return NONE
    xi = int(math.floor((x - ORIGIN) / CELL))
    zi = int(math.floor((z - ORIGIN) / CELL))
    if xi < 0 or zi < 0 or xi >= SIZE or zi >= SIZE:
        return NONE
    return grid[zi * SIZE + xi]


def main():
    with open(SOURCE) as f:
        data = json.load(f)

    grid = bytearray(SIZE * SIZE)
    painted = {k: 0 for k in range(8)}

    jobs = []
    for e in data["elements"]:
        tags = e.get("tags") or {}
        if "highway" in tags:
            continue
        kind = classify(tags)
        if kind is None:
            continue
        for ring in polygon_rings(e):
            jobs.append((PRIORITY[kind], kind, ring))
    jobs.sort(key=lambda j: j[0])

    for _, kind, ring in jobs:
        n = paint_polygon(grid, ring, kind)
        if n:
            painted[kind] += n

    roads = []
    for e in data["elements"]:
        road = road_points(e)
        if road:
            roads.append(road)
    roads.sort(
        key=lambda r: -sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(r[1], r[1][1:]))
    )
    total_road_pts = sum(len(p) for _, p in roads)

    cells_b64 = base64.b64encode(bytes(grid)).decode("ascii")
    chunks = [cells_b64[i:i + 100] for i in range(0, len(cells_b64), 100)]
    b64_lines = "\n".join(
        ('            "' + chunk + '"' + (" +" if idx < len(chunks) - 1 else ""))
        for idx, chunk in enumerate(chunks)
    )
    osm_base = (data.get("osm3s") or {}).get("timestamp_osm_base", "unknown")
    coverage = {CLASS_NAME[k]: round(100.0 * painted[k] / (SIZE * SIZE), 2) for k in range(1, 8)}

    # Centroids measured from the named OSM polygons in runway-frame metres.
    landmarks = {
        "RoyalAdelaideGolf": (2433.2, 5789.2),
        "Patawalonga": (-2450.2, -543.5),
        "HarbourTown": (-549.7, 883.0),
        "GlenelgGolf": (-839.0, -924.0),
    }
    landmark_kinds = {
        name: CLASS_NAME[sample_kind(grid, x, z)] for name, (x, z) in landmarks.items()
    }

    out = f"""// <auto-generated>
// Generated by scripts/generate-ypad-landcover.py from docs/data/osm/ypad-landcover-2026-09-15.json
// (OSM base {osm_base}). Do not edit by hand — change the script and re-run it.
// Map data © OpenStreetMap contributors, available under the ODbL.
// </auto-generated>
using System;

namespace Airside.Simulation
{{
    /// <summary>
    /// Stylised land cover and arterial roads around Adelaide Airport in the runway
    /// frame (metres): a {SIZE}×{SIZE} class grid at {CELL:g} m, covering ±{HALF_EXTENT:g} m,
    /// painted from real OSM landuse / leisure / natural / parking polygons, plus
    /// motorway–secondary centreline ribbons. Presentation tints the surroundings
    /// heightfield and draws road meshes from this data. Fail-soft: an empty grid
    /// simply leaves the noise-based plain.
    /// </summary>
    public static class AdelaideLandCover
    {{
        public const string Attribution = "Land cover © OpenStreetMap contributors (ODbL)";

        public enum Kind : byte
        {{
            None = 0,
            Residential = 1,
            Commercial = 2,
            Park = 3,
            Parking = 4,
            Water = 5,
            Sand = 6,
            Scrub = 7,
        }}

        public const float HalfExtentMetres = {HALF_EXTENT:g}f;
        public const float CellMetres = {CELL:g}f;
        public const int Size = {SIZE};
        public const float OriginMetres = {ORIGIN:g}f;
        public const float CoreHalfXMetres = {CORE_HALF_X:g}f;
        public const float CoreHalfZMetres = {CORE_HALF_Z:g}f;

        /// <summary>Packed row-major class bytes (z major), Base64. Length = Size*Size.</summary>
        private const string CellsBase64 =
{b64_lines};

        private static readonly byte[] Cells = Convert.FromBase64String(CellsBase64);

        /// <summary>
        /// Arterial roads packed as: width, pointCount, x,z,… repeated; terminated by a 0 width.
        /// {len(roads)} roads, {total_road_pts} points.
        /// </summary>
        public static readonly float[] Roads =
        {{
{format_roads(roads)}
        }};

        public const float CoverageResidentialPercent = {coverage['Residential']}f;
        public const float CoverageCommercialPercent = {coverage['Commercial']}f;
        public const float CoverageParkPercent = {coverage['Park']}f;
        public const float CoverageParkingPercent = {coverage['Parking']}f;
        public const float CoverageWaterPercent = {coverage['Water']}f;
        public const float CoverageSandPercent = {coverage['Sand']}f;
        public const float CoverageScrubPercent = {coverage['Scrub']}f;

        public static Kind Sample(float x, float z)
        {{
            if (Cells == null || Cells.Length != Size * Size)
                return Kind.None;
            var xi = (int)Math.Floor((x - OriginMetres) / CellMetres);
            var zi = (int)Math.Floor((z - OriginMetres) / CellMetres);
            if ((uint)xi >= Size || (uint)zi >= Size)
                return Kind.None;
            return (Kind)Cells[zi * Size + xi];
        }}

        public static bool InOperationalCore(float x, float z) =>
            Math.Abs(x) < CoreHalfXMetres && Math.Abs(z) < CoreHalfZMetres;
    }}
}}
"""
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    with open(OUTPUT, "w") as f:
        f.write(out)

    print(
        f"landcover: {SIZE}x{SIZE} @ {CELL:g}m; "
        + ", ".join(f"{CLASS_NAME[k]}={painted[k]}" for k in range(1, 8))
        + f"; roads={len(roads)} ({total_road_pts} pts)"
    )
    print("landmarks:", landmark_kinds)
    print("coverage%:", coverage)
    print("wrote", os.path.relpath(OUTPUT, ROOT))

    expect = {
        "RoyalAdelaideGolf": {"Park"},
        "Patawalonga": {"Water"},
        "HarbourTown": {"Commercial", "Parking"},
        "GlenelgGolf": {"Park"},
    }
    failed = False
    for name, want in expect.items():
        got = landmark_kinds.get(name)
        if got not in want:
            print(f"ERROR: {name} sampled as {got}, expected one of {sorted(want)}", file=sys.stderr)
            x, z = landmarks[name]
            print(f"  at ({x:.0f},{z:.0f})", file=sys.stderr)
            failed = True
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
