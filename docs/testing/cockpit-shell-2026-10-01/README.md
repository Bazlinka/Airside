# Saab cockpit shell fix

The previous packaged `v2-game-startup.png` shows apron through an open strip
between the left side lining and instrument panel and beneath the panel. Cockpit
mode hides the exterior fuselage, so its interior must provide an opaque shell.

The floor now reaches the forward nose contour; continuous lower side, forward
footwell and rear side faces close the body below the existing window sills.
The actual window openings remain clear. Original geometry only.

Inspected native images: forward, footwell, left-down and right-down. These use
the runtime SF34 builder and pilot eye. The editor review also retains its existing
layout/window/roof/bank views. Reproduce with `CockpitAppearanceReview.Run`.

Regression uses the actual shell meshes to check seven lower sightlines at level
and banked/rotated aircraft poses, and three window sightlines that must stay open.
Native Unity: 1679 total, 1677 passed, zero failures; two existing inconclusives.
Headless: 1278 passed, zero failures. Diff whitespace check passed.

## Packaged proof and main integration

Clean Mac build passed at `437b6462`. `game-startup.png` and its log are from that
actual airport player: real VH-ZRC engine-start eligibility, pilot-eye camera,
1600x900. Visually inspected: the open apron strip beside the panel is replaced
by solid lining; window views remain clear. No managed exception in the capture.
The initial three-minute attempt ended before engine eligibility; the ten-minute
window captured shortly after real first spool and then stopped. This is visual
proof of this shell fix, not complete flight or performance acceptance.

Integrated newer main `67299614` fog and audio changes; only changelog conflicted.
All four native cockpit camera/lifecycle/shell cases passed on that integration.
Main's GitHub run 36812953638 already fails the unrelated
`AircraftAudioMixTests.PropGovernorHoldsTheNoteWhileJetRevsRise` assertion after
PR #507 changed jet pitch. The source shell fix does not change audio or that test;
prior full-suite success above applies before those concurrent audio changes.
