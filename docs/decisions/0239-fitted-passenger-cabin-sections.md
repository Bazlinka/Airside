# ADR 0239 — fitted passenger cabin sections

Date: 2026-10-06. Status: implemented in code; native visual verification excluded.

Bailey authorised merging the aircraft audits and continuing the recommended
window/cabin slice. No Unity tests, builds, editor/player execution are permitted
in this session. Existing flight simulation, watched identity and cockpit display
systems remain the owners of motion/information.

## Decision

Separate structural window spacing from seat pitch. Profiles contain measured
shipped-kit pane dimensions and pitches for thirteen passenger types, with separate
lining/reveal/crown/aisle data and bounded continuation lengths. Fit the first three
reference cameras (ATR42, A320, B789) to individual panes instead of merged-pair
midpoints. Retain other stations until their individual fitting passes.

Build curved, continuous double-sided lining around clear openings, layered trim/
seal/reveal, curved ceiling, fitted shaped bins and service units. Exact cell-corner
angles prevent the annular wall from cutting across rectangular cell corners.
Lining lies inboard of the pane datum by the reveal depth; it does not move the
world aperture out beyond the aircraft. No transparent overlay obscures scenery.

Only five nearby seat rows carry tray latches, armrests, supports, belts and
headrests. Simpler seats continue into 12/16/20 m local sections. These are bounded
visual sections with distant partitions, not full cabins or certified operator
layouts. Grouping preserves Saab 1+2, A220 2+3 and widebody twin aisles.

Accumulate seats and shell into material meshes without per-fitting GameObjects/
colliders; discard owned intermediate meshes,
retain shared cached primitive meshes. Generate original subtle cloth weave and
separate surface smoothness for lining, fabric, rubber and trim. Passenger cabins
use one bounded, shadowless ceiling fill rather than inheriting the pilot-panel
light. Cockpit behaviour continues through the unchanged base implementation.

Dreamliner windows receive decorative dimmer hardware instead of mechanical shade
tracks. Dimming is not interactive and no tint is applied. The existing exterior
787 panes are undersized scaled donor geometry; large real 787 windows require a
paired exterior/interior modelling pass. Do not call this a certified replica.

## Evidence and limits

`docs/testing/cabin-immersion-2026-10-06/README.md` records source measurements,
headless results and static asset checks. The Unity-dependent shell/material/light
builders are not compiled or rendered by those checks. Native C# compilation,
clipping, actual material appearance, visibility switching and frame cost remain
unverified by instruction. Bell interiors and family-specific cockpit modelling
remain later audit packets; no new licensed assets, save migration or flight rules.
