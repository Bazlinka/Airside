# Task packet — sharper far satellite image (Adelaide Hills)

Status: **proposed, needs Bailey's sign-off** (changes a shipped art asset and the data register).
Drafted 2026-10-07 by Claude. Follows ADR 0247 (far terrain now meshed at 125 m).

## Player-visible outcome
Dragging the map camera out over the Adelaide Hills shows crisper ground colour (roads, paddocks,
tree cover, creek lines) instead of the current soft wash. Near view and the 12 km surroundings
look the same.

## Why the Hills look soft today
`tx_adelaide_sentinel2_l2a_far_v01.jpg` is 2048 px over ±30.5 km = ~30 m per pixel, and
`generate-adelaide-satellite-s2.py` bakes it from a **40 m** overview of the scenes
(`bake(..., FAR_EXTENT_METRES, FAR_SIZE, 40.0)`). The source is 10 m Sentinel-2, so detail is
being discarded twice (40 m source, then 2048 px output).

## Scope (files / module)
- `scripts/generate-adelaide-satellite-s2.py`: raise `FAR_SIZE` 2048 → 4096 and the far source
  resolution 40 m → 20 m (~15 m/px output). Keep the near-square tone fit unchanged so the two
  images still meet.
- Regenerated `Art/Textures/Environment/tx_adelaide_sentinel2_l2a_far_v01.jpg` **under a new
  version name** (`..._far_v02.jpg`) plus its `.meta` and byte-identical StreamingAssets mirror
  (`scripts/sync-art-streaming-assets.sh`, `scripts/audit-unity-assets.py`).
- `AirsideAdelaideFarTerrain.SatelliteTexturePath` → v02. `SatelliteExtentMetres` unchanged.
- `docs/data/ASSET_AND_DATA_REGISTER.md` row TEX-ENV-YPAD-FAR → v02 (resolution, source
  resolution, cost, fallback = v01 file kept until verified).
- New ADR 0248.

## Invariants / decisions
- ADR 0158/0162: no main-thread block-compress at startup; texture decode/upload cost must not
  reintroduce the startup hitch. Measure before/after.
- Licence: same nine Sentinel scenes, same attribution; no new external source.
- Do not overwrite v01; keep it as the fallback until Bailey signs off on v02.

## Acceptance criteria
1. v02 is 4096 px, covers the same ±30.5 km square, and tone matches v01 at the 12 km join
   (no visible seam on the Mac at dusk and day).
2. Offline hill-ridge crops compared with v01 show finer paddock/road/tree detail.
3. Startup-to-title time and 60 s frame time at the overview camera are no worse than v01 by a
   margin Bailey sets (suggest: startup +≤1 s, no new hitch).
4. Memory: far texture grows ~16 MB → ~64 MB (+mips) uncompressed; confirm acceptable on the
   Mac build, otherwise use a compressed import or 3072 px.
5. `scripts/audit-unity-assets.py` and `scripts/test-unity.sh` pass.

## Tests / playtest steps
Needs network (AWS Earth Search COGs) and numpy/scipy/pillow/rasterio/pyproj; run
`python3 scripts/generate-adelaide-satellite-s2.py` (scene manifest in
`docs/data/sentinel-2/adelaide-l2a-v02-scenes.json`). Then on the Mac: drag the map over the
Hills at day/dusk; compare against v01 screenshots (`scripts/capture-game.sh`).

## Must remain unchanged
Near 12 km texture, surroundings shader, simulation, saves, far terrain mesh (ADR 0247).
Not attempted here: the 29 km+ outer ring (500 m cells, vertex-coloured, no satellite image).
