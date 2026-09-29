# 0184 — Adelaide map overhaul: vector-first from OpenStreetMap

Date: 29 September 2026. Author: Claude, from Bailey's request for a top-down Adelaide Airport as it is today,
with complete roads and accurate terminal detail.

## Decision

- **Vector-first from OpenStreetMap** (ODbL 1.0, already used by the layout, buildings, boundary, land cover and
  service-road data). No commercial aerial imagery: Google/Bing/Nearmap terms forbid redistribution in a shipped
  game (ADR 0157). Sentinel-2 stays as far-field tint only.
- **Scope: the whole airport and its landside** — airside pavements and roads, all public roads, car parks, the
  Terminal 1 precinct, precincts and furniture. Delivered in phases (plan: P0 data, P1 roads, P2 terminal,
  P3 airside detail, P4 verification and credits).
- **Data access.** Overpass and Geofabrik are unreachable from the cloud sandbox; the OSM map API
  (`api.openstreetmap.org/api/0.6/map?bbox=`) is. `scripts/fetch-ypad-osm.py` tiles it (splitting any refused
  tile) and also supports `--overpass` for Bailey's machine. Output: `docs/data/osm/ypad-map-<date>.json`.
- **Frame.** `scripts/ypad_osm.py` holds the projection, identical to `generate-ypad-layout.py` and
  `YpadFrame.cs`, plus resample and simplify helpers. Generators stay standard-library only.
- **Route-critical geometry is not touched.** `AdelaideLayout` (taxiways, stands, ground routes) keeps driving
  aircraft; the overhaul changes ground and visual layers, and existing pavement/accuracy tests must stay green.

## Why the roads are partial today

`AirsideAdelaideRoads` draws only the 720 longest of the motorway→secondary ribbons, at 9 m and wider, and skips
every point inside the operational core; the airside service roads are never drawn; suburb streets exist only as
satellite pixels. The new snapshot carries 260 km of residential, 140 km of service, 27 km of tertiary road and
188 road ways inside the airfield core, plus street lamps, gates, jet bridges, crossings, bus stops and car parks.

## Migration

None: new data file and scripts only; no persisted schema changes.
