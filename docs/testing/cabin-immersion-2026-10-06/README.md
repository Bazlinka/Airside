# Passenger cabin immersion — 6 October 2026

Source-only implementation of the first recommended slice from the aircraft audit
(merged #538); older visual report #534 also merged, its concrete fixes already
implemented in #535/#536. ADR 0239, branch
`feature/aircraft-cabin-immersion-20261006`, base main `2868e6c8`.

## Changed behaviour

Thirteen passenger types share a curved cabin/window builder with independently
profiled pane spacing, clear rounded openings, recessed lining/trim, shaped bins,
service units and longer bounded inward perspectives. Nearby seats have shaped
backs/headrests, trays/latches, armrests, support legs and resting belts; distant
rows retain simple geometry. Cabin material responses and original procedural
cloth differ, with one shadowless ceiling fill responding to day/night. Electronic
window dimmer fittings are decorative 787 cues; there is no new dimming control.
Cockpit environment, flight HUD, outside wings/engines and watched identity are
preserved by source ownership, not claimed runtime acceptance.

## Shipped-kit measurements and fitting

Pane POSITION/indices were decoded from current glTF/bin files; connected panes
were separated within merged nodes. Figures below are model measurements, not
manufacturer-certified cabin data. `WindowY` remains within .0004 m of the chosen
pane's projected Y centre.

| Type | Selected left pane | Old camera Z | New Z | Pane spacing | Projected width × height |
|---|---|---:|---:|---:|---:|
| ATR42 | cabin_window_4 | 2.048 | 1.794158 | .508045 | .240021 × .330165 |
| A320 | cabin_window_7 | -13.960 | -14.295000 | .670000 | .220000 × .329231 |
| B789 | cabin_window_17 | -24.471 | -24.232627 | .477656 | .235067 × .344649 |

Three selected stations move roughly .24–.34 m; aircraft parts do not move.
Other profiles retain their prior longitudinal camera stations. All window pitches
are independent from seat pitches. Aperture dimensions round to model measurements.

The old half-width is about 15 mm inward of these panes' bounding-box X centres;
curved pane extents cross that constant-X plane. New lining is inset by reveal
depth and returns toward the original pane datum. This establishes an approximate
construction fit, not uniform hull clearance. Widths/aisles and the curved lining
are authored. Future native review must check both sides and actual curved skin.
The -9/-10 kit retains inherited longitudinal scaling, reflected in its different
window pitch/width. Exterior resizing and manufacturer-accurate 787 windows remain
future work, not falsely accepted by passing profile-data checks.

## Verification

- Integrated Unity-free .NET suite: **1,683 passed / 0 failed** (19 new pure profile
  cases). The existing harness exclusions remain; this is not Unity compilation.
- After the final lining-clearance adjustment, focused profile checks:
  **19 passed / 0 failed**.
- Static asset audit: **1,766 unique GUIDs**, **386 byte-identical runtime art
  mirrors**, **70 committed character materials**. New scripts/test metadata only;
  no imported photo or external asset.
- Harness generation and `git diff --check` pass. Material mesh accumulation without per-seat-fitting GameObjects/colliders,
  intermediate ownership/disposal, clear openings, exact corner coverage and
  lining/bin/seat relationships were reviewed in source.

No Unity tests, editor/player execution, builds, new renders or player captures.
Native C# compilation, lighting, shader appearance, comfort, clipping, view-cycle
visibility and frame cost are unverified. Future acceptance is the same registration
through cockpit → left/right outward/inward → exterior, day/dusk/night and banking,
with shared HUD visible. Retained historical fixtures are not post-change evidence.
