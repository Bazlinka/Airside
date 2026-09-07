# Aircraft skin surface generation — 2026-09-07

Decision **0025** item 4. Project-owned procedural maps so `AircraftSkin`
materials catch light under follow camera (panel seams + rivet grit).

## Generator

`scripts/generate-aircraft-skin-surfaces.py` — 256² tileable PNGs.

## Outputs (`Textures/Surfaces/`)

| Stem | Maps |
|---|---|
| `tx_aircraft_skin` | basecolour, normal, AO, metallic/smoothness mask |

## Runtime

- `AirsideMaterialLibrary.AuthoredStemByKind` maps `AircraftSkin` → `tx_aircraft_skin`
- Authored basecolour auto-applies when `Create` is called without an albedo
- `ApplyLiveryDecal` covers segmented fuselage parts (Fuselage / Mid / Aft / Nose)
- StreamingAssets synced via `scripts/sync-art-streaming-assets.sh`

## Licence

Project-owned. No third-party packs.
