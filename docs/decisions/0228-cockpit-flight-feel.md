# 0228 — Cockpit flight feel: per-type attitude, head/body motion, live attitude indicator

Date: 2026-10-01. Decision: make takeoff, climb, approach, flare and rollout feel like flying the
real type from the left seat. Presentation only; no simulation, save or path changes.

- **Per-type body pitch** (`AircraftAttitude`): pitch = flight-path angle + angle of attack, keyed
  to each type's own rotate/flare/touchdown timing. Jets rotate at ~3 deg/s to a ~15 deg climb
  (widebodies ~12.5), flare to ~5 deg and touch down main-gear first; turboprops stay flatter
  (ATR ~7.5, Dash 8 ~11). Every jet previously flew the ATR's 7.5 deg climb and 6.5 deg flare.
  ATR figures and the existing `PitchDegrees(phase, progress)` are unchanged.
- **Head and body motion** (`CockpitMotion`, driven by observed kinematics only): runway joint and
  centreline-light thumps tied to distance rolled, tyre/engine vibration (turboprops stronger),
  unstick, touchdown jolt scaled by a deterministic per-flight firmness (smooth to firm) plus a
  nose-wheel slam, gear retract/extend thumps and buffet, low-level approach turbulence, thrust
  push-back and braking lean, and gaze leading into turns. Bounded to +/-14 cm, 4 deg pitch,
  3 deg roll; damped when paused; thumps are capped under fast-forward.
- **Live attitude indicator**: PFDs on all ten jets and the ATR/Dash show real pitch and bank
  (sky/ground/ladder texture, UV driven) instead of bank only. Telemetry adds vertical speed and
  uses wheel height (gear-pivot lift removed) so a pitched rotation still reads on the ground.
- Not done: crosswind crab/decrab (no wind model), Saab live ADI, interactive flying.
- Native Unity review of ground roll, rotation, descent, flare and touchdown is still required.
