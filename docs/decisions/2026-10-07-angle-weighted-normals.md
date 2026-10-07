# Angle-weighted smooth normals for runtime-loaded models

- **Date:** 2026-10-07
- **Decision:** `MeshNormalSmoothing.Compute` (used by `ArtGltfLoader` for every runtime kit and aircraft part) now
  weights each face by its corner angle and tests each face against the 60° threshold directly. Before, every face
  contributed one equal-weight unit normal, those were blended per vertex index, and the threshold compared the
  already-blended vertex normals rather than the faces.
- **Reason:** Reported "wavy" models. Equal weighting lets a fan of thin triangles out-vote the wide face beside it, so
  smoothly curved panels shade lumpy and rippled; the same mesh shades differently depending on how it was triangulated.
  Angle weighting is the standard fix and is independent of triangulation.
- **Affected systems:** `MeshNormalSmoothing`, every glTF-loaded model (aircraft, vehicles, buildings). Tangents are derived from the normals.
- **Migration impact:** none (presentation only; no save or simulation change). One-function revert.
- **Verification:** the three `MeshNormalSmoothingTests` scenarios were checked against a Python port of the algorithm
  (30° fold blends, 90° fold stays hard, flat quad unchanged). NOT run in Unity: the headless harness skips UnityEngine tests.
  **Next:** look at aircraft fuselages, GSE and terminal glazing at follow cameras and compare shading against `main`.
- **Startup time:** reviewed, not changed. Awake still builds the world synchronously and decodes the 4096 px far satellite
  (~85 MB uncompressed with mips, ADR 0162/0248). Any further saving needs a Unity profile of Awake first.
