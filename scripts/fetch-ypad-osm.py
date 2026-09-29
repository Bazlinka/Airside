#!/usr/bin/env python3
"""Fetch a rich OpenStreetMap snapshot of Adelaide Airport and its landside for the map overhaul (ADR 0184).

Data © OpenStreetMap contributors, ODbL 1.0.

Uses the official OSM map API (api.openstreetmap.org/api/0.6/map?bbox=), tiled to stay under its
0.25 deg² / 50,000-node limits (a tile that is refused is split in four). This works where Overpass
is blocked. Pass --overpass to use an Overpass endpoint instead (one query, richer filters).

Writes docs/data/osm/ypad-map-<date>.json in the same shape as the other snapshots:
  {"elements": [{"type": "way", "id", "tags", "geometry": [{lat, lon}, ...]},
                {"type": "node", "id", "tags", "lat", "lon"}]}
Only map-relevant features are kept (see KEEP_*); houses, and untagged geometry, are dropped.

Run:  python3 scripts/fetch-ypad-osm.py [--bbox W,S,E,N] [--out FILE] [--overpass URL] [--keep-xml DIR]
"""
import argparse
import datetime
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API = "https://api.openstreetmap.org/api/0.6/map?bbox={w:.6f},{s:.6f},{e:.6f},{n:.6f}"
# Airport, terminal precinct, approach roads and the arterial roads that meet the fence.
DEFAULT_BBOX = (138.495, -34.975, 138.565, -34.925)  # W,S,E,N
TILE_LON, TILE_LAT = 0.014, 0.010                      # ~1.3 km x 1.1 km: well inside the API limits
USER_AGENT = "Airside-game-map-fetch/1.0 (private game project)"

# Way tags that make a way worth keeping.
KEEP_WAY_KEYS = {
    "highway", "aeroway", "railway", "barrier", "power", "man_made", "bridge", "tunnel",
    "amenity", "parking", "landuse", "leisure", "natural", "waterway", "public_transport",
}
# Buildings: keep only non-residential ones — the suburbs already have their own extract.
SKIP_BUILDING = {"house", "detached", "semidetached_house", "terrace", "residential", "garage",
                 "garages", "shed", "roof", "carport", "static_caravan", "bungalow"}
# Node tags that make a point worth keeping.
KEEP_NODE_KEYS = {
    "highway", "aeroway", "amenity", "barrier", "man_made", "power", "emergency", "entrance",
    "railway", "public_transport", "shop", "tourism", "traffic_sign", "traffic_calming",
    "natural", "information", "leisure",
}
DROP_TAGS = {"source", "created_by", "note", "fixme", "FIXME", "source:date", "survey:date"}


def http_get(url, tries=5):
    delay = 5.0
    for attempt in range(tries):
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code == 400:            # too many nodes / area too large — caller splits
                return None
            if e.code in (429, 500, 502, 503, 504, 509) and attempt < tries - 1:
                time.sleep(delay)
                delay *= 2
                continue
            raise
        except (urllib.error.URLError, TimeoutError, ConnectionError):
            if attempt == tries - 1:
                raise
            time.sleep(delay)
            delay *= 2
    return None


def parse_xml(data, nodes, ways):
    root = ET.fromstring(data)
    for n in root.findall("node"):
        nodes[n.attrib["id"]] = {
            "lat": float(n.attrib["lat"]),
            "lon": float(n.attrib["lon"]),
            "tags": {t.attrib["k"]: t.attrib["v"] for t in n.findall("tag")},
        }
    for w in root.findall("way"):
        ways[w.attrib["id"]] = {
            "tags": {t.attrib["k"]: t.attrib["v"] for t in w.findall("tag")},
            "nds": [nd.attrib["ref"] for nd in w.findall("nd")],
        }


