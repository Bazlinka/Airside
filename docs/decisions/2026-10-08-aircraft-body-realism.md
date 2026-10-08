# Continuous aircraft bodies

Status: Implemented; native appearance and performance unverified.
Date: 2026-10-08
Task: #651

## Decision

Refine the 13 scheduled airframes through a common body-loft pass before cutting
window apertures and fitting livery paint. Recover each authored elliptical station
profile, remove redundant linear samples, and interpolate radii/centre height with
shape-preserving cubic tangents. Close the finite-radius ends with rounded tips.
Keep the existing type dimensions, parallel cabin barrels and family silhouettes.
Use adaptive rings with a 2.5 mm contour chord-error target and 72-point sections.
Barrel rings are at most 60 cm apart to bound aperture subdivision.
The body remains a single named mesh; there are no additional rendering nodes.

Carry windows, cockpit surrounds, door leaves, paint and fuselage fittings onto
the same continuous profile. Rebuild radome shells from that profile. Glazing
apertures, seals and final paint are then generated against the actual revised
triangles. A320 v02 retains its existing licensed free-source engine, pylon, fan,
wheel and tail adaptations; regenerate those after the A320 base.

Bell 412 gains continuous cabin/tail-boom lofts and curved cockpit/cabin glazing
and sliding door leaves. The Parafield trainer gains a continuous body and fitted
cockpit/side glazing, paint, seams and handles. Both preserve their moving-part
names and ground/rotor/propeller datums.

## Reason

Straight station joins made tapered bodies visibly segmented. Several sources
ended their noses with a short cone or flat finite-radius disc. Flat helicopter
and trainer window slabs did not follow their cabin contours. Interpolating only
the visible body would leave fitted details behind, so the body and skin parts
are revised together before finishing.

These remain project-owned representative models, not manufacturer CAD. This
pass preserves the existing reference envelopes; inherited wing/engine/gear
approximations and the separate 787-window-height proposal (#543) remain separate.

## Affected systems and regeneration

`scripts/aircraft_body.py`, `polish-aircraft-glazing.py`, Bell and trainer generators;
existing AIR-001 / AIR-005…017 / AIR-YPPF-001 model paths and packaged mirrors.
Run `polish-aircraft-glazing.py`, Bell/trainer generators, then call `aircraft()`
in `import-free-visual-assets.py`, and sync runtime art. No new asset IDs or external
sources. Previous git revisions remain the fallback.

## Migration

No save migration, catalogue or aircraft animation contract changes.
Geometry-derived title and jet L1 docking coordinates are regenerated; simulation
rules remain unchanged.

## Validation

Bounded numeric body checks cover radii, continuous station derivatives, positive
volume, index budgets, body length and unchanged moving components. Geometry/asset
fit checks are recorded in `docs/testing/aircraft-bodies-2026-10-08/README.md`.
Native Unity compilation, overview/follow appearance, doors/glazing from inside,
and runtime performance require Bailey's chosen Mac review. No rendered review,
packaged build or broad simulation suite is claimed.
