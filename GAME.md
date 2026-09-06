# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Current milestone

Phase two: make the aircraft turnaround a readable management decision. The prototype now coordinates passengers, baggage, fuel and cabin cleaning at the assigned stand. A cleaning disruption can delay boarding, the cause is shown to the player, and the completed flight earns revenue minus delay costs. The player can spend $300 on a priority crew during turnaround to recover time.

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Time comes from an injected clock.
- Random choices come from a seeded source.
- Runways, taxiways and stands must be reserved before use.
- Frame rate must not change simulation outcomes.
- No external data or asset enters the project without a recorded licence.

## Run it

Open `game/Airside` in Unity 6.3 LTS and press Play.

- Space: pause or resume simulation
- Tab: switch between 1× and 4× time
- Right-drag: orbit camera
- Scroll: zoom
- WASD: pan overview
- F: follow aircraft
- O: return to overview
- P: hire a priority turnaround crew while the aircraft is at stand

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.

## Current evidence

- Eleven edit-mode tests pass.
- A fifty-cycle simulation completes without reservation conflicts.
- Large and one-second time steps reach identical simulation state.
- Turnaround dependencies, disruptions, priority crews and delay costs are covered by tests.
- The project compiles in Unity 6.3 LTS on the development Mac.

## Next work

Add a versioned save snapshot and offline catch-up report, then replace the single path with a named taxi graph. Record transition events, measure frame rate, and run a longer visual soak before introducing a second simultaneous aircraft.
