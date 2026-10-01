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
First V2 Mac build passed at `047a296f` and rendered the interior in the airport.
Its small cube tick marks disappeared in the player, despite appearing in native
stills. Thicker separate geometry at `b8e871bd` still disappeared in the player.
The final approach uses original code-drawn tick artwork directly on the dial
faces, removing the separate tick surfaces. Native Unity passed again with the
same counts. Final clean Mac player build passed from committed source `4c153d46`.

## Final packaged visual proof

`v2-game-startup.png` and `v2-game-startup.log` are from that clean Mac player in
the actual airport at 1600x900. The camera entered VH-ZRC at first nonzero spool;
telemetry subsequently records R0.07. The screenshot shows the real cockpit at
stand (GS 0 kt, height 0 ft, heading 233 degrees), with clear windows, visible
paired flight displays, round engine instruments and the small live readout.
The tick marks are now visible on the dial faces in the player. No managed
exception is recorded. The short capture includes startup and overview frames;
it is not a controlled performance comparison, complete flight, or 14-minute soak.

The requested reference-driven gamified visual rebuild is complete. Full cockpit
journey, controls playtest, weather/night, audio and performance acceptance remain
open follow-up requirements. Bailey approved merging the simplified SF34 candidate
on 1 October. Broader cockpit-mode journey/audio/performance
acceptance remains in the parent README and is not claimed by this visual revision.

## Integration with main

Integrated main `b290b275`, preserving aircraft articulation, hollow doorways and
hangar work. Restored main's implemented `AttachDoorways` runtime-kit call.
Cockpit ADR is now 0214 because main independently used 0207 for golf bunkers.

Headless regression: 1275 passed, zero failures. Native Unity EditMode: 1674 total,
1672 passed, zero failures, two existing inconclusives (`FleetMarket_SaysASharedLockOnce`
and `Storm_IsAGroundStop`). Asset audit passed: 1589 GUIDs, 347 mirrors, 70 character
materials. Diff whitespace check against main passed. Unity's shared compiler
stalled; the same assembly compiled successfully without `/shared`. Removing
that flag from this worktree's ignored Bee cache allowed the actual Unity test
runner to complete. This local workaround changes no shipped source or install.

A clean Mac build passed at `bc20661f`. Main then advanced to `245e7b53` with the
selection-card redesign. Final integration keeps its Follow/Following toggle,
close button, telemetry and journey, with Cockpit in a separate bottom row.
Native Unity regression passed again: 1677 total, 1675 passed, zero failures,
the same two existing inconclusives.

Final headless regression: 1278 passed, zero failures. Final clean Mac player
build passed from `a99a0390`, including the selection-card integration, at
`/private/tmp/airside-cockpit/work/builds/Airside.app`. Build completion proves
compilation and packaging; a new complete cockpit flight/performance playtest
is still not claimed.
