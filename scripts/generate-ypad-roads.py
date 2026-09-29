#!/usr/bin/env python3
"""Generate the complete Adelaide Airport road network for Airside (ADR 0184, map overhaul P1).

Reads docs/data/osm/ypad-map-<date>.json (© OpenStreetMap contributors, ODbL) and the aerodrome
outline in docs/data/osm/ypad-boundary-2026-09-29.json, and writes
game/Airside/Assets/Airside/Simulation/AdelaideRoadNetwork.cs:

  Roads      every drivable highway (motorway ... service, track) as a polyline in the runway frame,
             with class, width, lanes, one-way, surface, layer, airside flag, and a name
  Junctions  every shared node: x, z, half-width of the widest road, degree
  Furniture  crossings, signals, give-way/stop, bus stops, gates — with the road heading where known

Junction nodes are kept through simplification so roads still meet exactly. Footways and cycleways
are not roads here. Standard library only.

Run: python3 scripts/generate-ypad-roads.py [--snapshot FILE]
Check without writing: python3 scripts/generate-ypad-roads.py --check
"""
import argparse
import base64
import collections
import glob
import math
import os
import re
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ypad_osm import ROOT, OSM_DIR, load_snapshot, polyline_length, simplify, to_local, way_points  # noqa: E402

OUTPUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Simulation/AdelaideRoadNetwork.cs")
BOUNDARY = "ypad-boundary-2026-09-29.json"
BOUNDARY_WAY = 146489105
FAR_SNAPSHOT = "ypad-landcover-2026-09-15.json"   # arterials out to +-6.5 km (DAT-YPAD-LANDCOVER)

# Road classes: index is the C# RoadClass value.
CLASSES = ["Motorway", "Trunk", "Primary", "Secondary", "Tertiary", "Unclassified", "Residential",
           "LivingStreet", "Service", "Track"]
HIGHWAY_CLASS = {"motorway": 0, "trunk": 1, "primary": 2, "secondary": 3, "tertiary": 4,
                 "unclassified": 5, "residential": 6, "living_street": 7, "service": 8, "track": 9}
DEFAULT_WIDTH = {0: 22.0, 1: 16.0, 2: 14.0, 3: 12.0, 4: 10.0, 5: 8.0, 6: 7.5, 7: 6.0, 8: 6.0, 9: 4.0}
SERVICE_WIDTH = {"parking_aisle": 5.5, "driveway": 3.5, "drive-through": 3.5, "alley": 4.0,
                 "emergency_access": 4.0}
SERVICE_KIND = {None: 0, "parking_aisle": 1, "driveway": 2, "drive-through": 3, "alley": 4,
                "emergency_access": 5}
LANE_WIDTH = 3.4
SURFACE = {"asphalt": 0, "paved": 0, "concrete": 1, "concrete:plates": 1, "concrete:lanes": 1,
           "paving_stones": 3, "sett": 3, "cobblestone": 3, "bricks": 3, "gravel": 2, "compacted": 2,
           "fine_gravel": 2, "unpaved": 2, "dirt": 2, "ground": 2, "grass": 2, "sand": 2}

F_ONEWAY, F_AIRSIDE, F_LINK, F_ROUNDABOUT, F_BRIDGE, F_TUNNEL, F_RESTRICTED = 1, 2, 4, 8, 16, 32, 64

FURNITURE = {  # (highway|barrier|amenity value) -> kind index
    ("highway", "crossing"): 0, ("highway", "traffic_signals"): 1, ("highway", "give_way"): 2,
    ("highway", "stop"): 3, ("highway", "bus_stop"): 4, ("barrier", "gate"): 5,
    ("barrier", "lift_gate"): 5, ("highway", "turning_circle"): 6,
}
FURNITURE_NAMES = ["Crossing", "TrafficSignal", "GiveWay", "Stop", "BusStop", "Gate", "TurningCircle"]
HEADING_KINDS = {0, 1, 2, 3, 4, 5}
SIMPLIFY_TOL = 0.35
OUT_PREC = 1


