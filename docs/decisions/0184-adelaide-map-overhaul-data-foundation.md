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
- **P2/P3 (precinct).** `scripts/generate-ypad-precinct.py` → `Simulation/AdelaidePrecinct.cs`; `AdelaidePrecinctGeometry`
  (pure): open canopies on posts (the taxi rank and bus/car-park entrance roofs used to draw as solid 2.6 m blocks from the
  suburb extract, burying the road and cars; `AirsideAdelaideSuburbs.AppendBuilding` now skips a prism that is a canopy),
  107 solar arrays (blue panels; on the canopy roof where the roof is mapped), 41 fuel tanks, 70 masts (the T1 apron
  floodlights, VOR/DME, localizer, comms), 130 bus stops (shelter or pole and sign, turned to the road), 773 footpaths.
  Gates and jet bridges were cross-checked against OSM: the game's gate noses agree within the deliberate ADR 0141 setback
  (0–20 m), so they are left as they are.
- **Mini-map, credits, load cost.** `FieldMiniMap.Bake` paints car parks and every road (three passes: minor, major,
  airside) under the pavement. The Flight Manual credits line names the new data. The geometry measured 295 ms and 367k
  triangles on .NET, so `AirsideAdelaideRoadNetworkMesh.BuildAsync` builds it on a worker thread (pure maths over static
  data) and copies tiles into meshes 3 ms a frame; cars are two boxes each (about 168k vertices). If the build fails,
  `FallBackToLegacyRoads` restores the old ribbons and the authored T1 side.
- **One road system (code-quality pass).** `generate-ypad-roads.py` also takes the arterials beyond the full-detail window from
  the land-cover snapshot (motorway to secondary, 3,601 ways, ids already in the map snapshot skipped), so
  `AdelaideRoadNetwork` holds 6,735 roads (781 km) out to about 6.5 km. The old capped arterial ribbons
  (`AirsideAdelaideRoads`), the authored T1 traffic side (`AdelaideLandside`, `AirsideAdelaideLandside`,
  `BuildYpadLandsideLife`), `AdelaideLandCover.Roads` (1,680 lines of packed data) and the `-airsideLegacyRoads` flag and
  fallback are gone.
- **State and size (code-quality pass).** The pavement rule the road build asks about is now one instance per build (no shared
  static cache or bounds), and lazily loaded data is touched on the main thread before the worker starts.
  `AdelaideRoadNetwork.cs` is 318 KB instead of 1.36 MB: the tables are delta-encoded 0.1 m integers, raw-deflated and
  base64'd, decoded once at start-up (`PointChecksum` and a test prove it decodes whole).
  `AirsidePrototype._airfieldRoot` and the other statics of the prototype are left for the Unity-verified split.
- **Performance pass (no visual change).** Measured on the real DEM: cutting public roads every 12 m made 71,897 ribbon
  segments; splitting only where the ground bends more than 1.5 cm off the chord (`AdaptiveDensify`) makes 23,683 (33 %),
  with the ribbon within 1.5 cm of the 12 m version. Paint lift rose from 2 to 3 cm so paint always clears the ribbon.
  Road/car/prop meshes use a compact vertex layout (position floats, normal 4 x sbyte, colour 4 x unorm16: 24 bytes, or 16
  without colour, against 40) and `UploadMeshData(true)` frees the CPU copy: about half the memory and bandwidth. If a
  platform rejects the layout the tile falls back to the plain one. Still to measure in Unity: frame time with
  `-airsideSoak`.
- **Bridges and packed layout.** Drivable bridges (the `bridge` tag above layer 0) now rise from the ground at both ends to
  a smooth peak (0.035 x length, 0.6 to 3 m) with a 1 m concrete parapet along both edges; the paint follows the same
  profile (`BridgeProfile`, one lifted options object for ribbon and paint). Footpath bridges are not lifted.
  `AdelaideLayout.cs` shrank from 1.09 MB to 195 KB: every one of its 240 route arrays is stored as 0.1 m zigzag-varint
  deltas in base64 (`LayoutPack.Xz`), verified identical to the old arrays value by value.
