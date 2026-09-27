#!/usr/bin/env python3
"""ADR 0140 — builds the route map's geography from public-domain data.

Writes game/Airside/Assets/Airside/Presentation/MapGeographyData.cs:
  * coastlines at three levels of detail (Natural Earth 1:110m world, 1:50m region,
    1:10m Australia / New Zealand / New Guinea / Indonesia), simplified per level;
  * Australian state and territory borders (Natural Earth 1:10m admin-1 lines);
  * towns and cities (Natural Earth 1:10m populated places) for the zoomed-in town layer;
  * runways and elevations for every airport the game serves (OurAirports).

Natural Earth and OurAirports are public domain (asset register MAP-001, MAP-002).
Longitudes are unwrapped to 0..360 so the Pacific reads as one ocean: Honolulu is 202°,
Los Angeles 242°. Usage:

  scripts/map/build_map_data.py [cache-dir]

Missing source files are downloaded into the cache dir (default: /tmp/airside-map-cache).
"""
import csv, json, math, os, re, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game/Airside/Assets/Airside/Presentation/MapGeographyData.cs")
DEST = os.path.join(ROOT, "game/Airside/Assets/Airside/Domain/Destination.cs")
NE = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/"
OA = "https://davidmegginson.github.io/ourairports-data/"
SOURCES = {
    "ne_110m_land.geojson": NE + "ne_110m_land.geojson",
    "ne_50m_land.geojson": NE + "ne_50m_land.geojson",
    "ne_10m_land.geojson": NE + "ne_10m_land.geojson",
    "ne_10m_minor_islands.geojson": NE + "ne_10m_minor_islands.geojson",
    "ne_10m_populated_places_simple.geojson": NE + "ne_10m_populated_places_simple.geojson",
    "ne_10m_admin_1_states_provinces_lines.geojson": NE + "ne_10m_admin_1_states_provinces_lines.geojson",
    "airports.csv": OA + "airports.csv",
    "runways.csv": OA + "runways.csv",
}

# The map's window, in unwrapped longitude: Doha (51°) to Los Angeles (242°).
WINDOW = (20.0, 260.0, -56.0, 62.0)


def fetch(cache):
    os.makedirs(cache, exist_ok=True)
    for name, url in SOURCES.items():
        path = os.path.join(cache, name)
        if not os.path.exists(path):
            print("downloading", url)
            urllib.request.urlretrieve(url, path)
    return lambda name: os.path.join(cache, name)


def unwrap_ring(ring):
    mean = sum(p[0] for p in ring) / len(ring)
    return [(x + 360.0 if mean < 0 else x, y) for x, y in ring]


def bbox(ring):
    xs = [p[0] for p in ring]; ys = [p[1] for p in ring]
    return min(xs), max(xs), min(ys), max(ys)


def intersects(b, w):
    return not (b[1] < w[0] or b[0] > w[1] or b[3] < w[2] or b[2] > w[3])


def area(ring):
    s = 0.0
    for i in range(len(ring) - 1):
        s += ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1]
    return abs(s) / 2.0