def band_streets():
    """Streets of the suburb band (adelaide-suburb-streets-<date>.json), in the snapshot's way shape."""
    files = sorted(glob.glob(os.path.join(OSM_DIR, "adelaide-suburb-streets-*.json")))
    if not files:
        return []
    out = []
    for e in load_snapshot(os.path.basename(files[-1]))["streets"]:
        out.append({"type": "way", "id": e["id"], "tags": e["tags"],
                    "geometry": [{"lat": p[1], "lon": p[0]} for p in e["geometry"]]})
    return out


def latest_snapshot():
    files = sorted(glob.glob(os.path.join(OSM_DIR, "ypad-map-*.json")))
    if not files:
        sys.exit("no docs/data/osm/ypad-map-*.json — run scripts/fetch-ypad-osm.py first")
    return files[-1]


def point_in_polygon(pt, poly):
    x, z = pt
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi:
            inside = not inside
        j = i
    return inside


def parse_number(value):
    if value is None:
        return None
    m = re.match(r"\s*([0-9]+(?:\.[0-9]+)?)", str(value).replace(",", "."))
    return float(m.group(1)) if m else None


def classify(tags):
    hw = tags.get("highway")
    link = hw.endswith("_link") if hw else False
    base = hw[:-5] if link else hw
    return HIGHWAY_CLASS.get(base), link


def road_width(cls, tags, oneway, lanes):
    w = parse_number(tags.get("width"))
    if w and 2.0 <= w <= 40.0:
        return w
    if cls == 8:
        sw = SERVICE_WIDTH.get(tags.get("service"))
        if sw:
            return sw
    if lanes:
        return max(lanes * LANE_WIDTH + (1.0 if cls <= 3 else 0.0), 3.0)
    base = DEFAULT_WIDTH[cls]
    if oneway and cls <= 3:
        base *= 0.5
    return base


def keep_junction_simplify(pts, forced, tol):
    """RDP that never drops the indices in `forced` (junction nodes)."""
    idx = sorted(set([0, len(pts) - 1] + [i for i in forced if 0 <= i < len(pts)]))
    out = []
    for a, b in zip(idx, idx[1:]):
        seg = simplify(pts[a:b + 1], tol)
        out.extend(seg[:-1])
    out.append(pts[idx[-1]])
    return out


class Grid:
    """Uniform grid over road segments for nearest-heading queries."""

    def __init__(self, cell=40.0):
        self.cell = cell
        self.cells = collections.defaultdict(list)

    def add(self, a, b, info=None):
        c = self.cell
        n = int(math.dist(a, b) / c) + 1
        for i in range(n + 1):
            t = i / n
            key = (int((a[0] + (b[0] - a[0]) * t) // c), int((a[1] + (b[1] - a[1]) * t) // c))
            if not self.cells[key] or self.cells[key][-1][:2] != (a, b):
                self.cells[key].append((a, b, info))

    def nearest(self, p, radius):
        c = self.cell
        best, best_d = None, radius
        kx, kz = int(p[0] // c), int(p[1] // c)
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for a, b, info in self.cells.get((kx + dx, kz + dz), ()):
                    vx, vz = b[0] - a[0], b[1] - a[1]
                    l2 = vx * vx + vz * vz
                    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vz) / l2))
                    d = math.hypot(p[0] - (a[0] + vx * t), p[1] - (a[1] + vz * t))
                    if d < best_d:
                        best, best_d = (a, b, info), d
        return best, best_d


