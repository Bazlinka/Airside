# YPAD surroundings plan — making the ground look like Adelaide Airport

Status: proposal for Bailey · 2026-09-14 · Claude (research only, no code changed)

## Where we are

- `scripts/generate-ypad-layout.py` turns the OSM snapshot
  `docs/data/osm/ypad-aeroways-2026-09-14.json` into `Simulation/AdelaideLayout.cs`
  (runways, taxiways, aprons, terminal footprints, baked routes).
- `Presentation/AirsidePrototype.YpadPavement.cs` draws those as flat ribbons and prisms
  (terminals are 14 m grey boxes) over `AirsideAdelaideGroundMesh`: 3 900 × 2 800 m,
  CC0 grass/dirt blend, 6 m relief budget, dead-level plateau.
- Past the ground edge there is nothing: sky, fog, `CameraFarClip` 10 km,
  `MaxOrbitDistance` 4.5 km. No coast, no suburbs, no Hills, no roads, no car parks.
- `AdelaideLayout.Attribution` exists as a constant but is **not shown anywhere on screen**
  yet. That is already an ODbL gap before we add anything.

Why it looks bare: from the overview camera the airfield is a grey stencil on a green
card floating in fog. A real YPAD overview reads as "Adelaide" from three things:
the sea 3 km west, the dense low suburbs and road grid all round, and the Hills
wall to the east. None of those need photos.

## 1. Options, honestly

### (a) Aerial / satellite imagery draped on the ground

