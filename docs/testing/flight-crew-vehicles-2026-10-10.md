# Flight crews and vehicles — 10 October 2026

Issue #779. Two pilots plus one/two/four cabin crew, automatic provision; captain inspection sets a physical minimum player Fuel stage. Existing stairs/bridges own boarding geometry. Service driver poses, vehicle opaque tints, independent front steering and clock-driven motion/lamps.

78 focused crew/preparation/boarding/engine/diagnostic/planner/contact checks and 98 related UI/crew/baggage/countdown checks passed. The P0 remaining capture-delay lock passed with boarding delays 408/411 s (Saab midpoint now 403 s).

Previous PR #776 CI: compared its 16 failed cases against clean pre-crew main 229ef893. Ten failures reproduced there: land-cover palettes, three Emirates/Qatar evening cases, consecutive arrivals, ground separation, helicopter economics and three sky-traffic cases. Six previously passing cases depended on old preparation/capture times; corrected Commands, separate-apron departure, DevTools departed-aircraft, FlightPlanner takeoff timing and the two boarding capture samples. No test assertions or geometry/traffic rules disabled. Baseline selected cases only, not a full suite. Local log: `work/ci-before.log`; downloaded old job log: `work/pr776-job.log`.

## Native and packaged evidence

Unity 6.3.23f1 imports/compiles and renders the final **3498e79c** revision. Selected rendered pilot/vehicle frames opened and inspected: navy pilot/cap and preserved skin, baggage train solid cab/carts with seated driver below the canopy, opaque fuel truck and amber lamp under daylight/dimmed-night lighting. Review captured 12 frames; this is isolated native character/service geometry, not an exhaustive normal-game camera or animation matrix. Female/cabin uniforms, continuously moving steering and whole-airport night/dusk readability were not individually visually approved.

Clean final universal Mac build **3498e79c** succeeded (`Build Finished, Result: Success.`); x86_64/arm64 executable. Nine booking/save/cancellation/view actions passed on this exact final build, zero runtime errors, isolated save files. **7592be76** also passed the 40× ADL–KGC round trip in 201.97 s with departure/outbound/destination/inbound/landing/taxi-in/completed overview observed, and nine feature steps. Follow and completed-overview PNGs opened. The final runtime delta is freight crew count (two pilots, no cabin staff) and driver canopy fit; regional simulation and readiness code are unchanged from the round trip.

After merging #780 timing fixtures, 78 focused checks and capture-delay lock passed again. CI caught a stale generated harness after the planner fixture was re-included by the Unity-compatible Math qualification: regenerated it, `--check` passed, and 21 crew/planner/engine checks passed. Optional full headless CI still has the historical baseline limitations; no CI-green claim.

Evidence: [pilot](flight-crew-vehicles-2026-10-10/SF34_None_t20.png), [baggage train](flight-crew-vehicles-2026-10-10/A320_Baggage_t44.png), [night fuel scene](flight-crew-vehicles-2026-10-10/night-fuel.png), [final feature summary](flight-crew-vehicles-2026-10-10/final-summary.json), [round-trip summary](flight-crew-vehicles-2026-10-10/round-trip-summary.json). Private local runs: `work/agent-gameplay/20261010-181016-afadcdcd` and `work/agent-gameplay/20261010-182603-09cf3714`.

Freight pilot boarding, terminal interior/cockpit occupants, full-role wardrobe/boarding close-ups, frozen-clock behaviour in a player pause control, nominal wheel-radius fidelity and performance remain unverified. Personal running game/saves are untouched. Personal running game/saves are untouched. No full suite, soak, performance or terminal/cockpit occupant modelling claim.