def build(snapshot):
    data = load_snapshot(os.path.basename(snapshot))
    boundary = load_snapshot(BOUNDARY)
    outline = next(way_points(e) for e in boundary["elements"] if e["id"] == BOUNDARY_WAY)

    ways = [e for e in data["elements"] if e["type"] == "way" and "highway" in e["tags"]]
    # Beyond the full-detail window only the arterials are mapped: the wider land-cover extract
    # (motorway .. secondary) fills the far field, so one network draws every road.
    near_ids = {w["id"] for w in ways}
    far = {}
    for e in load_snapshot(FAR_SNAPSHOT)["elements"]:
        if e["type"] == "way" and "highway" in e.get("tags", {}) and e["id"] not in near_ids:
            far[e["id"]] = e
    # The suburb band (every street out to about 4 km) has the local streets the arterial extract lacks, and its tags win.
    for e in band_streets():
        if e["id"] not in near_ids:
            far[e["id"]] = e
    ways = ways + list(far.values())
    ways = [w for w in ways if classify(w["tags"])[0] is not None and len(w["geometry"]) >= 2]
    ways.sort(key=lambda w: w["id"])

    # Junctions: a lat/lon shared by two or more vertices across the road ways.
    use = collections.Counter()
    for w in ways:
        for p in w["geometry"]:
            use[(p["lat"], p["lon"])] += 1
    shared = {k for k, n in use.items() if n >= 2}

    roads, points, names = [], [], {}
    junction_use = collections.defaultdict(lambda: [0, 0.0])  # xz key -> [degree, max half width]
    grid = Grid()

    for w in ways:
        tags = w["tags"]
        cls, link = classify(tags)
        raw = way_points(w)
        forced = [i for i, p in enumerate(w["geometry"]) if (p["lat"], p["lon"]) in shared]
        pts = keep_junction_simplify(raw, forced, SIMPLIFY_TOL)
        # collapse zero-length runs
        clean = [pts[0]]
        for p in pts[1:]:
            if math.dist(p, clean[-1]) > 0.05:
                clean.append(p)
        if len(clean) < 2:
            continue
        oneway = tags.get("oneway") in ("yes", "true", "1", "-1") or tags.get("junction") == "roundabout"
        if tags.get("oneway") == "-1":
            clean.reverse()
        lanes = int(parse_number(tags.get("lanes")) or 0)
        width = round(road_width(cls, tags, oneway, lanes), 1)
        mid = clean[len(clean) // 2]
        access = tags.get("access")
        airside = point_in_polygon(mid, outline) and cls in (8, 9, 5)
        flags = 0
        flags |= F_ONEWAY if oneway else 0
        flags |= F_AIRSIDE if airside else 0
        flags |= F_LINK if link else 0
        flags |= F_ROUNDABOUT if tags.get("junction") == "roundabout" else 0
        flags |= F_BRIDGE if tags.get("bridge") not in (None, "no") else 0
        flags |= F_TUNNEL if tags.get("tunnel") not in (None, "no") else 0
        flags |= F_RESTRICTED if access in ("no", "private", "customers", "permit") else 0
        layer = int(parse_number(tags.get("layer")) or 0) if tags.get("layer", "0").lstrip("-").isdigit() else 0
        if flags & F_BRIDGE:
            layer = max(layer, 1)
        if flags & F_TUNNEL:
            layer = min(layer, -1)
        name = tags.get("name") or ""
        if name and name not in names:
            names[name] = len(names)
        roads.append({
            "id": w["id"], "cls": cls, "flags": flags, "lanes": lanes, "width": width,
            "surface": SURFACE.get(tags.get("surface"), 0), "layer": layer,
            "sub": SERVICE_KIND.get(tags.get("service"), 6) if cls == 8 else 0,
            "name": names[name] if name else -1, "start": len(points), "count": len(clean),
            "maxspeed": int(parse_number(tags.get("maxspeed")) or 0),
        })
        points.extend(clean)
        for a, b in zip(clean, clean[1:]):
            grid.add(a, b, (width, cls, airside, oneway))
        # junction bookkeeping on the simplified geometry (raw junction vertices are kept)
        for i in forced:
            p = raw[i]
            key = (round(p[0], 1), round(p[1], 1))
            junction_use[key][0] += 1
            junction_use[key][1] = max(junction_use[key][1], width / 2.0)

    junctions = []
    for (x, z), (deg, hw) in sorted(junction_use.items()):
        if deg >= 2:
            junctions.append((x, z, hw, deg))

    furniture = []
    for e in data["elements"]:
        if e["type"] != "node":
            continue
        kind = None
        for (k, v), idx in FURNITURE.items():
            if e["tags"].get(k) == v:
                kind = idx
                break
        if kind is None:
            continue
        p = to_local(e["lat"], e["lon"])
        yaw, width, heading, flags = 0.0, 0.0, 0.0, 0
        if kind in HEADING_KINDS:
            seg, d = grid.nearest(p, 4.0)
            if seg:
                heading = math.degrees(math.atan2(seg[1][1] - seg[0][1], seg[1][0] - seg[0][0])) % 360.0
                if e["tags"].get("direction") in ("backward", "reverse"):
                    heading = (heading + 180.0) % 360.0
                yaw = round(heading % 180.0, 0)
                heading = round(heading, 0)
                width = seg[2][0]
                flags = 1 if seg[2][3] else 0
        furniture.append((kind, p[0], p[1], yaw, width, heading, flags))
    furniture.sort()

    return data, roads, points, sorted(names, key=names.get), junctions, furniture


def f(v):
    return f"{v:.{OUT_PREC}f}f"


def wrap(items, per_line, indent):
    lines = []
    for i in range(0, len(items), per_line):
        lines.append(" " * indent + ", ".join(items[i:i + per_line]) + ",")
    return "\n".join(lines)


def cs_string(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def pack(roads, points, junctions, furniture):
    """Little-endian, delta-encoded, raw-deflated blob (see AdelaideRoadNetwork.Decode). Coordinates are 0.1 m ints."""
    dm = lambda v: int(round(v * 10.0))
    buf = bytearray(struct.pack("<4I", len(roads), len(points), len(junctions), len(furniture)))
    for r in roads:
        assert r["count"] < 65536 and r["width"] * 10 < 65536 and r["name"] < 32768
        buf += struct.pack("<IBBBBBbBhHH", r["id"], r["cls"], r["flags"], r["lanes"], r["surface"], r["sub"],
                           r["layer"], r["maxspeed"], r["name"], dm(r["width"]), r["count"])
    px = pz = 0
    for x, z in points:
        ix, iz = dm(x), dm(z)
        buf += struct.pack("<ii", ix - px, iz - pz)
        px, pz = ix, iz
    px = pz = 0
    for x, z, hw, deg in junctions:
        ix, iz = dm(x), dm(z)
        buf += struct.pack("<iiBB", ix - px, iz - pz, dm(hw), deg)
        px, pz = ix, iz
    for kind, x, z, yaw, width, heading, flags in furniture:
        buf += struct.pack("<BiiHHHB", kind, dm(x), dm(z), int(yaw), dm(width), int(heading), flags)
    checksum = sum(dm(x) + 3 * dm(z) for x, z in points)
    comp = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = comp.compress(bytes(buf)) + comp.flush()
    return base64.b64encode(packed).decode("ascii"), checksum, len(buf), len(packed)


def emit(snapshot, data, roads, points, names, junctions, furniture):
    counts = collections.Counter(CLASSES[r["cls"]] for r in roads)
    airside = sum(1 for r in roads if r["flags"] & F_AIRSIDE)
    total_km = sum(polyline_length(points[r["start"]:r["start"] + r["count"]]) for r in roads) / 1000.0
    blob, checksum, raw_bytes, packed_bytes = pack(roads, points, junctions, furniture)
    blob_lines = "\n".join('            "' + blob[i:i + 100] + '"' + (" +" if i + 100 < len(blob) else "")
                           for i in range(0, len(blob), 100))
    src = os.path.relpath(snapshot, ROOT)
    out = f"""// GENERATED by scripts/generate-ypad-roads.py — do not edit by hand.
// Source: {src} (+ docs/data/osm/{BOUNDARY})
// © OpenStreetMap contributors, ODbL. OSM base {data.get('retrieved', '')}.
// {len(roads)} roads, {total_km:.0f} km ({airside} airside), {len(junctions)} junctions, {len(furniture)} furniture points.
// By class: {', '.join(f'{k} {v}' for k, v in sorted(counts.items()))}.

namespace Airside.Simulation
{{
    /// <summary>
    /// The complete drivable road network around Adelaide Airport in the game's runway frame (metres):
    /// x along runway 05→23, z to the left (north-west / terminal side). Every OpenStreetMap
    /// <c>highway</c> a vehicle can use, from the motorway to airside service tracks — no length cap and
    /// no operational-core skip (ADR 0184).
    ///
    /// Road polylines share their junction vertices exactly, so a mesher can fill junctions and start
    /// and end ribbons on the same point. No UnityEngine types: checked by the headless harness.
    /// </summary>
    public static class AdelaideRoadNetwork
    {{
        public const string Attribution = "Roads © OpenStreetMap contributors";
        public const string OsmBase = "{data.get('retrieved', '')}";

        public enum RoadClass : byte
        {{
{chr(10).join(f'            {n} = {i},' for i, n in enumerate(CLASSES))}
        }}

        [System.Flags]
        public enum RoadFlags : byte
        {{
            None = 0, OneWay = {F_ONEWAY}, Airside = {F_AIRSIDE}, Link = {F_LINK}, Roundabout = {F_ROUNDABOUT},
            Bridge = {F_BRIDGE}, Tunnel = {F_TUNNEL}, Restricted = {F_RESTRICTED},
        }}

        /// <summary>Surface: 0 asphalt, 1 concrete, 2 gravel or dirt, 3 pavers.</summary>
        public enum SurfaceKind : byte {{ Asphalt = 0, Concrete = 1, Unpaved = 2, Pavers = 3 }}

        /// <summary>Service road kind: 0 plain, 1 parking aisle, 2 driveway, 3 drive-through, 4 alley, 5 emergency, 6 other.</summary>
        public enum ServiceKind : byte {{ None = 0, ParkingAisle = 1, Driveway = 2, DriveThrough = 3, Alley = 4, Emergency = 5, Other = 6 }}

        public enum FurnitureKind : byte
        {{
{chr(10).join(f'            {n} = {i},' for i, n in enumerate(FURNITURE_NAMES))}
        }}

        public readonly struct Road
        {{
            public readonly long OsmId;
            public readonly RoadClass Class;
            public readonly RoadFlags Flags;
            public readonly byte Lanes;
            public readonly float Width;
            public readonly SurfaceKind Surface;
            public readonly sbyte Layer;
            public readonly ServiceKind Service;
            public readonly short NameIndex;
            public readonly int PointStart;
            public readonly int PointCount;
            public readonly byte MaxSpeedKmh;

            public Road(long osmId, int cls, int flags, int lanes, float width, int surface, int layer, int service,
                int nameIndex, int pointStart, int pointCount, int maxSpeed)
            {{
                OsmId = osmId;
                Class = (RoadClass)cls;
                Flags = (RoadFlags)flags;
                Lanes = (byte)lanes;
                Width = width;
                Surface = (SurfaceKind)surface;
                Layer = (sbyte)layer;
                Service = (ServiceKind)service;
                NameIndex = (short)nameIndex;
                PointStart = pointStart;
                PointCount = pointCount;
                MaxSpeedKmh = (byte)maxSpeed;
            }}

            public bool IsAirside => (Flags & RoadFlags.Airside) != 0;
            public bool IsOneWay => (Flags & RoadFlags.OneWay) != 0;
            public string Name => NameIndex < 0 ? null : Names[NameIndex];
        }}

        public static readonly string[] Names =
        {{
{wrap([cs_string(n) for n in names], 4, 12)}
        }};

        /// <summary>Sum over the points of x + 3 z in decimetres: a cheap check that the packed data decoded whole.</summary>
        public const long PointChecksum = {checksum}L;

        // The tables below are packed by scripts/generate-ypad-roads.py: {raw_bytes:,} bytes of little-endian records,
        // coordinates as 0.1 m integers (delta-encoded for points and junctions), raw-deflated to {packed_bytes:,} bytes.
        private const string Blob =
{blob_lines};

        private sealed class Tables
        {{
            public Road[] Roads;
            public float[] Points, Junctions, Furniture;
        }}

        private static readonly Tables Data = Decode();

        public static readonly Road[] Roads = Data.Roads;

        /// <summary>Road vertices as x, z pairs; each <see cref="Road"/> owns PointCount of them from PointStart.</summary>
        public static readonly float[] Points = Data.Points;

        /// <summary>Shared nodes as x, z, half-width of the widest road there, road count.</summary>
        public static readonly float[] Junctions = Data.Junctions;

        /// <summary>
        /// Street furniture, <see cref="FurnitureStride"/> floats each: kind, x, z, heading of the road it sits on
        /// (degrees, 0..180, x-axis = 0), that road's width (0 when no road lies within 4 m), the direction of travel
        /// the node applies to (degrees, 0..360, x-axis = 0; the way's own direction unless the node says backward)
        /// and flags (1 = the road is one-way).
        /// </summary>
        public static readonly float[] Furniture = Data.Furniture;

        private static Tables Decode()
        {{
            byte[] raw;
            using (var packed = new System.IO.MemoryStream(System.Convert.FromBase64String(Blob)))
            using (var inflate = new System.IO.Compression.DeflateStream(packed, System.IO.Compression.CompressionMode.Decompress))
            using (var unpacked = new System.IO.MemoryStream())
            {{
                inflate.CopyTo(unpacked);
                raw = unpacked.ToArray();
            }}

            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(raw));
            var roadCount = (int)r.ReadUInt32();
            var pointCount = (int)r.ReadUInt32();
            var junctionCount = (int)r.ReadUInt32();
            var furnitureCount = (int)r.ReadUInt32();
            var tables = new Tables
            {{
                Roads = new Road[roadCount],
                Points = new float[pointCount * 2],
                Junctions = new float[junctionCount * 4],
                Furniture = new float[furnitureCount * FurnitureStride]
            }};
            var start = 0;
            for (var i = 0; i < roadCount; i++)
            {{
                var id = r.ReadUInt32();
                int cls = r.ReadByte(), flags = r.ReadByte(), lanes = r.ReadByte(), surface = r.ReadByte(), sub = r.ReadByte();
                int layer = r.ReadSByte(), maxSpeed = r.ReadByte();
                int name = r.ReadInt16();
                var width = r.ReadUInt16() / 10f;
                int count = r.ReadUInt16();
                tables.Roads[i] = new Road(id, cls, flags, lanes, width, surface, layer, sub, name, start, count, maxSpeed);
                start += count;
            }}

            int px = 0, pz = 0;
            for (var i = 0; i < pointCount; i++)
            {{
                px += r.ReadInt32();
                pz += r.ReadInt32();
                tables.Points[i * 2] = px / 10f;
                tables.Points[i * 2 + 1] = pz / 10f;
            }}

            px = pz = 0;
            for (var i = 0; i < junctionCount; i++)
            {{
                px += r.ReadInt32();
                pz += r.ReadInt32();
                tables.Junctions[i * 4] = px / 10f;
                tables.Junctions[i * 4 + 1] = pz / 10f;
                tables.Junctions[i * 4 + 2] = r.ReadByte() / 10f;
                tables.Junctions[i * 4 + 3] = r.ReadByte();
            }}

            for (var i = 0; i < furnitureCount; i++)
            {{
                var o = i * FurnitureStride;
                tables.Furniture[o] = r.ReadByte();
                tables.Furniture[o + 1] = r.ReadInt32() / 10f;
                tables.Furniture[o + 2] = r.ReadInt32() / 10f;
                tables.Furniture[o + 3] = r.ReadUInt16();
                tables.Furniture[o + 4] = r.ReadUInt16() / 10f;
                tables.Furniture[o + 5] = r.ReadUInt16();
                tables.Furniture[o + 6] = r.ReadByte();
            }}

            return tables;
        }}

        public static int JunctionCount => Junctions.Length / 4;
        public const int FurnitureStride = 7;
        public static int FurnitureCount => Furniture.Length / FurnitureStride;

    }}
}}
"""
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    snapshot = args.snapshot or latest_snapshot()
    data, roads, points, names, junctions, furniture = build(snapshot)
    text = emit(snapshot, data, roads, points, names, junctions, furniture)
    if args.check:
        current = open(OUTPUT, encoding="utf-8").read() if os.path.exists(OUTPUT) else ""
        if current != text:
            sys.exit("AdelaideRoadNetwork.cs is out of date: run scripts/generate-ypad-roads.py")
        print("AdelaideRoadNetwork.cs is up to date")
        return
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    total = sum(polyline_length(points[r["start"]:r["start"] + r["count"]]) for r in roads) / 1000.0
    print(f"wrote {os.path.relpath(OUTPUT, ROOT)}: {len(roads)} roads, {total:.0f} km, {len(points)} points, "
          f"{len(junctions)} junctions, {len(furniture)} furniture, {len(text) / 1e3:.0f} KB")


if __name__ == "__main__":
    main()
