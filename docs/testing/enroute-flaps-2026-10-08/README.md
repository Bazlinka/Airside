# Enroute flap phase correction

Task #586. Owner: Codex / Bazlinka. Original base `3fcb8063`; current review updated to main `7020cf23`.

## Cause and change

`UpdateAircraftViews` starts from the local airport operation phase, then draws a separate regional journey position. An `Inbound` aircraft uses the local `Approach` phase for the return to Adelaide. The regional override covered only the first 120 seconds of return departure; after that, it left `Approach` active throughout return cruise. `FlapDegrees` requests 8–22 degrees in approach, instead of cruise's zero. Landing gear and other visual consumers also received that incorrect approach phase.

The supplied exterior screenshot shows deployed trailing-edge surfaces, but does not identify its journey stage. The source establishes a return-cruise phase bug; this fix is not yet a native confirmation of that exact frame. Flaps during takeoff or approach are expected.

A second independent cause explains departure-side stuck flaps: `RefreshFleetFollowTargets` called `EnsureFleetPickables` whenever the visible/followable set changed. The latter evicted every existing view's animation cache, even with its proxy/marker already present. `ControlSurfacePart` then captured the current deployed rotation and aft position as its new rest pose; commanding zero could no longer return to the authored wing plane. The same rebuild could recapture folded gear too. Refresh only the cached marker references and child-name snapshot, preserving the rig's rest poses, actuator deflections and articulation history. Newly created views still classify normally on first use.

Select the phase from the journey stage through pure `RegionalFlightPath.JourneyPhase`: clean `Departed` during cruise in both directions; return `Takeoff` until the aircraft-specific rotation time; destination `Approach` for the last 180 seconds and `Landing` for the final 40 seconds; `AtStand` during turnaround. Existing progress, slew rate, path geometry, injected time, simulations, runway clearance, save v22 and assets remain unchanged.

## Original headless verification (before Mac review)

- After the departure-side cache correction: **87 focused flight/articulation/pick tests passed**. The changed runtime cache path and added Unity regression are excluded from headless compilation; their native result remains unverified.
- Focused `FlightWorldTests`: 34 passed, including 12 journey-phase regressions at cruise/departure/approach/rollout boundaries.
- Two native regression cases check that the selected outbound/inbound cruise phase commands zero flap deflection. An additional native case deploys a flap, creates/refreshes pickables twice, and invokes the real cruise actuator update to require its original rotation/position and preserved cache. Unrun in Unity; excluded from the headless harness.
- Full `scripts/test-domain.sh`: **1,917 passed / zero failed**, 5 m 15 s. Includes generated harness consistency and NUnit 3.5 compile without implicit usings. Native cases are excluded from this headless total. C# 9 syntax: six changed files, zero errors. Asset audit: 1,811 unique GUIDs, 388 mirrors, 70 materials. Presentation map regenerated; diff check passed.

## Packaged acceptance still pending

Focused native checks now pass (see current main review below). Watch an Adelaide-based flight from departure to destination and back in exterior/window views. Cruise flaps must retract after their actuator transition; return departure and destination approach retain correct configuration. Change follow targets while departing, then verify flaps fully retract and gear returns to its original stowed pose. Check gear, lights, engine visuals and pitch at the regional/local handoffs. Pausing should freeze actuator motion. No packaged player journey or screenshot capture was performed in this task.

## Current main review (8 Oct, Mac)

Updated PR #588 against main 7020cf23 (includes opening/options and save recovery).
Resolved the AircraftVisuals conflict by retaining main's per-aircraft rotate duration
and RotateProgress while selecting flap/gear phase from the actual regional journey.
Two added jet/turboprop cases exercise aircraft-specific return rotation boundaries.

Native Unity compiled the combined source and passed all 295 focused tests: flight
world/journeys, presentation bug sweep, flight performance, aircraft performance,
articulation/rig, pick routing, departure transition and fleet flight clarity.
This includes all three formerly unrun native flap/cache cases. Zero failures,
inconclusive or skipped cases. Summary: review-native-results.json.
Asset audit passed: 1,886 GUIDs, 407 mirrored art files, 70 materials.
Full headless: 2,003 passed / the same 3 main failures (busy-day ground separation
and both night-sky review checks). NUnit 3.5 compilation and generated harness check
passed. Packaged flight/playtest remains unverified. Summary: review-headless-results.txt.
