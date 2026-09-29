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

## Phase log

- **P1a (data).** `scripts/generate-ypad-roads.py` → `Simulation/AdelaideRoadNetwork.cs`: 3,849 roads (511 km, 97
  airside inside the aerodrome outline), 4,487 junctions, 2,162 furniture points (crossings with the width of the
  road they sit on, signals, give-way, bus stops, gates, turning circles). Junction vertices survive simplification
  so roads meet exactly; the existing service-road data is reproduced to ~2 m.
- **P1b (mesh).** `Presentation/AdelaideRoadGeometry` (pure, headless-tested) builds ribbons at real width that follow the
  ground, with mitred bends; a bend sharper than 60° — or one where the inside edge would slide past the shorter
  segment — cuts the ribbon and joins it with a disc, and a road no bigger than two of its widths is a disc (cul-de-sac
  loops), so no triangle faces down (checked over 118k). Junction discs, turning circles, dashed/solid lane and edge
  paint cut back from side roads (`RoadMarkingPlan.ForRoad`: uses tagged lanes and one-way), zebra crossings (1,069).
  Airside roads are dark asphalt off the pavement, paint-only on apron concrete, and skipped on runways/taxiways.
  `AirsideAdelaideRoadNetworkMesh` copies 1.5 km tiles into meshes. The old arterial ribbons now only draw beyond the
  network's OSM window (cap 2,400); while the network draws, the authored T1 ribbons/car pad/props are off (they no
  longer match the real roads) until P1c replaces them with data-driven cars and lamps. `-airsideLegacyRoads` restores
  the old roads.
- **Test harness.** `scripts/dotnet-harness` had drifted (three tests needed excludes/includes). Fixed; the .NET 8 SDK
  installs with `dotnet-install.sh --channel 8.0` even where apt is unavailable. Headless: 1,033 passed, 0 failed.
- **P1c (landside life).** `scripts/generate-ypad-carparks.py` → `Simulation/AdelaideCarParks.cs`: 592 car-park outlines,
  12,959 bays (rows either side of each `parking_aisle`, inside the outline, clear of other roads and buildings, plus every
  mapped `parking_space`), 471 street lamps. `AdelaideCarParkGeometry` (pure): ear-clipped dark surfaces, white bay
  lines, up to 4,200 parked cars within 2.3 km of the T1 forecourt (62 % occupancy and colour by hash, so identical every
  run; body + glass + roof, flat-shaded) and lamp posts. Night glows for lamps within 2.2 km join the shared
  "Streetlights" lens group. This replaces the authored T1 car pad, cars, lamps, zebras and bay lines.
