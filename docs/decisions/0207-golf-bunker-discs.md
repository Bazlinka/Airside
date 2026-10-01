# 0207 — Golf bunker discs at overview

Date: 2026-10-01 · Owner: Cursor

**Decision:** bake OSM `golf=bunker` ways from `DAT-YPAD-MAP` into runway-frame
discs (`AdelaideGolfBunkers`) and draw one sand-coloured child mesh on the
surroundings root. Radii clamp to 2–22 m. The 50 m land-cover grid is unchanged
so ADR 0206 Golf green stays.

**Reason:** Phase 1 land-cover polish — courses now read as irrigated green but
had no sand traps at overview.

**Affected systems:** presentation surroundings mesh only.
**Migration impact:** none. Simulation, routes and saves unchanged.
