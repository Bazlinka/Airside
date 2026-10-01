# Reference-driven Saab 340B interior, revision 2

Bailey rejected the generic first blockout and requested an online-referenced,
gamified rebuild. The replacement source is `SaabCockpitInterior.cs`.

Reference observations and URLs: `docs/art/reference/saab340-cockpit/README.md`.
C&L's real aircraft interior photographs were inspected on page 3 of both PDFs.
They establish the stacked CRT pairs, grey panel, round central engine gauges,
squared yokes, pedestal and overhead. No photo pixels are reused in the game.

Visual review covers native forward, full layout, panel, left, right, overhead and
bank views using the runtime SF34 builder and its real seat position. The layout
shot moves the review camera back to show both stations; it is not the default
player seat. These stills are geometry evidence, not flight/motion evidence.

Controls and aircraft-availability behavior remain the existing cockpit candidate.
The small local telemetry is live; other dials and EFIS illustrations are decorative.
This is a recognisable simplified layout, not a dimensionally surveyed replica or
functional avionics suite. The old `native-*.png` and `game-*.png` in this directory
are revision 1 evidence and must not be used as revision 2 acceptance.

Verification: full headless regression 1,199 passed, zero failures; native Unity
EditMode 1,585 passed, zero failures, the same two unmet-precondition inconclusives
(1,587 total). Unity asset audit and diff whitespace checks pass. Seven final
native render angles are committed as `v2-native-*.png` and visually inspected.
Mac build and actual airport capture are the next verification step. Broader cockpit-mode journey/audio/performance
acceptance remains in the parent README and is not claimed by this visual revision.
