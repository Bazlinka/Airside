# Flight crews and vehicles — 10 October 2026

Issue #779. Two pilots plus one/two/four cabin crew, automatic provision; captain inspection sets a physical minimum player Fuel stage. Existing stairs/bridges own boarding geometry. Service driver poses, vehicle opaque tints, independent front steering and clock-driven motion/lamps.

78 focused crew/preparation/boarding/engine/diagnostic/planner/contact checks and 98 related UI/crew/baggage/countdown checks passed. The P0 remaining capture-delay lock passed with boarding delays 408/411 s (Saab midpoint now 403 s).

Previous PR #776 CI: compared its 16 failed cases against clean pre-crew main 229ef893. Ten failures reproduced there: land-cover palettes, three Emirates/Qatar evening cases, consecutive arrivals, ground separation, helicopter economics and three sky-traffic cases. Six previously passing cases depended on old preparation/capture times; corrected Commands, separate-apron departure, DevTools departed-aircraft, FlightPlanner takeoff timing and the two boarding capture samples. No test assertions or geometry/traffic rules disabled. Baseline selected cases only, not a full suite. Local log: `work/ci-before.log`; downloaded old job log: `work/pr776-job.log`.

Native compile/frame/build/execution evidence pending. Personal running game/saves are untouched. No full suite, soak, performance or terminal/cockpit occupant modelling claim.
