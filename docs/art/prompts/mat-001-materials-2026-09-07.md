# MAT-001 — shared URP Lit material family (Batch F1)

**Date:** 2026-09-07  
**Tool:** Cursor overnight Batch F1  
**Contract:** Batch F packet; Batch B textures; decision 0022

## Deliverables

Eight URP Lit materials (Art + Resources mirror for runtime `Resources.Load`):

| File | Maps (Batch B) |
|---|---|
| `mat_asphalt_v01.mat` | `tx_asphalt_runway_*` |
| `mat_concrete_v01.mat` | `tx_concrete_apron_*` |
| `mat_grass_v01.mat` | `tx_grass_kingscote_*` |
| `mat_corrugated_metal_v01.mat` | `tx_corrugated_metal_*` |
| `mat_glass_v01.mat` | glass/transparent Lit profile + optional AO |
| `mat_painted_line_v01.mat` | painted-line maps |
| `mat_aircraft_v01.mat` | `tx_aircraft_skin_*` |
| `mat_wet_v01.mat` | wet-leaning asphalt/concrete profile |

Paths: `Art/Materials/` and `Resources/Airside/Materials/`.

## Runtime

`AirsideMaterialLibrary.Create` prefers `TryInstantiateAuthored` →
`Resources.Load("Airside/Materials/mat_*_v01")`, then instances and tints.
Procedural Lit create path remains the fallback when a `.mat` is missing.

## Authoring

Editor menu **Airside → Art → Create MAT-001 Materials** (`AirsideMat001Menu`).
Shader GUID `933532a4fcc9baf4fa0491de14d08ed7` (URP Lit). Normal strength and
reflections kept restrained; no unique 4K textures.

## Assumptions

- Materials are mirrored under Resources so packaged builds load without Editor
  Addressables groups.
- Wetness still uses `ApplyWetness` on instances; `mat_wet_v01` is the Water /
  wet SurfaceKind template.
- Do **not** assign `mat_glass` / SurfaceKind.Glass to rain, shadows, clouds or
  contact blobs — those must use Default Lit (Batch F1 follow-up on PR #134).
