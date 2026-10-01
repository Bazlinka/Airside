# 0227 — Cockpit window view fitted to real over-the-nose vision

Date: 2026-10-01. Decision: the jet and glass-turboprop cockpits are fitted to a realistic
sight-line, and the exterior kit parts a pilot truly sees are kept.

- Jets: glareshield leading edge now cuts the sight line at about 16 degrees below the eye
  (was about 4, so the runway ahead was hidden). Window sills drop to the glareshield, the
  windscreen head reaches about 30 degrees up. Values live in `JetCockpitShellGeometry`.
  Reference: FAA AC 25.773-1 (pilot compartment view); transport decks typically 15-20 degrees.
- ATR 42 / Dash 8: panel group lowered 6 cm (about 11 to 15 degrees over the nose), default
  gaze 8-9 degrees down instead of 14-15, centre windscreen sill lowered to close the lining strip.
- Outside the glass: the case-sensitive keep-list hid flaps, ailerons, spoilers, fans, intakes,
  pylons and exhausts, leaving bare wings/engines. `CockpitExteriorVisibility` now keeps them.
- Targets, not surveyed measurements. Affected: Presentation cockpit files only. No save,
  simulation or asset changes. Native Unity render review remains open (no editor in this session).