def simplify(points, tol):
    """Douglas-Peucker, iterative."""
    if len(points) < 3:
        return points
    keep = [False] * len(points)
    keep[0] = keep[-1] = True
    stack = [(0, len(points) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = points[a]; bx, by = points[b]
        dx, dy = bx - ax, by - ay
        norm = math.hypot(dx, dy)
        worst, index = -1.0, -1
        for i in range(a + 1, b):
            px, py = points[i]
            d = abs(dy * px - dx * py + bx * ay - by * ax) / norm if norm > 0 else math.hypot(px - ax, py - ay)
            if d > worst:
                worst, index = d, i
        if worst > tol:
            keep[index] = True
            stack.append((a, index)); stack.append((index, b))
    return [p for p, k in zip(points, keep) if k]


def rings_of(geojson_path):
    data = json.load(open(geojson_path, encoding="utf-8"))
    for feature in data["features"]:
        geometry = feature["geometry"]
        polygons = geometry["coordinates"] if geometry["type"] == "MultiPolygon" else [geometry["coordinates"]]
        for polygon in polygons:
            yield [tuple(p[:2]) for p in polygon[0]]  # outer ring only: lakes are not drawn


def coast_level(paths, window, tol, min_area):
    out = []
    for path in paths:
        for ring in rings_of(path):
            ring = unwrap_ring(ring)
            if not intersects(bbox(ring), window) or area(ring) < min_area:
                continue
            simple = simplify(ring, tol)
            if len(simple) >= 4:
                out.append(simple)
    return out


def clip_pieces(ring, box):
    """Runs of a ring inside box (with one point either side), as open polylines."""
    w, e, so, n = box
    inside = [w <= x <= e and so <= y <= n for x, y in ring]
    pieces, current = [], []
    for i, point in enumerate(ring):
        near = inside[i] or (i > 0 and inside[i - 1]) or (i + 1 < len(ring) and inside[i + 1])
        if near:
            current.append(point)
        elif current:
            pieces.append(current); current = []
    if current:
        pieces.append(current)
    return [p for p in pieces if len(p) >= 2]


def airport_coasts(path, airports, radius=1.5, tol=0.004):
    """Fine 1:10m coast around each overseas airport, so zooming in on one shows its real shoreline."""
    boxes = [(lon - radius, lon + radius, lat - radius, lat + radius) for lon, lat in airports]
    out = []
    for ring in rings_of(path):
        ring = unwrap_ring(ring)
        b = bbox(ring)
        for box in boxes:
            if intersects(b, box):
                for piece in clip_pieces(ring, box):
                    simple = simplify(piece, tol)
                    if len(simple) >= 2:
                        out.append(simple)
    return out


def served_airports():
    src = open(DEST, encoding="utf-8").read()
    out = []
    for m in re.finditer(r'new(?: Destination)?\("\w{3}", "[^"]+", "[^"]*", (-?[\d.]+), (-?[\d.]+)', src):
        lat, lon = float(m.group(1)), float(m.group(2))
        out.append((lon + 360.0 if lon < 0 else lon, lat))
    return out


def borders(path):
    data = json.load(open(path, encoding="utf-8"))
    out = []
    for feature in data["features"]:
        if feature["properties"].get("ADM0_A3") != "AUS":
            continue
        geometry = feature["geometry"]
        lines = geometry["coordinates"] if geometry["type"] == "MultiLineString" else [geometry["coordinates"]]
        for line in lines:
            simple = simplify([tuple(p[:2]) for p in line], 0.05)
            if len(simple) >= 2:
                out.append(simple)
    return out


def towns(path):
    data = json.load(open(path, encoding="utf-8"))
    out = []
    for feature in data["features"]:
        p = feature["properties"]
        if p.get("adm0name") not in ("Australia", "New Zealand"):
            continue
        population = p.get("pop_max") or 0
        if population < 2000:
            continue
        lon, lat = feature["geometry"]["coordinates"][:2]
        # Rank 0: capital cities; 1: 50k+; 2: 10k+; 3: the rest.
        rank = 0 if p.get("adm0cap") or p.get("featurecla", "").startswith("Admin-1 capital") else \
            1 if population >= 50000 else 2 if population >= 10000 else 3
        out.append((p["name"], lon, lat, rank, int(population)))
    out.sort(key=lambda t: (t[3], -t[4], t[0]))
    return out


def served_codes():
    src = open(DEST, encoding="utf-8").read()
    return sorted(set(re.findall(r'new(?: Destination)?\("(\w{3})"', src)))


def runways(airports_path, runways_path, codes):
    by_ident, info = {}, {}
    for row in csv.DictReader(open(airports_path, encoding="utf-8")):
        if row["iata_code"] in codes and row["type"] in ("large_airport", "medium_airport", "small_airport") \
                and row["iata_code"] not in info:
            by_ident[row["ident"]] = row["iata_code"]
            info[row["iata_code"]] = row
    out = {code: [] for code in codes}
    for row in csv.DictReader(open(runways_path, encoding="utf-8")):
        code = by_ident.get(row["airport_ident"])
        if code is None or row["closed"] == "1":
            continue
        try:
            length_m = float(row["length_ft"]) * 0.3048
        except ValueError:
            continue
        try:
            coords = [float(row[k]) for k in ("le_latitude_deg", "le_longitude_deg", "he_latitude_deg", "he_longitude_deg")]
        except ValueError:
            # No surveyed thresholds (Mount Gambier): lay the strip through the airport reference
            # point on the heading its number gives. Close enough to read on a map.
            coords = derived_ends(info[code], row, length_m)
            if coords is None:
                continue
        if length_m < 900:
            continue
        width_m = float(row["width_ft"]) * 0.3048 if row["width_ft"] else 30.0
        out[code].append((row["le_ident"], row["he_ident"], *coords, round(length_m), round(width_m)))
    elevations = {code: int(float(info[code]["elevation_ft"])) if code in info and info[code]["elevation_ft"]
                  else ELEVATION_FT.get(code, 0) for code in codes}
    names = {code: info[code]["name"] if code in info else code for code in codes}
    return out, elevations, names


def derived_ends(airport, row, length_m):
    heading = row.get("le_heading_degT")
    try:
        heading = float(heading) if heading else float(re.sub(r"[^0-9]", "", row["le_ident"])) * 10.0
    except ValueError:
        return None
    lat0, lon0 = float(airport["latitude_deg"]), float(airport["longitude_deg"])
    half = length_m / 2.0
    dlat = half * math.cos(math.radians(heading)) / 111320.0
    dlon = half * math.sin(math.radians(heading)) / (111320.0 * math.cos(math.radians(lat0)))
    return [lat0 - dlat, lon0 - dlon, lat0 + dlat, lon0 + dlon]


# Field elevations OurAirports leaves blank (feet, from the AIP).
ELEVATION_FT = {"MGB": 212}


def f(v):
    return f"{v:.3f}f"


def emit_rings(name, rings, doc):
    lines = [f"        /// <summary>{doc}</summary>", f"        public static readonly float[][] {name} =", "        {"]
    for ring in rings:
        flat = ", ".join(f"{f(x)}, {f(y)}" for x, y in ring)
        lines.append(f"            new[] {{ {flat} }},")
    lines.append("        };")
    return "\n".join(lines)


def main():
    path = fetch(sys.argv[1] if len(sys.argv) > 1 else "/tmp/airside-map-cache")
    world = coast_level([path("ne_110m_land.geojson")], WINDOW, 0.25, 4.0)
    region = coast_level([path("ne_50m_land.geojson")], (90.0, 245.0, -50.0, 40.0), 0.07, 0.08)
    detail = coast_level([path("ne_10m_land.geojson"), path("ne_10m_minor_islands.geojson")],
                         (110.0, 180.0, -48.0, -8.0), 0.02, 0.004)
    near = served_airports()
    if len(near) < 40:
        raise SystemExit(f"expected every served airport, found {len(near)}")
    around = airport_coasts(path("ne_10m_land.geojson"), near, radius=0.8)
    state_lines = borders(path("ne_10m_admin_1_states_provinces_lines.geojson"))
    places = towns(path("ne_10m_populated_places_simple.geojson"))
    codes = served_codes()
    strips, elevations, names = runways(path("airports.csv"), path("runways.csv"), codes)
    missing = [c for c in codes if not strips[c]]
    if missing:
        raise SystemExit(f"no runways found for {missing}")

    count = lambda rings: sum(len(r) for r in rings)
    print(f"world {len(world)} rings/{count(world)} pts, region {len(region)}/{count(region)}, "
          f"detail {len(detail)}/{count(detail)}, around airports {len(around)}/{count(around)}, borders {len(state_lines)}/{count(state_lines)}, "
          f"towns {len(places)}, airports {len(codes)}")

    parts = [
        "// <auto-generated>",
        "// Generated by scripts/map/build_map_data.py (ADR 0140) from Natural Earth (public domain,",
        "// MAP-001) and OurAirports (public domain, MAP-002). Do not edit by hand: re-run the script.",
        "// Longitudes are unwrapped to 0..360 east so the Pacific is continuous.",
        "// </auto-generated>",
        "namespace Airside.Presentation",
        "{",
        "    public static class MapGeographyData",
        "    {",
        emit_rings("WorldCoasts", world, "Land outlines, 1:110m, for the zoomed-out world view."),
        emit_rings("RegionCoasts", region, "Land outlines, 1:50m, for Asia-Pacific zoom."),
        emit_rings("DetailCoasts", detail, "Land outlines, 1:10m with islands, for Australia, New Zealand and near neighbours."),
        emit_rings("AirportCoasts", around, "Open coast lines, 1:10m, within 0.8° of each served airport, for zooming in on one."),
        emit_rings("StateBorders", state_lines, "Australian state and territory borders, 1:10m."),
        "",
        "        /// <summary>Towns and cities: name, longitude, latitude, rank (0 capital, 1 50k+, 2 10k+, 3 smaller).</summary>",
        "        public static readonly (string Name, float Lon, float Lat, int Rank)[] Towns =",
        "        {",
    ]
    for name, lon, lat, rank, _ in places:
        parts.append(f'            ("{name}", {f(lon)}, {f(lat)}, {rank}),')
    parts += ["        };", "",
              "        /// <summary>Runways: airport code, ends, threshold coordinates (lat, lon), length and width in metres.</summary>",
              "        public static readonly (string Code, string End1, string End2, double Lat1, double Lon1, double Lat2, double Lon2, int LengthM, int WidthM)[] Runways =",
              "        {"]
    for code in codes:
        for le, he, lat1, lon1, lat2, lon2, length, width in strips[code]:
            parts.append(f'            ("{code}", "{le}", "{he}", {lat1:.5f}, {lon1:.5f}, {lat2:.5f}, {lon2:.5f}, {length}, {width}),')
    parts += ["        };", "",
              "        /// <summary>Airport name and field elevation (feet) by code.</summary>",
              "        public static readonly (string Code, string Name, int ElevationFt)[] Airports =",
              "        {"]
    for code in codes:
        parts.append(f'            ("{code}", "{names[code]}", {elevations[code]}),')
    parts += ["        };", "    }", "}", ""]
    with open(OUT, "w", encoding="utf-8") as out:
        out.write("\n".join(parts))
    print("wrote", OUT, os.path.getsize(OUT), "bytes")


if __name__ == "__main__":
    main()
