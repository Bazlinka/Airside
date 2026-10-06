#!/usr/bin/env python3
"""Bake the buildings around Adelaide Airport from OpenStreetMap (plan P5, ADR 0159).

Every OSM building within BAND_METRES outside the airfield rectangle, plus those inside the
rectangle but outside the airport boundary (its corners hold real streets). The airport's own
buildings stay with AdelaideBuildings / the terminal models. Houses become a box with a hipped
roof on their minimum-area rectangle; everything else is its simplified footprint extruded to
its tagged height (or a default for its type) with a flat roof.

OSM's house footprints here are patchy: whole blocks are mapped, the next is bare, while the
satellite shows continuous housing. The street network is complete, so bare residential frontage
is filled with typical Adelaide houses: set back from the kerb, facing the street, never on a road,
another building, or land that is not residential. They are flagged (kind 2) so they can be
counted and told apart from surveyed buildings.

Input (fetched once into work/cache/osm/, then committed as a filtered snapshot):
  way["building"] over the band, out tags geom; the aerodrome way 146489105.
Output: game/Airside/Assets/Airside/Art/Terrain/osm_adelaide_suburbs_v01.bin, little-endian:
  char[4] "ASUB", int32 version (1), int32 count, then per building:
    byte   kind (0 flat prism, 1 hipped OSM house, 2 hipped street-front filler house)
    byte   wall palette index (AirsideAdelaideSuburbs.WallColours)
    byte   roof palette index (AirsideAdelaideSuburbs.RoofColours)
    float  wall height, float roof rise (0 for flat)
    kind 1/2: float centre x, centre z, half length, half width, angle (radians, length axis from +x)
    kind 0: byte n, n x (float x, float z) counter-clockwise, byte t, t x 3 byte indices (roof)
Coordinates are metres in the runway frame (generate-ypad-layout.py).

Run: python3 scripts/generate-adelaide-suburbs.py [--fetch]
Needs shapely.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import struct
import urllib.parse
import urllib.request
from pathlib import Path

from shapely.geometry import Polygon
from shapely.geometry.polygon import orient

ROOT = Path(__file__).resolve().parents[1]
LAYOUT_PATH = ROOT / "scripts/generate-ypad-layout.py"
SNAPSHOT = sorted((ROOT / "docs/data/osm").glob("adelaide-suburb-buildings-*.json"))[-1]   # newest fetch (fetch-ypad-osm.py --band)
RAW = ROOT / "work/cache/osm/buildings-raw.json"
STREETS_RAW = ROOT / "work/cache/osm/streets-raw.json"
STREETS_SNAPSHOT = sorted((ROOT / "docs/data/osm").glob("adelaide-suburb-streets-*.json"))[-1]
LANDCOVER_CS = ROOT / "game/Airside/Assets/Airside/Simulation/AdelaideLandCover.cs"
AERODROME_RAW = ROOT / "work/cache/osm/aerodrome.json"
OUTPUT = ROOT / "game/Airside/Assets/Airside/Art/Terrain/osm_adelaide_suburbs_v01.bin"
BOUNDARY_SNAPSHOT = sorted((ROOT / "docs/data/osm").glob("ypad-boundary-*.json"))[-1]
OVERTURE_SNAPSHOTS = sorted((ROOT / "docs/data/overture").glob("ypad-suburb-buildings-*.json.gz"))
OVERTURE_DUPLICATE = 0.3   # share of the smaller footprint that must overlap an OSM one to count as the same building
OVERPASS = "https://maps.mail.ru/osm/tools/overpass/api/interpreter"
BBOX = "-35.005,138.462,-34.894,138.598"

AIRFIELD_HALF_X = 1950.0   # AirsideBareField.GroundLengthMetres / 2
AIRFIELD_HALF_Z = 1400.0   # AirsideBareField.GroundWidthMetres / 2
BAND_METRES = 2000.0
SIMPLIFY_METRES = 0.6
MAX_VERTICES = 12

HOUSE_TAGS = {"house", "detached", "semidetached_house", "terrace", "bungalow", "residential"}
SMALL_TAGS = {"garage", "garages", "carport", "shed", "hut", "roof", "kiosk"}

# Must match AirsideAdelaideSuburbs.RoofColours: terracotta tile, charcoal and pale steel,
# grey concrete tile, galvanised.
TERRACOTTA, CHARCOAL, PALE_STEEL, GREY_TILE, GALVANISED = range(5)
HOUSE_ROOFS = [TERRACOTTA, TERRACOTTA, CHARCOAL, GREY_TILE, PALE_STEEL]
OTHER_ROOFS = [GALVANISED, PALE_STEEL, GREY_TILE, CHARCOAL]

# Street-front filling.
FRONTING = {"residential": 5.0, "living_street": 4.0, "unclassified": 5.5, "tertiary": 7.0, "secondary": 8.5}
CLEARANCE = {"residential": 5.0, "living_street": 4.0, "unclassified": 5.5, "tertiary": 7.0,
             "tertiary_link": 5.0, "secondary": 8.5, "secondary_link": 5.5, "primary": 10.0,
             "primary_link": 6.0, "trunk": 12.0, "trunk_link": 6.0, "service": 3.0}
FILL_BAND_METRES = 1800.0
# Buildings thin out between these distances outside the airfield, so the extruded suburb
# fades into the flat imagery instead of stopping at a line.
TAPER_START_METRES = 1100.0
TAPER_END_METRES = 1800.0
# The airport's own modelled buildings, terminals and landside precinct keep their ground.
AIRPORT_CLEARANCE_METRES = 25.0
AIRPORT_BUILDINGS_CS = ROOT / "game/Airside/Assets/Airside/Simulation/AdelaideBuildings.cs"
LAYOUT_CS = ROOT / "game/Airside/Assets/Airside/Simulation/AdelaideLayout.cs"
LANDSIDE_PRECINCT = (750.0, 430.0, 1750.0, 920.0)   # AdelaideLandside.Precinct*
LOT_WIDTH = 17.0
FRONT_SETBACK = 6.0
RESIDENTIAL = 1

# Must match AirsideAdelaideSuburbs.WallColours.
CREAM, BRICK, BLUESTONE, WHITE, PALE_GREY, WARM_GREY, BLUE_GREY = range(7)
HOUSE_WALLS = [CREAM, CREAM, BRICK, BRICK, BLUESTONE, WHITE]
OTHER_WALLS = [PALE_GREY, WARM_GREY, BLUE_GREY, CREAM, WHITE]


def load_layout():
    spec = importlib.util.spec_from_file_location("ypad_layout", LAYOUT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def fetch():
    queries = {
        RAW: f'[out:json][timeout:300][bbox:{BBOX}];(way["building"];);out tags geom;',
        AERODROME_RAW: '[out:json][timeout:120];way(146489105);out tags geom;',
        STREETS_RAW: f'[out:json][timeout:300][bbox:{BBOX}];(way["highway"~"^(residential|living_street|'
                     'unclassified|tertiary|tertiary_link|secondary|secondary_link|primary|primary_link|trunk|'
                     'trunk_link|service)$"];);out tags geom;',
    }
    for path, query in queries.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        body = urllib.parse.urlencode({"data": query}).encode()
        request = urllib.request.Request(OVERPASS, data=body, headers={"User-Agent": "Airside-game-data/1.0"})
        with urllib.request.urlopen(request, timeout=900) as response:
            path.write_bytes(response.read())


def local(layout, lon, lat):
    east = (lon - layout.LON0) * 111_320.0 * math.cos(math.radians(layout.LAT0)) - layout.MID[0]
    north = (lat - layout.LAT0) * 110_574.0 - layout.MID[1]
    return east * layout.U[0] + north * layout.U[1], east * layout.N[0] + north * layout.N[1]


def outside_airfield(x, z):
    return math.hypot(max(0.0, abs(x) - AIRFIELD_HALF_X), max(0.0, abs(z) - AIRFIELD_HALF_Z))


def number(text):
    try:
        return float(str(text).split()[0].replace(",", "."))
    except (ValueError, IndexError):
        return None


def stable(osm_id, n):
    return int(hashlib.sha1(str(osm_id).encode()).hexdigest()[:8], 16) % n


def heights(tags, area, is_house):
    """(wall height, roof rise) in metres."""
    levels = number(tags.get("building:levels"))
    height = number(tags.get("height"))
    kind = tags.get("building", "yes")
    if is_house:
        storeys = max(1, int(levels)) if levels else 1
        wall = 2.9 * storeys
        rise = 1.5 + min(1.0, area / 400.0)
        if height and height > wall + 0.5:
            rise = min(3.5, max(0.8, height - wall))
        return wall, rise
    if height:
        return max(2.2, height), 0.0
    if levels:
        return 3.3 * levels + 0.8, 0.0
    if kind in SMALL_TAGS or area < 40:
        return 2.6, 0.0
    if kind in ("apartments", "hotel"):
        return 10.0, 0.0
    if kind in ("church", "cathedral"):
        return 9.0, 0.0
    if kind in ("industrial", "warehouse", "hangar", "factory"):
        return 8.0, 0.0
    if kind in ("commercial", "retail", "office", "supermarket", "school", "university", "public", "hospital"):
        return 6.5, 0.0
    return (4.0 if area < 400 else 6.0 if area < 2000 else 8.0), 0.0


def ear_clip(points):
    """Triangles over a simple counter-clockwise polygon (indices)."""
    idx = list(range(len(points)))
    tris = []

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        return cross(a, b, p) >= 0 and cross(b, c, p) >= 0 and cross(c, a, p) >= 0

    guard = 0
    while len(idx) > 3 and guard < 1000:
        guard += 1
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = points[i0], points[i1], points[i2]
            if cross(a, b, c) <= 1e-9:
                continue
            if any(inside(points[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            return None
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def airport_keep_out():
    """The airport's modelled footprints (AdelaideBuildings, terminals) and the landside precinct."""
    import re
    from shapely.geometry import box
    from shapely.ops import unary_union
    shapes = []
    for path, pattern in ((AIRPORT_BUILDINGS_CS, r"new AdelaideBuilding\([^\n]*?new\[\] \{([^}]*)\}"),
                          (LAYOUT_CS, r"new AdelaideOutline\([^\n]*?new float\[\] \{([^}]*)\}")):
        for match in re.finditer(pattern, path.read_text()):
            v = [float(t.strip().rstrip("f")) for t in match.group(1).split(",") if t.strip()]
            if len(v) >= 6:
                shapes.append(Polygon(list(zip(v[0::2], v[1::2]))).buffer(AIRPORT_CLEARANCE_METRES))
    shapes.append(box(*LANDSIDE_PRECINCT))
    return unary_union(shapes)


