# 0166 — Departures turn properly, and the gear waits until the aircraft has climbed away

Date: 28 September 2026. Author: Claude, at Bailey's request (a play test: "as it turns to head
its destination it doesn't turn — it more so drifts in the air? And landing gear goes up too soon?").

## Context

**Drift.** After the far threshold, a departure yawed its nose towards 72 % of the destination
bearing. Its position, however, slid at most 380 m sideways and otherwise kept going down the
extended runway line. So the aircraft pointed one way and moved another, like a crab in a
crosswind, and never flew onto its course.

**Gear.** Retraction was a fixed fraction (0.12) of the takeoff phase, tuned to the ATR42. Across
the fleet it started from 7.2 s before lift-off (the heaviest jets, still on the runway) to 2.5 s
after it. Landing and taxi lights also went off the moment the takeoff phase ended.

## Decision

- **A flown turn.** `DepartureTurn.Arc` gives position and heading from one curve:
  - straight until the turn start (0.52 of the Departed phase, past the far threshold as before);
  - then a constant-radius arc onto the **full** bearing to the destination, capped at 171°;
  - then straight on that bearing.

  The radius is a coordinated 25° bank turn at the type's airspeed, r = v² / (g·tan 25°), about
  1 km for a turboprop and 2 km for a jet. `DepartureTurn.ArcBank` rolls in over 180 m, holds 25°
  and rolls out as the heading arrives. The nose always points along the path. Before the view hands
  off, jets turn about 30° and turboprops about 55–60°.
- **Gear per type, after lift-off.** `AirsideReusableMotion.GearBias(phase, progress, type)` starts
  retraction 3 s after that type's lift-off (from `AircraftPerformance`) and runs a 7 s cycle. The
  gear doors follow the same clock. The old untyped overloads keep the ATR42 timing.
- **Lights on through the climb.** Landing lights stay on through Takeoff and Departed.

## Evidence

- `DepartureTurnTests`:
  - `Arc_NoseAlwaysPointsAlongThePath`
  - `Arc_TurnsTheWholeWayOntoTheDestinationBearing`
  - `TurnRadius_FollowsSpeedLikeARealTurn`
  - `ArcBank_RollsInHoldsAndRollsOut`

  All 950 headless Domain and Simulation tests pass.
- `PresentationLayoutTests.Gear_StaysDownUntilEveryTypeHasClimbedAway` covers every catalogue type.
  Its body was run headlessly against Unity shims and passes.

## Not verified

**Not seen in Unity.** The turn is visible only in the last part of the Departed phase. Whether the
arc reads well from the tower camera still needs a play test.
