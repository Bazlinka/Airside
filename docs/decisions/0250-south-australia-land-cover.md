# 0250 — State-wide land cover for the streamed South Australia terrain

Status: accepted (Unity look not yet verified)
Date: 2026-10-07 (increment 1 of Bailey's approved request: pan/zoom the overview across all of South Australia, colour by real land cover, no new satellite imagery)

- **Decision:** Add `landcover_south_australia_v01.bin` — one class byte per 0.01° (~1 km) cell over
  128–142 °E, 39–25 °S (`FlightWorldGrid.Covered`), 1400 × 1400 cells, 123 KB — baked from ESA WorldCover 2021 v200 by
  `scripts/generate-sa-landcover.py`, parsed by the pure `FlightWorldLandCover` (format `SALC` v1). `AirsideFlightWorldTerrain`
  colours its streamed 16 km tiles from it, through the same `AdelaideOuterTerrainGeometry.LandCoverColour` rule and measured
  palette as the Adelaide rings (ADR 0190), so the two landscapes meet in one palette. Without the file the tiles keep the old
  height-only colours. No imagery is shipped.
- **Reason:** the streamed tiles were a two-colour height lerp, so flights and (next) the overview looked the same everywhere
  from the Nullarbor to the Murray. Land cover already existed only for ±96 km round Adelaide.
- **Affected systems:** `AirsideFlightWorldTerrain` (colour only — geometry, tiling, residency and costs unchanged),
  `AdelaideOuterTerrainGeometry` (new class-based overload; the old one delegates, same output), the new file, parser, test and
  generator. No save, simulation or schema change.
- **Cost:** +123 KB (twice: authored + StreamingAssets mirror), parsed once when the terrain is first created (1.96 M bytes
  inflated); per-vertex work is one table lookup. Unmeasured in Unity.
- **Evidence:** headless `FlightWorldLandCoverTests` pass (parse/reject/orientation/bounds, shipped-file checkpoints: Spencer
  Gulf, Gulf St Vincent, the Bight and Southern Ocean are water; Lakes Eyre and Torrens bare; Yorke Peninsula crop; Adelaide
  built; the class-based colour equals the classic one for every class); `audit-unity-assets.py` passes; preview
  `docs/testing/sa-landcover-2026-10-07/landcover-south-australia.png` shows the Eyre and Yorke peninsulas, both gulfs,
  Kangaroo Island, the mallee, Flinders Ranges and salt lakes in the right places.
- **Known limits:** at ~1 km a town is smaller than its cell's majority, so built-up shows only for Adelaide-sized areas
  (0.0 % of the state); that is fine for a regional backdrop. WorldCover is read from the COG overviews with nearest-neighbour
  votes (4 × 4 per cell), not full-resolution majority. Hill shading comes from lighting only (no slope term in the tiles yet).
- **NOT done (increment 2 — needs Unity):** letting the overview camera pan/zoom far from Adelaide and driving the tiles from
  it. Packet: `docs/plans/south-australia-overview-streaming.md`.
- **Revert:** delete the file or the three lines in `AirsideFlightWorldTerrain.Create`/`Build` that read it.
