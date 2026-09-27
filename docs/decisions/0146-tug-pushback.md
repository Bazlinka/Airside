# 0146 — Pushback like a tug does it

Date: 28 September 2026. Author: Claude, at Bailey's request ("aircraft don't … push back
realistically"). Bailey chose a tug push with a turn.

## Context

- **One pushback path per stand.** Each stand had one baked OSM pushback, whichever runway was
  in use. For one runway end the tail swung toward the way the aircraft would taxi, so it ended
  the push facing the wrong way.
- **Sideways skating.** To hide that, `GroundLeg` blended the nose from a quarter of the way
  through the push toward the taxi heading while the airframe followed the push path. The aircraft
  skated sideways along the whole push (`docs/testing/ground-2026-09-28/`, earlier renders).

## Decision

- **`Simulation/PushbackGeometry`** builds each push for its taxi route:
  - straight back from the stop along the stand line;
  - a tail swing on the aircraft's own turning radius (1.4 × nose-to-main-gear, at least 12 m),
    away from the direction it will taxi;
  - a few metres straight along the taxilane, so it stops nose-first down it.

  The taxi-out then starts at the push end and runs forward to rejoin the baked route 30 m past the
  corner.
- **Taxilane hint.** Where a route leaves the push point almost along the push line (the 23 routes
  from the east gates), the taxilane's line is taken from the stand's 05 route, facing the way this
  route travels.
- **Fallbacks.** Walk-out stands keep their painted lines. Anything that doesn't suit a clean corner
  also keeps the baked pair.
- **`GroundLeg` tug turn.**
  - A push that already ends within 1° of the taxi heading gets no extra turn.
  - One within 60° has only that leftover turned in over the last fifth of the push.
  - Only a baked push that ends the wrong way round still gets the old full turn.
- **Unchanged.** The push speed (3 kt), the tug disconnect pause and the drawn tug are unchanged,
  and the tug follows the aircraft.
- **Timing.** Timings and the ground-traffic checks come from the same legs, so the simulation
  stays consistent.

## Verification

- Top-down renders for 05 and 23 (`docs/testing/ground-2026-09-28/pushbacks-05-and-23.png`) show the
  nose following the push and the aircraft finishing nose-first down the taxilane.
- The heading step at tug disconnect is 0° on every stand and runway.
- The type-check is clean. Bailey asked to skip new tests this round.

Mac checks:

- pushes from the terminal gates on 05 and on 23;
- the tug stays on the nose;
- no slide at the disconnect;
- `GroundSeparationTests` and `TerminalGateOperationsTests`, because push paths changed.
