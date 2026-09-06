# Decision 0015: stands gate route acceptance

Date: 2026-09-06

## Decision

`AirportRoutes.Accept` (and therefore `AcceptPendingRoute`) refuses a standing
offer when accepting it would push `ScheduledFlightsPerDay` above

```
standCount × FlightsPerStandPerDayCap
```

with `FlightsPerStandPerDayCap = 6`. On today's two-stand airfield the schedule
cap is **12 flights/day**. The offer stays pending so the player can decline it;
no `accept-route` command is recorded on a refused accept.

`FitsScheduleCapacity(standCount)` is the shared predicate for simulation and HUD.
The HUD disables Accept and shows "Schedule full (N/cap)" when the offer does not
fit, in the same style as the reputation gate.

Stand count is currently the constant `AirportSimulation.StandCount` /
`AirportRoutes.BaselineStandCount` (2). A later buildable capacity upgrade should
pass the live stand count into `Accept` instead of introducing a parallel rule.

## Reason

Accepted routes already raise `ScheduledFlightsPerDay`, and concurrent-flight work
will turn that number into real aircraft. Without an accept gate the player can
stack unlimited schedule demand onto two stands. Cap-at-accept is the cheapest
correct control: deterministic, command-safe (refuse → no persisted command), and
independent of the concurrent-flight spawn slice.

## Consequences

- No save-schema change. Replay still rebuilds accepted routes from commands;
  a command only exists when accept succeeded under the cap.
- Declining remains available at capacity so a stuck offer can clear.
- When a third stand ships, raise the cap by passing the new stand count — do not
  invent a second capacity rule.
- Explicitly separate from the concurrent-flight *spawn* threshold (second aircraft
  when schedule ≥ 4): that gate controls airside activity; this gate controls
  contract backlog.

## Migration impact

None. Existing saves with fewer than 12 scheduled flights/day behave as before.
Saves cannot already exceed the cap because accept previously had no upper bound
only in live play going forward — old saves that somehow exceeded it would still
replay their recorded accepts; the gate only applies to new accepts.