def keep_at(osm_id, x, z):
    """Deterministic thinning across the taper band (1 inside it, 0 beyond)."""
    out = outside_airfield(x, z)
    if out <= TAPER_START_METRES:
        return True
    if out >= TAPER_END_METRES:
        return False
    t = (out - TAPER_START_METRES) / (TAPER_END_METRES - TAPER_START_METRES)
    keep = 1.0 - t * t * (3.0 - 2.0 * t)
    return stable(f"taper:{osm_id}", 1000) / 1000.0 < keep


def landcover_grid():
    """AdelaideLandCover's 50 m class grid, decoded from its generated C#."""
    import base64
    import re
    text = LANDCOVER_CS.read_text()
    start = text.index("CellsBase64 =")
    end = text.index(";", start)
    data = base64.b64decode("".join(re.findall(r'"([A-Za-z0-9+/=]*)"', text[start:end])))
    size, origin, cell = 260, -6500.0, 50.0

    def sample(x, z):
        xi = int(math.floor((x - origin) / cell))
        zi = int(math.floor((z - origin) / cell))
        if 0 <= xi < size and 0 <= zi < size:
            return data[zi * size + xi]
        return 0
    return sample


def fill_frontage(layout, streets, buildings, boundary, landcover):
    """Houses along residential frontage OSM left bare. Deterministic."""
    from shapely.geometry import LineString
    from shapely.strtree import STRtree

    roads, clear, fronting = [], [], []
    for way in streets:
        kind = way["tags"].get("highway")
        pts = [local(layout, lon, lat) for lon, lat in way["geometry"]]
        if len(pts) < 2 or kind not in CLEARANCE:
            continue
        roads.append(LineString(pts))
        clear.append(CLEARANCE[kind])
        if kind in FRONTING:
            fronting.append((way["id"], pts, FRONTING[kind]))
    road_tree = STRtree(roads)
    building_tree = STRtree(buildings)
    cell = {}

    def near_placed(poly):
        cx, cz = poly.centroid.x, poly.centroid.y
        key = (int(cx // 40), int(cz // 40))
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for other in cell.get((key[0] + dx, key[1] + dz), ()):
                    if poly.distance(other) < 1.5:
                        return True
        return False

    houses = []
    for way_id, pts, half_road in fronting:
        for a, b in zip(pts, pts[1:]):
            dx, dz = b[0] - a[0], b[1] - a[1]
            length = math.hypot(dx, dz)
            if length < LOT_WIDTH:
                continue
            ux, uz = dx / length, dz / length
            nx, nz = -uz, ux
            steps = int(length // LOT_WIDTH)
            for k in range(steps):
                t = (k + 0.5) * length / steps
                for side in (-1.0, 1.0):
                    h = stable(f"{way_id}:{a}:{k}:{side}", 1000) / 1000.0
                    width = 10.0 + 3.5 * h
                    depth = 13.0 + 4.0 * ((h * 7.3) % 1.0)
                    along = t + (h - 0.5) * 2.5
                    offset = half_road + FRONT_SETBACK + depth / 2
                    cx = a[0] + ux * along + nx * side * offset
                    cz = a[1] + uz * along + nz * side * offset
                    if outside_airfield(cx, cz) > FILL_BAND_METRES or landcover(cx, cz) != RESIDENTIAL:
                        continue
                    corners = [(cx + ux * sx * width / 2 + nx * sz * depth / 2,
                                cz + uz * sx * width / 2 + nz * sz * depth / 2)
                               for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
                    poly = Polygon(corners)
                    if boundary.contains(poly.centroid):
                        continue
                    if any(poly.distance(roads[i]) < clear[i] + 1.0
                           for i in road_tree.query(poly, predicate="dwithin", distance=14.0)):
                        continue
                    if len(building_tree.query(poly, predicate="dwithin", distance=2.0)) > 0:
                        continue
                    if near_placed(poly):
                        continue
                    cell.setdefault((int(cx // 40), int(cz // 40)), []).append(poly)
                    long_axis = (ux, uz) if width >= depth else (nx, nz)
                    houses.append((f"fill:{way_id}:{k}:{side}", cx, cz, max(width, depth) / 2,
                                   min(width, depth) / 2, math.atan2(long_axis[1], long_axis[0])))
    return houses


def merge_overture(layout, snapshot):
    """Add Overture/Microsoft footprints OSM does not already have (ADR 0236).

    OSM maps a fraction of these suburbs; Overture's Microsoft ML footprints, validated against the SA Government
    LiDAR building raster by prepare-ypad-overture-buildings.py, cover nearly every roof. A footprint that
    overlaps an OSM building by more than OVERTURE_DUPLICATE of the smaller one is the same building and is
    dropped, so OSM tags keep winning. Returns how many were added.
    """
    import gzip
    from shapely.strtree import STRtree
    if not OVERTURE_SNAPSHOTS:
        return 0
    data = json.loads(gzip.open(OVERTURE_SNAPSHOTS[-1], "rt").read())
    existing = []
    for b in snapshot["buildings"]:
        pts = [local(layout, lon, lat) for lon, lat in b["geometry"]]
        if len(pts) >= 4:
            poly = Polygon(pts)
            existing.append(poly if poly.is_valid else poly.buffer(0))
    tree = STRtree(existing)
    # Overture carries the airport's own sheds and hangars; those stay with AdelaideBuildings and the terminals.
    aerodrome = next(e for e in json.loads(BOUNDARY_SNAPSHOT.read_text())["elements"] if e["id"] == 146489105)
    boundary = Polygon([local(layout, p["lon"], p["lat"]) for p in aerodrome["geometry"]]).buffer(5.0)
    added = 0
    for b in data["buildings"]:
        pts = [local(layout, lon, lat) for lon, lat in b["g"]]
        if len(pts) < 4:
            continue
        poly = Polygon(pts)
        if not poly.is_valid:
            poly = poly.buffer(0)
        if poly.is_empty or poly.area < 12.0 or boundary.contains(poly.centroid):
            continue
        smaller = poly.area
        duplicate = False
        for i in tree.query(poly, predicate="intersects"):
            other = existing[i]
            if poly.intersection(other).area > OVERTURE_DUPLICATE * min(smaller, other.area):
                duplicate = True
                break
        if duplicate:
            continue
        tags = {"building": b["c"] or "yes"}
        if b["h"]:
            tags["height"] = str(round(b["h"], 1))
        if b["f"]:
            tags["building:levels"] = str(b["f"])
        snapshot["buildings"].append({"id": "ov:" + b["id"], "tags": tags, "geometry": b["g"]})
        added += 1
    return added


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fetch", action="store_true", help="Download from Overpass first")
    parser.add_argument("--no-overture", action="store_true", help="OSM footprints only (the pre-ADR 0236 behaviour)")
    args = parser.parse_args()
    layout = load_layout()

    if args.fetch or not SNAPSHOT.exists():
        if args.fetch or not RAW.exists():
            fetch()
        raw = json.loads(RAW.read_text())
        aerodrome = json.loads(AERODROME_RAW.read_text())["elements"][0]
        kept = []
        boundary = Polygon([local(layout, p["lon"], p["lat"]) for p in aerodrome["geometry"]])
        for element in raw["elements"]:
            if element.get("type") != "way" or "geometry" not in element:
                continue
            pts = [local(layout, p["lon"], p["lat"]) for p in element["geometry"]]
            cx = sum(p[0] for p in pts) / len(pts)
            cz = sum(p[1] for p in pts) / len(pts)
            if outside_airfield(cx, cz) > BAND_METRES:
                continue
            if boundary.buffer(5.0).contains(Polygon(pts).centroid if len(pts) >= 3 else Polygon()):
                continue
            tags = {k: v for k, v in element.get("tags", {}).items()
                    if k in ("building", "height", "building:levels", "roof:shape", "name")}
            kept.append({"id": element["id"], "tags": tags,
                         "geometry": [[round(p["lon"], 7), round(p["lat"], 7)] for p in element["geometry"]]})
        SNAPSHOT.parent.mkdir(parents=True, exist_ok=True)
        SNAPSHOT.write_text(json.dumps({
            "source": "OpenStreetMap contributors (ODbL), Overpass API",
            "osm_base": raw["osm3s"]["timestamp_osm_base"],
            "query": f'[bbox:{BBOX}];way["building"];out tags geom; filtered to {BAND_METRES:.0f} m '
                     "outside the airfield rectangle and outside aerodrome way 146489105",
            "buildings": kept,
        }, separators=(",", ":")))

    if args.fetch or not STREETS_SNAPSHOT.exists():
        raw_streets = json.loads(STREETS_RAW.read_text())
        kept_streets = []
        for element in raw_streets["elements"]:
            if element.get("type") != "way" or "geometry" not in element:
                continue
            pts = [local(layout, p["lon"], p["lat"]) for p in element["geometry"]]
            if min(outside_airfield(x, z) for x, z in pts) > FILL_BAND_METRES + 100:
                continue
            kept_streets.append({"id": element["id"], "tags": {"highway": element["tags"].get("highway")},
                                 "geometry": [[round(p["lon"], 7), round(p["lat"], 7)] for p in element["geometry"]]})
        STREETS_SNAPSHOT.write_text(json.dumps({
            "source": "OpenStreetMap contributors (ODbL), Overpass API",
            "osm_base": raw_streets["osm3s"]["timestamp_osm_base"],
            "query": f"[bbox:{BBOX}];way[highway~residential|...|service];out tags geom; "
                     f"filtered to ways reaching within {FILL_BAND_METRES + 100:.0f} m of the airfield rectangle",
            "streets": kept_streets,
        }, separators=(",", ":")))

    snapshot = json.loads(SNAPSHOT.read_text())
    osm_count = len(snapshot["buildings"])
    overture_added = 0 if args.no_overture else merge_overture(layout, snapshot)
    # the band fetch keeps every highway; the house filler only wants streets a house can face
    footpaths = {"footway", "cycleway", "path", "steps", "pedestrian", "corridor", "platform", "construction", "track", "bridleway"}
    streets = [st for st in json.loads(STREETS_SNAPSHOT.read_text())["streets"]
               if st["tags"].get("highway") not in footpaths]
    aerodrome = json.loads(AERODROME_RAW.read_text())["elements"][0] if AERODROME_RAW.exists() else None
    osm_polys = []
    keep_out = airport_keep_out()
    excluded = thinned = 0
    out = bytearray()
    count = houses = flats = triangles = 0
    for building in snapshot["buildings"]:
        pts = [local(layout, lon, lat) for lon, lat in building["geometry"]]
        if len(pts) < 4:
            continue
        poly = Polygon(pts)
        if not poly.is_valid:
            poly = poly.buffer(0)
        if poly.is_empty or poly.geom_type != "Polygon" or poly.area < 12.0:
            continue
        osm_polys.append(poly)
        if keep_out.intersects(poly):
            excluded += 1
            continue
        if not keep_at(building["id"], poly.centroid.x, poly.centroid.y):
            thinned += 1
            continue
        tags = building["tags"]
        kind = tags.get("building", "yes")
        area = poly.area
        rect = poly.minimum_rotated_rectangle
        fill = area / max(rect.area, 1e-6)
        is_house = (kind in HOUSE_TAGS and area < 600) or (kind == "yes" and 60 <= area <= 350 and fill > 0.7)
        wall, rise = heights(tags, area, is_house)
        if is_house:
            corners = list(rect.exterior.coords)[:4]
            e1 = (corners[1][0] - corners[0][0], corners[1][1] - corners[0][1])
            e2 = (corners[2][0] - corners[1][0], corners[2][1] - corners[1][1])
            l1, l2 = math.hypot(*e1), math.hypot(*e2)
            along = e1 if l1 >= l2 else e2
            angle = math.atan2(along[1], along[0])
            cx, cz = rect.centroid.x, rect.centroid.y
            out += struct.pack("<BBBff", 1, HOUSE_WALLS[stable(building["id"], len(HOUSE_WALLS))],
                               HOUSE_ROOFS[stable(f"r{building['id']}", len(HOUSE_ROOFS))], wall, rise)
            out += struct.pack("<fffff", cx, cz, max(l1, l2) / 2, min(l1, l2) / 2, angle)
            houses += 1
            triangles += 8 + 6
        else:
            # Coarser simplification before falling back to the bounding rectangle, which can
            # reach well past an irregular footprint (and into the airport's own buildings).
            ring, tris = None, None
            for tolerance in (SIMPLIFY_METRES, 1.5, 3.0):
                simple = poly.simplify(tolerance, preserve_topology=True)
                if simple.geom_type != "Polygon":
                    continue
                candidate = list(orient(simple, 1.0).exterior.coords)[:-1]
                if 3 <= len(candidate) <= MAX_VERTICES:
                    tris = ear_clip(candidate)
                    if tris:
                        ring = candidate
                        break
            if ring is None:
                ring = list(orient(poly.minimum_rotated_rectangle, 1.0).exterior.coords)[:-1]
                tris = ear_clip(ring)
            if not tris or keep_out.intersects(Polygon(ring)):
                excluded += 1
                continue
            out += struct.pack("<BBBff", 0, OTHER_WALLS[stable(building["id"], len(OTHER_WALLS))],
                               OTHER_ROOFS[stable(f"r{building['id']}", len(OTHER_ROOFS))], wall, 0.0)
            out += struct.pack("<B", len(ring))
            for x, z in ring:
                out += struct.pack("<ff", x, z)
            out += struct.pack("<B", len(tris))
            for tri in tris:
                out += struct.pack("<BBB", *tri)
            flats += 1
            triangles += 2 * len(ring) + len(tris)
        count += 1

    fillers = 0
    if aerodrome is not None:
        boundary = Polygon([local(layout, p["lon"], p["lat"]) for p in aerodrome["geometry"]]).buffer(20.0)
    else:
        boundary = Polygon()
    for fid, cx, cz, half_len, half_wid, angle in fill_frontage(layout, streets, osm_polys, boundary,
                                                                 landcover_grid()):
        ux, uz = math.cos(angle), math.sin(angle)
        footprint = Polygon([(cx + sl * half_len * ux - sw * half_wid * uz, cz + sl * half_len * uz + sw * half_wid * ux)
                             for sl, sw in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
        if keep_out.intersects(footprint) or not keep_at(fid, cx, cz):
            continue
        area = 4 * half_len * half_wid
        out += struct.pack("<BBBff", 2, HOUSE_WALLS[stable(fid, len(HOUSE_WALLS))],
                           HOUSE_ROOFS[stable("r" + fid, len(HOUSE_ROOFS))], 2.9, 1.5 + min(1.0, area / 400.0))
        out += struct.pack("<fffff", cx, cz, half_len, half_wid, angle)
        count += 1
        fillers += 1
        triangles += 14

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(b"ASUB" + struct.pack("<ii", 1, count) + bytes(out))
    print(f"Wrote {OUTPUT}: {count} buildings ({houses} OSM houses, {flats} flat, {fillers} street-front "
          f"fill; {excluded} airport-owned, {thinned} thinned), "
          f"~{triangles} triangles, {OUTPUT.stat().st_size} bytes; snapshot {osm_count} "
          f"from OSM base {snapshot['osm_base']} + {overture_added} Overture")


if __name__ == "__main__":
    main()
