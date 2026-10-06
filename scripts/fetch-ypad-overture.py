#!/usr/bin/env python3
"""Fetch Overture Maps buildings around Adelaide Airport and its suburbs (map accuracy pass, ADR 0236).

Overture's buildings theme merges OpenStreetMap, Microsoft ML footprints, Google Open Buildings and Esri
Community Maps. Each feature carries `height`, `num_floors`, `class` and per-source provenance where known.
Licence: ODbL 1.0 for the buildings theme (the OSM-derived share keeps "© OpenStreetMap contributors"; the
Microsoft/Google/Esri shares are also published under ODbL/CDLA terms — see docs/data/ASSET_AND_DATA_REGISTER.md).

Reads GeoParquet straight from the public S3 bucket with DuckDB (bbox pushdown, no full download).

Run:  work/venv/bin/python scripts/fetch-ypad-overture.py [--release 2026-09-23.1] [--bbox W,S,E,N]
Writes work/cache/overture/ ypad-buildings-<release>.json (git-ignored, 35 MB, pinned by release):
  {"release", "bbox", "licence", "features": [{"id","height","floors","class","subtype","sources":[dataset,...],
                                                "ring": [[lon,lat],...]}]}   (outer ring only, 6 dp)
"""
import argparse
import datetime
import json
import os
import sys

import duckdb

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_RELEASE = "2026-09-23.1"
DEFAULT_BBOX = (138.46, -35.00, 138.62, -34.89)   # airport + the suburbs the game draws


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--release", default=DEFAULT_RELEASE)
    ap.add_argument("--bbox", default=",".join(map(str, DEFAULT_BBOX)))
    args = ap.parse_args()
    w, s, e, n = (float(v) for v in args.bbox.split(","))
    con = duckdb.connect()
    con.execute("install httpfs; load httpfs; install spatial; load spatial; set s3_region='us-west-2'")
    path = f"s3://overturemaps-us-west-2/release/{args.release}/theme=buildings/type=building/*"
    rows = con.execute(f"""
        select id, height, num_floors, class, subtype, sources, ST_AsGeoJSON(geometry) as g
        from read_parquet('{path}', hive_partitioning=1)
        where bbox.xmin > {w} and bbox.xmax < {e} and bbox.ymin > {s} and bbox.ymax < {n}
    """).fetchall()
    feats = []
    for fid, h, fl, cls, sub, srcs, g in rows:
        geom = json.loads(g)
        ring = geom["coordinates"][0] if geom["type"] == "Polygon" else geom["coordinates"][0][0]
        feats.append({
            "id": fid, "height": h, "floors": fl, "class": cls, "subtype": sub,
            "sources": sorted({x["dataset"] for x in (srcs or [])}),
            "ring": [[round(x, 6), round(y, 6)] for x, y in ring],
        })
    out = os.path.join(ROOT, "work/cache/overture", f"ypad-buildings-{args.release}.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        json.dump({"release": args.release, "bbox": [w, s, e, n], "retrieved": datetime.date.today().isoformat(),
                   "licence": "ODbL 1.0 (Overture buildings theme)", "features": feats}, f, separators=(",", ":"))
    with_h = sum(1 for x in feats if x["height"])
    print(f"{len(feats)} buildings, {with_h} with height -> {out}")


if __name__ == "__main__":
    sys.exit(main())
