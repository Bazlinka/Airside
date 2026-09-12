# 0040 — AIR-001 v02 visual finish

Date: 2026-09-12. Requested by Bailey: “Finish it then” after the mesh review.

## Decision and reason

Create `mdl_atr42_starter_v02` rather than overwrite the verified v01. Replace floating rectangular glazing with rounded panels fitted to a fuller
continuous fuselage. Use four separated cockpit panes and move the passenger and
cargo doors to credible positions. Join the fuselage, dorsal fairing, fin, crown
saddle and stabiliser with overlapping geometry so no daylight gap is possible.
Seat the tail antenna and add static wing-root fairings. Preserve articulated part names, prop
and gear geometry, and the existing dimension envelope.

## Integration

The model is project-owned procedural geometry, generated with the existing
NumPy/glTF pipeline. No external assets or paid generation; attribution none.
Art and StreamingAssets receive identical glTF/bin files with distinct GUIDs.
Runtime prefers v02 and retains v01 then v06. The separate v02 key prevents the
old baked v01 Resources prefab from hiding this revision. v02 does not require
an FBX or prefab bake: ArtGltfLoader is the existing supported runtime path.

## Acceptance and evidence

Scope: v02 generator/checks/assets, runtime selection, art manifest/register and
handoff. Outcome: fitted glazing and coherent nose/T-tail silhouette at overview
and follow, with original articulation intact. Geometry regression checks pass;
158 parts, 20,572 triangles. Static six-view inspection is committed
as `docs/art/candidates/air_001_atr42_v02_mesh_review.png` (approximate materials).

Unity compilation, EditMode and day/dusk/night overview/follow checks remain
mandatory before merge. Exercise gear, propellers, doors, flaps and elevators.
The host has neither Unity nor .NET; no runtime verification is claimed.
Simulation, timing, save schema and existing v01 assets are unchanged. No migration.