| Source | Resolution | Licence for a shipped game | Verdict |
|---|---|---|---|
| Google / Bing / Apple / Mapbox satellite | 10–30 cm | ToS forbid bulk download, caching, offline use and redistribution ([Google Map Tiles policies](https://developers.google.com/maps/documentation/tile/policies)) | **No** |
| Esri World Imagery | 30 cm | Exports only for use inside ArcGIS apps ([Esri Wayback Export](https://www.esri.com/arcgis-blog/products/arcgis-living-atlas/imagery/wayback-export)) | **No** |
| Nearmap / Vexcel / Aerometrex | 5–7.5 cm | Commercial subscription, no redistribution | **No** (cost + licence) |
| SA Government (DEW Mapland / Location SA) modern ortho | ~10–30 cm | **Uncertain.** The Location SA *viewer site* is CC BY 4.0 ([Location SA Viewer](https://location.sa.gov.au/viewer/)) and data.sa.gov.au content is CC BY with a set attribution form ([Data.SA copyright](https://data.sa.gov.au/copyright)), but the only openly listed Adelaide ortho I could find is the **1949 black-and-white** mosaic ([dataset](https://data.sa.gov.au/data/dataset/aerial-imagery-greater-adelaide-1949)). Current metro imagery appears to be supplied on request through Mapland ([DEW aerial photography](https://www.environment.sa.gov.au/topics/science/mapland/aerial-photography)); I could not confirm its licence. | **Maybe — ask Mapland in writing** |
| Sentinel-2 via Digital Earth Australia (geomedian / surface reflectance) | 10 m | CC BY 4.0 (DEA) on top of Copernicus free/open data ([DEA S2](https://knowledge.dea.ga.gov.au/notebooks/DEA_products/DEA_Sentinel2_Surface_Reflectance/), [ESA open licence](https://open.esa.int/copernicus-sentinel-satellite-imagery-under-open-licence/)) | **Yes, legally clean.** Too coarse near-field (a 45 m runway is 4–5 px) but fine as low-frequency colour for the 3–20 km ring |

Look-wise, raw photo on a flat plane is the main "cringe" risk: baked shadows fight
our sun, parked cars and aircraft are painted on the ground, and every seam between a
10 m tile and our crisp CC0 grass is visible. Imagery is useful only as **muted albedo
tint**, never as the thing you look at.

### (b) Google Photorealistic 3D Tiles via Cesium for Unity

- Works in Unity ([Cesium for Unity tutorial](https://cesium.com/learn/unity/unity-photorealistic-3d-tiles/)),
  YPAD is covered, and it would look "real" from 2 km up.
- **Online-only**: no caching, no offline use, no extraction
  ([policies](https://developers.google.com/maps/documentation/tile/policies),
  [Cesium's Google terms](https://cesium.com/legal/terms-for-google/)). A Mac game that
  must launch offline cannot depend on it.
- Billed per root-tileset session; Cesium ion bundles ~1 000 free sessions/month, then
  paid ([Cesium pricing](https://cesium.com/platform/cesium-ion/pricing/); exact
  commercial numbers unconfirmed). Every player launch is a billable session.
- Mandatory on-screen Google attribution/logo.
- Look: melted photogrammetry — blobby aircraft baked onto aprons, wobbly hangars,
  lighting locked to the capture day. It clashes with our own aircraft, shadows,
  day/night cycle and weather. Near the camera it is the uncanny valley in its purest
  form. **Reject** (keep as a private reference tool at most, not in builds).

### (c) Stylised procedural from OSM — recommended backbone

OSM (ODbL, already in the register) has what we need around YPAD: `landuse`
(residential, commercial, retail, industrial, grass, recreation_ground),
`natural=water/coastline/beach/scrub`, `leisure=park/golf_course` (West Beach and
Glenelg golf courses), `amenity=parking` (the big long-term car parks), `highway=*`
(Sir Donald Bradman Dr, Tapleys Hill Rd, Burbridge Rd, Anzac Hwy, Airport Rd),
`building=*` with some `height`/`building:levels` (terminal, Harbour Town / DFO
retail, hangars, Pier/Holdfast Shores towers at Glenelg). Missing heights get
rule-based defaults (house 5 m, retail 8 m, hangar 12 m).
It is deterministic, offline, cheap to render, and can be styled to the approved
`AirsideTheme` palette (Tarmac, Concrete, DryGrass, Eucalyptus, Sand, CoastalBlue).
Weak spot: OSM house footprints in the suburbs are incomplete — fill residential
landuse with a low "roof texture" rather than millions of houses.

### (d) Elevation

- Plains around YPAD are ~5–15 m AMSL and flat; the airfield itself should stay
  dead-flat (existing plateau rule).
- The Hills backdrop (Mount Lofty 727 m, ~16 km east) needs only a coarse DEM:
  Geoscience Australia 1-second SRTM-derived DEM-H or the 5 m LiDAR national grid,
  both CC BY 4.0 via ELVIS ([GA elevation data](https://www.ga.gov.au/scientific-topics/national-location-information/digital-elevation-data),
  [ELVIS catalog on Data.SA](https://data.sa.gov.au/data/dataset/elvis-digital-elevation-model-imagery-catalog)).
  1 m LiDAR is pointless at 15 km — decimate to ~90–120 m cells.
- Licence per individual ELVIS tile should still be checked at download (some
  jurisdictional tiles carry their own terms).

### (e) Street View — no

Bulk download/scraping is prohibited and imagery cannot leave Google's services
([Street View policies](https://developers.google.com/maps/documentation/streetview/policies)).
It is also the wrong viewpoint: our camera is an orbit/follow view from 20 m–4.5 km
up, Street View is 2.5 m eye height on public roads outside the fence. Use it (in a
browser, by a human) only as **visual reference** for colours and building shapes,
never as game data or texture.

## 2. Recommendation — "grounded stylised", not photoreal

1. **Near field (the airport, 0–2 km):** keep our CC0 materials and real OSM pavement.
   Add OSM grass areas, landside roads, car parks, terminal/hangar/cargo buildings
   extruded to real-ish heights, flat roofs in Concrete/Cloud tones. No photo here.
2. **Mid field (2–6 km):** OSM land-cover polygons as flat coloured regions with a
   subtle tileable noise texture, main arterial roads as ribbons, extruded buildings
   only for landmarks (Harbour Town, Glenelg towers, West Beach Surf Life Saving area,
   Adelaide Shores). Residential blocks get a single low roof-scatter texture, not
   individual houses.
3. **Sea and coast (west, ~3 km):** OSM coastline → Gulf St Vincent water plane in
   `CoastalBlue` running to the horizon, sand strip in `Sand`, Glenelg jetty as a
   thin line. This one element does the most to make it read as Adelaide.
4. **Far field (6–25 km):** DEM-driven Hills silhouette as a low-poly ring mesh,
   coloured by distance fog into a blue-grey haze; CBD as a small cluster of grey
   boxes ~8 km ENE. No texture detail — silhouette only.
5. **Imagery:** optional, last. If (and only if) a licence is confirmed, use a DEA
   Sentinel-2 geomedian as a *desaturated, blurred, palette-graded* colour multiplier
   on mid/far land cover, faded out inside 2 km. Never full-strength, never near field.

What avoids cringe: one consistent palette and lighting model across near and far;
detail that falls off smoothly with distance (fog/aerial perspective hides the
transition instead of a seam); no baked shadows or painted-on vehicles; real
*placement* (where the sea, the car parks and the Hills are) matters more than
real *pixels*.

## 3. Phased PRs

Each PR: `feature/ypad-surroundings-<n>`, one acceptance criterion, GAME.md + CHANGELOG
in the same commit, `scripts/test-unity.sh` green, `scripts/build-mac.sh` succeeds.
Keep the existing bare ground as fallback when generated data is missing.

**Acceptance cameras** (add to a small `SurroundingsCameraShots` table in Presentation
and a Dev Tools (F8) "shot" button): S1 overview high from SW over the 05 threshold
looking NE (Hills in frame); S2 overview from E over terminal looking W (sea in
frame); S3 follow camera on BAY-2 at 60 m; S4 top-down 4 km altitude centred on the
runway midpoint. Evidence = packaged-build screenshots at S1–S4 placed side-by-side
with a reference aerial (Google Earth / Location SA viewer screenshot kept **only in
`work/`, never committed**), plus a frame-time readout.

**Performance budget (Mac, High ladder):** surroundings add ≤ 1.5 ms CPU and ≤ 2 ms
GPU per frame at S1, ≤ 150 k extra triangles, ≤ 60 draw calls (combine per material),
≤ 96 MB extra texture memory, ≤ 0.5 s extra startup. Nothing generated at startup
beyond mesh upload (lesson from `AirsideTerrainGround`).

- **P0 — Attribution first.** Show "Map data © OpenStreetMap contributors" in a corner
  of the overview and the map, and in credits. Test: string present in scene index.
  *Evidence:* screenshot S1 with credit legible.
- **P1 — Shared frame + surroundings snapshot.** Extract `xy()/local()` from
  `generate-ypad-layout.py` into `scripts/ypad_frame.py` (same `LAT0/LON0`, same
  runway U/N/MID), prove `AdelaideLayout.cs` regenerates byte-identical. Commit the
  second Overpass snapshot (§4) + SHA-256 + register row DAT-YPAD-OSM-SURROUND.
  *Evidence:* `git diff` of AdelaideLayout.cs empty; Python test round-trips 05/23
  thresholds to ±0.1 m.
- **P2 — Coast and sea.** Generator emits `Simulation/AdelaideSurroundings.cs`
  (UnityEngine-free: coastline polyline, sea polygon, beach polygon, in runway frame).
  Presentation builds a water plane to 25 km plus sand strip. Raise `CameraFarClip`
  only if needed; prefer fog. EditMode test: coastline lies 2.5–3.5 km west of runway
  midpoint and never intersects the airfield plateau. *Evidence:* S2, S4 vs reference.
- **P3 — Land cover.** Landuse/leisure/natural/parking polygons, triangulated in the
  generator (ear clipping in Python, emit triangles, not raw polygons, so C# stays
  dumb), 6 palette classes, one atlas material with world-UV noise. Clip to a
  6 × 6 km box. Test: class coverage percentages stable ±2 % vs snapshot.
  *Evidence:* S4 overlay — golf courses, car parks, Harbour Town site in the right place.
- **P4 — Roads.** Motorway/trunk/primary/secondary as ribbons (width by class),
  Sir Donald Bradman Dr and Tapleys Hill Rd named in a test. No traffic yet.
  *Evidence:* S1, S4.
- **P5 — Buildings.** Extrude `building=*` within 2 km (all) and 2–6 km (landmarks +
  anything ≥ 3 levels); real terminal footprint replaces the 14 m box with OSM
  `height`/levels where present. Flat roofs, window-band shader in palette.
  Test: triangle count under budget; terminal height within 3 m of tag.
  *Evidence:* S2, S3.
- **P6 — Hills and CBD backdrop.** DEM (ELVIS/GA, CC BY 4.0) resampled to ~100 m grid
  6–25 km east, baked to a committed low-poly mesh asset (not runtime); CBD cluster
  from OSM buildings ≥ 30 m. Register row DAT-YPAD-DEM. Fog tuned so it is a
  silhouette at dusk. *Evidence:* S1 at noon and dusk vs reference photo from the
  terminal side.
- **P7 — Optional imagery tint.** Only after written licence confirmation. DEA
  Sentinel-2 geomedian (CC BY 4.0), resampled in the generator into runway-frame
  pixels, graded to palette, 1 × 2048² texture for 12 × 12 km, faded out < 2 km.
  Behind a Dev Tools toggle until Bailey approves. *Evidence:* S1/S4 with tint on/off.

Each generator step writes a report line (counts, areas, triangle totals) like the
existing layout script, so reviewers can check scale without Unity.

## 4. Data pipeline specifics

**Frame.** Match `generate-ypad-layout.py` exactly: equirectangular about
`LAT0 = -34.95, LON0 = 138.53` (`x = Δlon·111320·cos(LAT0)`, `z = Δlat·110574`), then
translate to the 05/23 threshold midpoint and rotate onto `U` (05→23, ≈ 40° north of east)
and `N` (left of U, terminal side). Unity world x = runway-frame x, z = runway-frame z.
The flat-earth error is < 1 m inside the airfield and ~10–30 m at 15–20 km — fine for a
backdrop. Because the frame is rotated, anything raster (DEM, imagery) must be
**resampled into runway-frame axes in the generator** so Unity UVs stay axis-aligned.

**Overpass snapshot** (bbox widened west to catch the sea and east for the CBD edge;
the given -34.975..-34.915 / 138.49..138.57 box clips the coast):

```
[out:json][timeout:180][bbox:-34.995,138.46,-34.895,138.62];
(
  way["landuse"]; relation["landuse"];
  way["leisure"~"park|golf_course|pitch|recreation_ground|nature_reserve"];
  way["natural"~"water|beach|sand|scrub|wetland|coastline"];
  relation["natural"="water"];
  way["amenity"="parking"];
  way["highway"~"motorway|trunk|primary|secondary|tertiary|residential|service"];
  way["building"]; relation["building"];
  way["man_made"="pier"];
);
out geom;
```

Sea: build from `natural=coastline` ways, closed against the bbox west edge (the
standard OSM coastline convention: land on the left). Record the OSM base timestamp,
SHA-256 and query text in the register, same pattern as DAT-YPAD-OSM. Expect a
several-MB JSON; if > 20 MB, commit a filtered/simplified snapshot and the exact
filter script instead of the raw dump.

**DEM.** Download GA DEM-H 1-second (or ELVIS 5 m) for
-35.05..-34.80 / 138.45..138.85 as GeoTIFF into `work/` (git-ignored); generator reads
it with a pure-Python/GDAL step, resamples to 100 m in runway frame, subtracts the
airfield datum, clamps ≤ 0 near field, writes a committed `.asset`/binary heightfield
(~250 × 250 = 62 k samples) + SHA of the source.

**Unity geometry.**
- Mesh, not Unity Terrain, for everything. Terrain's heightmap is axis-aligned and
  square, wasteful for a mostly flat 12 km plain and the rotated frame; the existing
  project already hit Terrain memory issues with static batching.
- Near/mid land cover: generated flat meshes chunked into ~1 km tiles, one material
  per class family via vertex colour + atlas, `MeshRenderer` with LOD-less but
  distance-culled chunks; static-combine only the small chunks (not the big ones).
- Far: one Hills ring mesh (~20 k tris) and one sea plane, fog-coloured.
- Textures: tileable noise/detail at 512–1024², mips on, anisotropic 8 (matches
  `AirsideRuntimeQuality.AnisoLevel`). Optional tint texture 2048², mipmap streaming
  enabled, ASTC/BC7 compression. No 16k satellite mosaics.
- Z-fighting: land cover sits below `AirsideAdelaideGround.PavementWorldY` like the
  shoulders already do; OSM land cover is clipped out of the airfield ground rectangle
  so there is only one owner per square metre.

**Attribution UI.** One small persistent line bottom-right of overview + route map +
credits screen, composed from all active sources: "Map data © OpenStreetMap
contributors · Elevation © Geoscience Australia (CC BY 4.0)" and, if P7 lands,
"Contains modified Copernicus Sentinel data, processed by Digital Earth Australia".
Pure string composition lives in a UnityEngine-free helper with a test.

## 5. Risks and open questions for Bailey

1. **Do you want Glenelg/Hills visible at all zoom levels**, or is the overview camera
   the only place surroundings matter? (Decides whether P5/P6 are worth it.)
2. **Imagery licence:** OK to email DEW Mapland to ask about current metro ortho
   licence terms for a game? Until answered, the plan assumes no photo imagery.
3. **ODbL obligations** grow as we ship more OSM-derived data; a credits screen is
   needed before any public build. Derived database vs produced work split should be
   noted in an ADR ([OSMF attribution guidelines](https://osmfoundation.org/wiki/Licence/Attribution_Guidelines)).
4. **Scope vs PROJECT_PLAN order:** this is presentation polish; confirm it should
   not jump ahead of first-session/trust work.
5. **Performance on the Mac build** is the hard constraint; if P3–P5 blow the budget,
   the fallback is coast + land-cover colours + Hills only (P2, P3, P6).
