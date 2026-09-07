# MAT-001 — airfield surface material library

Bailey approved Batch B on 2026-09-06. Batch F1 ships eight URP Lit materials:

| Material | Role |
|---|---|
| `mat_asphalt_v01.mat` | Runway / taxi asphalt |
| `mat_concrete_v01.mat` | Apron / terminal concrete |
| `mat_grass_v01.mat` | Kingscote grass |
| `mat_corrugated_metal_v01.mat` | Building metal cladding |
| `mat_glass_v01.mat` | Terminal / vehicle glass |
| `mat_painted_line_v01.mat` | Markings |
| `mat_aircraft_v01.mat` | Aircraft skin |
| `mat_wet_v01.mat` | Wet / water lean |

Create or refresh via **Airside → Art → Create MAT-001 Materials**. Runtime
`AirsideMaterialLibrary` instances `Resources/Airside/Materials/mat_*_v01` when
present and falls back to procedural Lit otherwise. Maps remain Batch B PNGs —
no unique 4K textures.