def fetch_tile(w, s, e, n, nodes, ways, keep_dir, depth=0):
    data = http_get(API.format(w=w, s=s, e=e, n=n))
    if data is None:
        if depth > 4:
            sys.exit(f"tile {w},{s},{e},{n} refused even when split")
        mw, ms = (w + e) / 2, (s + n) / 2
        for bw, bs, be, bn in ((w, s, mw, ms), (mw, s, e, ms), (w, ms, mw, n), (mw, ms, e, n)):
            fetch_tile(bw, bs, be, bn, nodes, ways, keep_dir, depth + 1)
        return
    if keep_dir:
        os.makedirs(keep_dir, exist_ok=True)
        with open(os.path.join(keep_dir, f"tile-{w:.4f}-{s:.4f}.osm"), "wb") as f:
            f.write(data)
    parse_xml(data, nodes, ways)
    time.sleep(1.5)  # be polite to the public API


def keep_way(tags):
    b = tags.get("building")
    if b and b not in SKIP_BUILDING:
        return True
    if "building:part" in tags:
        return True
    return any(k in tags for k in KEEP_WAY_KEYS)


def clean(tags):
    return {k: v for k, v in tags.items() if k not in DROP_TAGS and not k.startswith("source:")}


def fetch_overpass(url, bbox):
    w, s, e, n = bbox
    q = f"""[out:json][timeout:180];
(way({s},{w},{n},{e})[highway];way({s},{w},{n},{e})[aeroway];way({s},{w},{n},{e})[building];
 way({s},{w},{n},{e})[amenity=parking];way({s},{w},{n},{e})[barrier];way({s},{w},{n},{e})[man_made];
 node({s},{w},{n},{e})[highway];node({s},{w},{n},{e})[aeroway];node({s},{w},{n},{e})[amenity];
 node({s},{w},{n},{e})[man_made];node({s},{w},{n},{e})[barrier];);out geom;"""
    req = urllib.request.Request(url, data=("data=" + urllib.request.quote(q)).encode(),
                                 headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.load(r)["elements"]


BAND_BBOX = (138.462, -35.005, 138.598, -34.894)   # W,S,E,N: the whole suburb band the game draws
BUILDING_TAGS = {"building", "building:levels", "height", "min_height", "roof:shape", "roof:levels", "roof:height",
                 "roof:colour", "building:colour", "building:part", "name", "amenity", "shop", "tourism", "layer",
                 "aeroway", "man_made", "operator"}
STREET_TAGS = {"highway", "name", "oneway", "lanes", "width", "surface", "service", "junction", "bridge", "tunnel",
               "layer", "access", "maxspeed", "ref"}


def fetch_band(out_dir, keep_xml):
    """Every building and every highway over the suburb band, in the formats generate-adelaide-suburbs.py reads."""
    w0, s0, e0, n0 = BAND_BBOX
    nodes, ways = {}, {}
    lon, tiles = w0, 0
    while lon < e0:
        lat = s0
        while lat < n0:
            fetch_tile(lon, lat, min(lon + TILE_LON, e0), min(lat + TILE_LAT, n0), nodes, ways, keep_xml)
            tiles += 1
            lat += TILE_LAT
        lon += TILE_LON
        print(f"band: {tiles} tiles, {len(ways)} ways so far", file=sys.stderr)
    today = datetime.date.today().isoformat()
    buildings, streets = [], []
    for wid, w in sorted(ways.items(), key=lambda kv: int(kv[0])):
        geom = [[nodes[r]["lon"], nodes[r]["lat"]] for r in w["nds"] if r in nodes]
        if len(geom) < 2:
            continue
        t = w["tags"]
        if t.get("building") and t["building"] != "no" and len(geom) >= 4:
            buildings.append({"id": int(wid), "tags": {k: v for k, v in t.items() if k in BUILDING_TAGS}, "geometry": geom})
        elif "highway" in t:
            streets.append({"id": int(wid), "tags": {k: v for k, v in t.items() if k in STREET_TAGS}, "geometry": geom})
    head = {"source": "OSM API 0.6 /map, tiled", "osm_base": today, "query": "bbox {:.3f},{:.3f},{:.3f},{:.3f}".format(*BAND_BBOX)}
    for name, key, rows in (("adelaide-suburb-buildings", "buildings", buildings), ("adelaide-suburb-streets", "streets", streets)):
        path = os.path.join(out_dir, f"{name}-{today}.json")
        with open(path, "w", encoding="utf-8") as f:
            f.write(json.dumps({**head, key: rows}, separators=(",", ":"), ensure_ascii=False) + "\n")
        print(f"wrote {os.path.relpath(path, ROOT)}: {len(rows)} {key}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--band", action="store_true", help="fetch the whole suburb band (buildings + streets) instead")
    ap.add_argument("--bbox", help="W,S,E,N in degrees")
    ap.add_argument("--out")
    ap.add_argument("--overpass", help="Overpass interpreter URL (e.g. https://overpass-api.de/api/interpreter)")
    ap.add_argument("--keep-xml", help="also keep each raw API tile here (not committed)")
    args = ap.parse_args()
    if args.band:
        fetch_band(os.path.join(ROOT, "docs/data/osm"), args.keep_xml)
        return
    bbox = tuple(float(v) for v in args.bbox.split(",")) if args.bbox else DEFAULT_BBOX
    today = datetime.date.today().isoformat()
    out = args.out or os.path.join(ROOT, f"docs/data/osm/ypad-map-{today}.json")

    elements = []
    if args.overpass:
        for el in fetch_overpass(args.overpass, bbox):
            if el["type"] == "way" and keep_way(el.get("tags", {})):
                elements.append({"type": "way", "id": el["id"], "tags": clean(el["tags"]),
                                 "geometry": [{"lat": p["lat"], "lon": p["lon"]} for p in el["geometry"]]})
            elif el["type"] == "node" and any(k in KEEP_NODE_KEYS for k in el.get("tags", {})):
                elements.append({"type": "node", "id": el["id"], "tags": clean(el["tags"]),
                                 "lat": el["lat"], "lon": el["lon"]})
    else:
        w0, s0, e0, n0 = bbox
        nodes, ways = {}, {}
        lon = w0
        tiles = 0
        while lon < e0:
            lat = s0
            while lat < n0:
                fetch_tile(lon, lat, min(lon + TILE_LON, e0), min(lat + TILE_LAT, n0),
                           nodes, ways, args.keep_xml)
                tiles += 1
                lat += TILE_LAT
            lon += TILE_LON
        print(f"fetched {tiles} tiles: {len(nodes)} nodes, {len(ways)} ways", file=sys.stderr)
        used = set()
        for wid, w in ways.items():
            if not keep_way(w["tags"]):
                continue
            geom = [nodes[r] for r in w["nds"] if r in nodes]
            if len(geom) < 2:
                continue
            used.update(w["nds"])
            elements.append({"type": "way", "id": int(wid), "tags": clean(w["tags"]),
                             "geometry": [{"lat": p["lat"], "lon": p["lon"]} for p in geom]})
        for nid, nd in nodes.items():
            t = nd["tags"]
            if t and any(k in KEEP_NODE_KEYS for k in t) and w0 <= nd["lon"] <= e0 and s0 <= nd["lat"] <= n0:
                elements.append({"type": "node", "id": int(nid), "tags": clean(t),
                                 "lat": nd["lat"], "lon": nd["lon"]})

    elements.sort(key=lambda e: (e["type"], e["id"]))
    doc = {
        "version": 0.6,
        "generator": "Airside scripts/fetch-ypad-osm.py",
        "copyright": "OpenStreetMap contributors",
        "license": "https://opendatacommons.org/licenses/odbl/1-0/",
        "source_bbox": "{:.4f},{:.4f},{:.4f},{:.4f}".format(*bbox),
        "source": "Overpass" if args.overpass else "OSM API 0.6 /map",
        "retrieved": today,
        "elements": elements,
    }
    text = json.dumps(doc, separators=(",", ":"), ensure_ascii=False) + "\n"
    with open(out, "w", encoding="utf-8") as f:
        f.write(text)
    sha = hashlib.sha256(text.encode("utf-8")).hexdigest()
    print(f"wrote {os.path.relpath(out, ROOT)}: {len(elements)} elements, {len(text) / 1e6:.2f} MB, sha256 {sha}")


if __name__ == "__main__":
    main()
