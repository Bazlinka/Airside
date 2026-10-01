# ADR 0215 — All-jet spectator cockpit interiors

Date: 2026-10-01. Status: implemented on `feature/all-jet-cockpits`, unmerged candidate.

Bailey requested immersive cockpit preparation covering every jet on a separate
branch. Extend ADR 0214's spectator mode to the ten flying catalogue jets. Explicit
per-type kit-coordinate eye anchors and family layouts prevent Saab fallback and
length-based seat scaling. Reuse the Saab's tested shell hiding/restoration and
geometry ownership via `CockpitInterior`; preserve its geometry.

Original procedural jet decks provide opaque lower/front shell, floor, bulkhead,
roof, framed open windows, seats, pedals, family controls, display arrangement,
guidance rail, overhead and pedestal. Only the entered aircraft allocates a deck.
Live labels derive from existing presentation state. Simulation and saves do not
change; no migration. Unknown types fail closed. Cold access and shutdown exit
retain the existing rules. Interactive preparation is separate scope.

Affected systems: cockpit availability, presentation lifecycle and native review.
Acceptance/evidence: `docs/plans/jet-cockpits.md` and
`docs/testing/jet-cockpits-2026-10-01/README.md`. Broad visual, audio and journey
acceptance cannot be inferred from profile coverage or compilation alone.
