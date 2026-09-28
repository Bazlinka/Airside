# 0158 — Real Adelaide terrain and the far ring out to the Hills

Date: 28 September 2026. Author: Claude, at Bailey's request ("improve the surroundings: ground,
scenery, map data, realism"). Plan `docs/plans/ypad-surroundings-plan.md` P6.

## Context

Past the ±12 km surroundings square there was nothing, and the surroundings shader faded everything
to the fog colour between 6.5 and 9.6 km from the camera, whatever the weather. So there was always
a fog wall about 10 km out, even on a clear day, when the weather model's own visibility is 60 km.
The coastal plain was a flat sheet at one height, and the Adelaide Hills, the city's eastern
skyline, did not exist. Mount Lofty is 727 m high, about 16 km from the airport.

## Decision

**Heights.** `scripts/generate-adelaide-terrain.py` bakes Copernicus DEM GLO-30 (30 m, open) into
the runway frame. It covers ±32 km at 125 m cells, 513², stored as int16 in 5 cm steps (526 KB,
`Art/Terrain/dem_adelaide_runway_v01.bin`). GLO-30 is a surface model, so on the plain each cell
takes the 20th percentile of 25 sub-samples, which finds the ground between roofs. Above 80 m it
takes the median, so summits are not shaved. `AdelaideTerrainHeights` reads the file and has no
Unity dependency, so it is tested headlessly.

**Surroundings relief.** Land higher than the existing plain (3.6 m above sea) rises by the
difference. The relief eases in from 700 m to full at 2 500 m outside the airfield, so the airfield
edge, the coast, the beach and inland water keep exactly their current shape. Landside roads use
the pavement plane plus the same relief (`RoadHeight`), which is identical to today wherever the
relief is zero.

**Far ring.** `AirsideAdelaideFarTerrain` is one mesh of 250 m cells over a 30 km disc (the far
clip), skipping the square the surroundings own except a 300 m band tucked 3 m beneath their edge.
It uses the same Surroundings shader, with a second Sentinel-2 image: ±30.5 km at 2048 px, toned
with the near image's fitted curves. The sea is the stylised deep Gulf, the same colour as the
surroundings' open water at their edge.

**Horizon.** When the ring builds, both materials fade to the fog colour at 25.5–29.5 km, just
before the far clip, instead of 6.5–9.6 km. How far you see is then the weather's: the Hills on a
clear day, gone in rain or fog. If the DEM or far image is missing, nothing changes: flat plain and
the old fade.

**Credits.** A new Flight Manual page, "Map and data credits", carries the Copernicus DEM statement
(house style: the "; all rights reserved" becomes a separate sentence) along with the map, imagery,
weather and traffic sources.

## Evidence

- `docs/testing/surroundings-2026-09-28/terrain-hillshade.jpg`: coast, Port River, LeFevre
  Peninsula and the Hills in the right places.
- `docs/testing/surroundings-2026-09-28/hills-before-after.jpg`: a rough offline height-field render
  (not Unity) with the game's heights, images, fog density for 60 km visibility and horizon fades,
  from over the terminal and from 1.5 km up in the south-west. Before, the land ends in a fog wall
  about 10 km out. After, the plain runs on and the Hills make the skyline.
- `AdelaideTerrainHeightsTests`: grid extent, the airfield at 0–12 m, the Gulf at 0, the CBD at
  30–70 m, Mount Lofty at 600–740 m, the relief rules, the file parser and the credits. Domain suite
  941/941. Asset audit unchanged from `main`.

## Costs

- **Far ring:** about 36 k cells (72 k triangles), one draw call, no shadows, and a 2048² image
  (RGBA32 with mips, about 22 MB). The ring is built from the baked grid at load with no per-frame
  work.
- **Surroundings:** no new vertices, only heights.

## Not verified

**None of this has been seen in Unity.** A Mac check is still owed for:
- the join at 12 km;
- the Hills silhouette at noon and dusk;
- road heights inland;
- the frame-time cost of the ring.
