# 0159 — The suburbs around the airfield as buildings

Date: 28 September 2026. Author: Claude, at Bailey's request ("improve the surroundings … scenery,
map data, realism"). Plan `docs/plans/ypad-surroundings-plan.md` P5.

## Context

Outside the airport's 78 modelled buildings the land was flat imagery. From the overview (about
2.4 km out, 50° down) and every low camera, the suburbs that surround Adelaide Airport on three
sides were a photo lying on the ground.

OpenStreetMap maps about 39 000 buildings in the 12 × 12 km around the field, but unevenly. Some
blocks have every house, and the next has none, while the satellite image shows continuous housing
(`docs/testing/surroundings-2026-09-28/` overlays). Extruding OSM alone would give a patchy suburb.
The street network, though, is complete.

## Decision

`scripts/generate-adelaide-suburbs.py` bakes `Art/Terrain/osm_adelaide_suburbs_v01.bin` (ASUB v1,
about 530 KB) from committed OSM snapshots:

- **Scope.** Every OSM building within 2 km outside the airfield rectangle, including those inside
  its corners. Dropped: the airport's own modelled footprints (`AdelaideBuildings`, the terminals,
  each buffered 25 m) and the landside precinct. The OSM aerodrome way is the airside fence, so it
  is not enough on its own.
- **Houses** (house, detached, semi and similar, or a small unlabelled building that fills its
  rectangle) become a box on their minimum-area rectangle with a hipped roof. One storey is 2.9 m,
  or 2.9 m per `building:levels`.
- **Everything else** is its simplified footprint (≤ 12 vertices, simplified progressively before
  any rectangle fallback) extruded to `height`, levels or a default for its type, with a flat roof.
- **Street-front fill.** Where OSM has no footprints, residential frontage (`residential`,
  `living_street`, `unclassified`, `tertiary`, `secondary`) gets houses every ~17 m, 6 m back
  from the kerb. Each one must be clear of every road, OSM building and other fill house, in
  residential land cover (`AdelaideLandCover`), and off the airport. They are stored as kind 2,
  so they are never confused with surveyed buildings.
- **Taper.** Buildings thin out deterministically between 1.1 and 1.8 km outside the field, so the
  extruded suburb fades into the imagery instead of ending at a line.
- **Result:** 14 992 buildings: 4 523 OSM houses, 1 594 other OSM buildings and 8 875 street-front
  houses, about 212 k triangles.

`AirsideAdelaideSuburbs` builds one mesh per 1 km tile (shared vertices per face, Color32, no
shadows cast) with a new `Airside/SuburbBuildings` shader, added to Always Included Shaders:

- **Walls:** an Adelaide palette of cream render, red brick, bluestone and white, with greys for
  commercial buildings.
- **Roofs:** half their own colour (terracotta, charcoal or pale steel, grey tile, galvanised) and
  half the satellite image at that spot, so the suburb sits on its imagery.
- **Heights:** buildings stand on `AirsideAdelaideSurroundings.LandHeight`, the same surface the
  ground meshes build, including the ADR 0158 relief.
- **Horizon:** the same fade as the land.

The Options "Graphics tests" group gains **Suburb buildings** (applies from the next launch).
`-airsideGraphicsOff suburbs` switches them off for a soak.

## Evidence

- `docs/testing/surroundings-2026-09-28/suburbs-overview.jpg` and `suburbs-low.jpg`: rough offline
  painter's-algorithm renders (not Unity) of the baked buildings over the imagery, from the
  overview and from 120 m over the north-east suburbs.
- `AdelaideSuburbDataTests`: file parses whole; counts of each kind; house-scale sizes and palette
  indices; nothing in the landside precinct; the taper holds by 1.8 km; truncated files are
  rejected. That last test found and fixed a crash in the parser.
- Settings tests cover the new switch. Domain suite passes.
- No building overlaps any of the 78 airport buildings (checked in the generator run).
- Asset audit unchanged from `main`.

## Costs

- About 212 k triangles in about 12 tile meshes, one material, no shadow casting.
- About 20 MB of mesh memory.
- Built at load from a 530 KB file.
- This is over the plan's 150 k-triangle guideline, which is why it has its own graphics-test switch.

## Not verified

**None of this has been seen in Unity.** A Mac check is still owed for:
- the look at the overview and follow cameras;
- the shader compile;
- frame time with and without the switch.

Data: © OpenStreetMap contributors (ODbL). Snapshots `docs/data/osm/adelaide-suburb-buildings-2026-09-28.json`
and `adelaide-suburb-streets-2026-09-28.json` (OSM base 2026-09-28T07:29Z). Output SHA-256 `acf1bf88d08a6704895d8db6da1c0e9f41fee56370047a33c63bf5147c11d179`.
