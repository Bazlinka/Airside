# SF34 cockpit candidate evidence

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
Mac build and packaged evidence will be recorded below once verified.

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
