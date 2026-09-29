# 0183 — The main strip frees when the aircraft is off the pavement

Date: 29 September 2026. Author: Claude, from Bailey's report: "aircraft are still getting stuck on landing,
waiting for aircraft who have landed or are taxiing" (after ADR 0180 removed the far-taxi-in cause).

## Player-visible outcome

Arrivals wait much less behind the aircraft ahead of them, and departures wait less behind arrivals. Over a
simulated day (seed 2026): aircraft holding in the air 17,290 s → 10,312 s (−40 %), longest single air hold
1,215 s → 751 s, departures holding short 16,699 s → 12,257 s (−27 %), with the same number of landings and
takeoffs.

## Cause found

After a landing on 05/23 the tower kept the strip locked until the aircraft finished its whole vacate leg to the
E2 waiting point: 511 m, 150–200 s. The aircraft is off the 45 m runway after about 232 m (|z| ≥ 50 m). The
next landing or takeoff waited for the rest of the taxi, on top of the wake gap. 12/30 already released early
(ADR 0126, 480 m).

## Decision

`AdelaideGround.MainClearOfRunwaySeconds`: the main strip is free when the vacating aircraft's path point is
50 m (`MainStripClearMetres`) from the centreline, never earlier than 30 s and never later than the whole leg.
One function, used by the tower's clearance, the strip free time, the runway busy window and the arrival
estimate, so a resumed save matches an uninterrupted game.

## Safety

Unchanged and still enforced: the next arrival's vacate and the first 30 s of its taxi-in are checked against
every aircraft on the ground including the one ahead (`VacateClearOfTaxiing`), taxi-in is re-checked over the
whole route before it moves, and runway crossings still hold. `GroundSeparationTests.BusyDay_NoAircraftDriveThroughEachOther`
passes. `DualRunwayTowerTests.CrossStrip_ClearsFasterThanTheFullTaxiToE2` asserted the old rule for 05/23 and
now asserts the new one.

## Not changed

Wake separation times, the vacate leg itself, 12/30, crossings.

## Migration

None: the strip free time is derived from state times (ADR 0156).
