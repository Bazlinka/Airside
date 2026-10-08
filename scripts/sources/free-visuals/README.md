# Free visual source inputs

All inputs are free for commercial reuse. Runtime loaders make no network calls.
`python3 scripts/import-free-visual-assets.py` (NumPy and Pillow) rebuilds derivatives.
The VRML97 geometry reader supports the transformations and indexed meshes in this
specific A320 source; it is not a general VRML application or animation importer.

- **A320 by manilov.ap**: https://sketchfab.com/3d-models/a320-ec28bdee6c944688a19bef31ea33437f
  Creative Commons Attribution 4.0: https://creativecommons.org/licenses/by/4.0/
  Author: https://sketchfab.com/manilov.ap
  `A320.wrl.gz` is the original gzip-compressed VRML from Bailey's downloaded archive.
  Public API licence evidence is in `A320-licence.json`. Adapted nacelles, exhausts,
  pylons, fan blades, tyre sidewalls and horizontal tail; fitted to Airside's envelope,
  coordinate system and named animation pivots. Original airline texture maps are not
  used. Airside's hollow cabin skin, doors, windows, gear rig and fictional paint remain.
- **Kenney Car Kit 3.1**: https://kenney.nl/assets/car-kit — CC0 1.0.
  Kept `truck.glb` and `wheel-truck.glb`. Adapted cab geometry and fitted wheel geometry
  to the game's service vehicles. Kept original service equipment and moving parts.
- **Kenney Nature Kit**: https://kenney.nl/assets/nature-kit — CC0 1.0.
  Kept `tree_thin.glb` and `plant_bushDetailed.glb`. Rounded canopy lobes with one
  subdivision, retained branch structure, muted foliage and real tree positions.
- **Fabric Pattern 07 by Rob Tuytel / Poly Haven**:
  https://polyhaven.com/a/fabric_pattern_07 — CC0 1.0.
  Original 1K colour, OpenGL normal and roughness JPEGs. Broad plaid colour removed; fine weave retained
  for tintable fictional upholstery; roughness packed into smoothness alpha.

Hashes and runtime filenames: `docs/testing/free-visual-upgrade-2026-10-08/sources.json`.
Mandatory A320 attribution also ships in `StreamingAssets/ThirdPartyNotices.txt`.
