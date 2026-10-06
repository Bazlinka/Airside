# ADR 0236: Map accuracy from open Australian data

Date: 2026-10-06. Owner: Claude. Request: use the best open map data available, for private non-commercial play.

## Decision

Replace the guessed parts of the suburbs around Adelaide Airport with measured data, all open-licensed and
recorded in the asset register (DAT-YPAD-OVERTURE, DAT-SA-LIDAR-BUILDINGS, DAT-SA-LIDAR-CANOPY):

- **Buildings.** Overture Maps buildings (ODbL; OSM + Microsoft ML footprints) join the OpenStreetMap
  footprints in `generate-adelaide-suburbs.py`. A footprint overlapping an OSM building by more than 30 % of the
  smaller one is the same building and OSM wins (so its tags and heights are kept). Microsoft-only footprints are
  kept only when the Government of South Australia 2022 LiDAR building raster (0.5 m, CC BY 4.0) confirms a roof
  (`prepare-ypad-overture-buildings.py`; 282 of ~32,000 failed). Footprints inside the aerodrome stay with
  `AdelaideBuildings`. Real houses in the game's suburbs rise from 4,523 to 17,618; invented street-front filler
  houses fall from 8,880 to 660 and remain only where neither source has a roof.
- **Trees.** `generate-adelaide-trees.py` takes crowns from the SA 2022 LiDAR canopy height model (0.5 m,
  centimetres): each local height maximum is a tree with its measured height and a watershed-derived crown radius,
  instead of a random 7-14 m tree on each NDVI-green 10 m pixel. 62,724 crowns; 35,166 survive the existing
  building/road/water/airside rejection and the distance taper (budget 40,000). `--ndvi` keeps the old path.
- **OSM refresh.** `ypad-map-2026-10-06.json` replaces 2026-09-29 for roads, car parks, precinct furniture and golf
  bunkers (+50/-11 elements, 75 edits; airside aeroways unchanged).
- **Terrain hook.** `generate-adelaide-terrain.py --elvis DIR` blends 1 m / 5 m LiDAR DTM tiles downloaded from
  Geoscience Australia's ELVIS portal (CC BY) over the Copernicus 30 m DEM wherever they cover. The download
  needs a manual ELVIS order, so the shipped terrain is unchanged until tiles are supplied.

## What was tried and rejected

- The SA LiDAR building raster as an outline source: it merges terraces and sheds into block-sized blobs and
  carries false returns over the sea (about 220 "buildings" off Henley Beach). It is a validator only.
- City of Adelaide 3D model (CC BY): a 2016 Collada mesh needing manual Z correction; not worth the CBD gain.
- Airservices Australia charts: © Airservices Australia, no reuse licence; manual reference only.

## Affected systems and migration

`osm_adelaide_suburbs_v01.bin` (same ASUB v1 format; 15,004 -> 21,737 buildings) and `adelaide_trees_v01.bin`
(same ATRE v1 format; heights 3-24 m, crowns 1.8-6.5 m) are regenerated; both StreamingAssets mirrors resynced.
No schema, save or Domain change. Tests widened: suburb tree count 3k-45k, tree height/crown ranges, filler houses
> 300, stranded-from-road tolerance 1/300. Large raw inputs stay in git-ignored `work/cache/`; the committed
Overture snapshot is the band-trimmed, LiDAR-validated `docs/data/overture/ypad-suburb-buildings-*.json.gz` (2.2 MB).

## Evidence and limits

`scripts/test-domain.sh`: 1508 passed, 0 failed. Not yet checked in a native Unity build or at the follow/overview
cameras: more buildings and trees mean more mesh, so frame time needs a Mac run before merging.
