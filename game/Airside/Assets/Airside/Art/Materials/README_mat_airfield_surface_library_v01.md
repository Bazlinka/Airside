# MAT-001 — airfield surface material library

Bailey approved Batch B on 2026-09-06. Runtime already binds:

- basecolours TEX-SRF-001…004 via `AirsidePrototype.TryLoadArtTexture`
- companion normal / AO / metallic-smoothness masks TEX-SRF-005 via
  `AirsideMaterialLibrary` (StreamingAssets; procedural fallbacks)

Optionally create `mat_airfield_surface_library_v01.mat` in Unity 6.3 LTS after
import, linking the same maps into Editor materials / Addressables. Do not invent
shader GUIDs outside the Editor.
