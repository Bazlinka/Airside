# SF34 cockpit candidate evidence

**Latest visual rebuild:** see [revision 2](revision-2.md). Files without a `v2-`
prefix below are the superseded first blockout, retained as historical evidence.

Branch: `feature/saab-cockpit-mode`; starting point `e55e3c30` (remote main).

- Baseline headless suite: 1194 passed.
- Changed full headless suite: 1198 passed, zero failures.
- Final focused CockpitAvailabilityTests: 5 passed, including disabled-action layout.
- Unity full EditMode: 1587 total, 1585 passed, zero failures, two existing
  unmet-precondition inconclusives: FleetMarket_SaysASharedLockOnce and Storm_IsAGroundStop.
- First native attempt found remote-main's orphan AttachDoorways call; removed that
  single call without copying the original checkout's unfinished doorway work.
- First camera test run failed because EditMode AddComponent does not initialize
  this MonoBehaviour. The fixture now invokes Awake and captures camera input.
- Native graphics renders: forward, left, panel and bank. First renders showed
  a roof gap and instrument text overflow; both corrected before final render.

Native interior stills use a simple review runway and the real runtime SF34 model.
They prove geometry/rendering, not the complete airport journey or controls.
Mac player build passed from clean committed source `3e377fe7`. Final focused
camera tests passed 2/2, including local fog distance and clip restoration.

## Packaged startup review

`game-startup.png`, `game-after-startup.png` and `game-startup.log` come from that
Mac player at 1600x900 in the actual airport. The review entered SF34 VH-ZRC at the
first nonzero engine spool and retained the same aircraft and seat pose for both
captures. The entry log rounds this tiny positive spool to 0.000; subsequent
telemetry confirms both engines reach 1.00. Both screenshots show AtStand,
GS 0 kt and height 0 ft. The second still is **not flight or taxi evidence**.
The interior, windows, compact HUD and panel text render without exterior-shell
occlusion. Ground equipment and boarding people remain visible outside.

No managed exception is recorded. The run contains a 196,372 ms frame stall,
so it provides no performance acceptance. Its cause is not established. It also
quit after the requested captures rather than completing a 14-minute soak.
Taxi, takeoff, arrival and automatic local-view exit still require actual evidence.
The script now calls the second image `after-startup.png` to avoid implying an
unobserved phase. A private per-invocation TMPDIR was used for the successful
rebuild after a shared compiler stall; other checkout/build processes were untouched.

## Remaining acceptance

- Full local departure and arrival, including holds, go-around and shutdown.
- Repeated entry/exit via actual UI, Esc/R/input ownership, changed/removed target.
- Day/dusk/night and rain/fog/storm motion review at supported window sizes.
- Manual listening and frame-time comparison against external follow.
- Candidate visual acceptance before any fleet-wide interior production.

Reproduce native stills with CockpitAppearanceReview.Run in Unity.
Run `scripts/review-cockpit.sh` after `scripts/build-mac.sh` for real-time packaged
SF34 captures. It uses a fresh soak career and leaves the player's save alone.
The capture aborts if no eligible cockpit is active; it cannot substitute overview evidence.

Initial packaged review at fixed T+220s correctly aborted with no PNG: the player
SF34 was still cold. The soak's old four-minute comment does not include the
current prep lead. The review now chooses an eligible SF34 departure (player
preferred, then registration order) and starts its shot timers at real cockpit
entry. It does not skip preparation, advance time or force engines. It waits at
most 600s for entry and still aborts if the actual cockpit is lost before a shot.
