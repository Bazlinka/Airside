# 0247 — Far terrain uses the full 125 m DEM

- **Date:** 2026-10-07
- **Decision:** `AirsideAdelaideFarTerrain.Stride` 2 → 1. The far ring (12–30 km, incl. the Adelaide
  Hills) is meshed from every DEM sample (125 m cells, ~513² vertices, ~0.5 M triangles) instead
  of every second one (250 m cells).
- **Reason:** Bailey reported the Hills looked poor when the map camera was dragged out over them.
  The ring was discarding half the height data it already ships (DAT-YPAD-DEM is 125 m).
- **Affected systems:** `AirsideAdelaideFarTerrain` only. Same material, one draw, shadows off.
  No data/asset change, no save or simulation change.
- **Cost (estimated, unmeasured):** ~4x vertices/triangles for this one mesh (~6 MB more mesh
  memory) and a longer one-off build at startup (ADR 0162 cares about startup hitches).
- **Not done / limits:** the far satellite image is still 2048 px over 61 km (~30 m/px), so the
  Hills' colour stays soft; a sharper image needs a new Sentinel-2 export and licence-register
  entry (Bailey's call). The 29 km+ outer ring is 500 m cells and is unchanged.
- **Verification:** NOT verified — no Unity or dotnet here. **Next:** on the Mac, drag the map over
  the Hills at day/dusk, compare frame time and startup time, and revert (Stride = 2) if either
  regresses.
