**Owner:** Codex / Bazlinka
**Branch:** codex/fleet-surface-details-20261007

## Player-visible outcome
All 14 current aircraft retain their individual fictional liveries and gain restrained, fitted door and service-panel details at follow-camera distance.

## Plan
1. Derive detail patches from each actual mesh, preserving its curvature and dimensions.
2. Add thin door perimeter seams, recessed handle surrounds, latch bars and threshold/hinge marks. Distinguish cargo doors and Bell sliding doors.
3. Add restrained forward service-hatch details below the title/window belt on fuselages.
4. Apply the same finish to glTF and prefab aircraft, keep details attached through door articulation, use shared materials and release generated meshes.
5. Document coverage and minimal validation, implement and merge as Bailey requested.

## Scope
New AircraftSurfaceDetailGeometry.cs and AircraftSurfaceDetails.cs; ArtPresentationLoader.cs integration; narrow generated-mesh ownership update in AirsidePrototype.cs:RebakePartPivot; focused geometry tests and their metadata; generated harness; GAME.md, CHANGELOG.md, dated ADR, asset register/art contract and task evidence.

## Invariants / acceptance
No real branding, asset replacement, save/simulation changes or aircraft dimensions altered. Keep configurable operator paint, existing rig/doorway motion and missing-art fallback. Details hug real front-facing triangles, with a few millimetres separation. Cover ATR42, SF34, DH8D, E190, A223, A320, B738, B38M, A21N, A359, A339, B789, B78X and B412. No per-frame detail building.

## Verification
Focused clipping/curvature checks plus existing headless CI and asset metadata checks. Mac follow/overview day/dusk/night and open-door visual inspection remain unverified. Bailey explicitly requested implementation and merge with light testing; do not wait for native Unity confirmation.
