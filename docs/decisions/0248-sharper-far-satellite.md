# 0248 — Sharper far satellite image (v02)

- **Date:** 2026-10-07 (approved by Bailey in session after the packet in `docs/plans/sharper-far-satellite.md`)
- **Decision:** The far terrain ring is draped with `tx_adelaide_sentinel2_l2a_far_v02.jpg`: 4096 px over
  ±30.5 km (~15 m/px) from a 20 m source, replacing the 2048 px / 40 m v01. Same nine scenes, same
  near-image tone fit. v01 stays in the tree as the fallback.
- **Reason:** With ADR 0247 the Hills' relief is meshed at 125 m, but their colour was a soft 30 m/px
  wash baked from a 40 m overview of 10 m data.
- **Affected systems:** `AirsideAdelaideFarTerrain.SatelliteTexturePath`; generator
  `scripts/generate-adelaide-satellite-s2.py` (`FAR_OUTPUT`, `FAR_SIZE`, `FAR_SOURCE_METRES`, `--far-only`);
  register row TEX-ENV-YPAD-FAR; `docs/data/sentinel-2/`.
- **Migration impact:** none (no save or simulation change). Near 12 km texture unchanged.
- **Cost:** JPEG 1.2 MB → 4.6 MB (twice, authored + StreamingAssets mirror). Decoded texture ~16 MB →
  ~64 MB (+ mips) because it is loaded with `LoadImage`, and decode happens at load: **startup time and
  memory are unmeasured.**
- **Evidence:** offline side-by-side crops of the Hills/suburbs against v01 show road grids, creek lines and
  blocks that v01 smears; overview colour/tone matches. `scripts/audit-unity-assets.py` passes. The
  darker horizontal band across the lower third exists in v01 as well (scene-boundary tone), not new.
- **NOT verified:** no Unity run, no seam check against the 12 km near image in-game, no startup/memory
  measurement. **Revert** by pointing `SatelliteTexturePath` back at `_far_v01.jpg`.
