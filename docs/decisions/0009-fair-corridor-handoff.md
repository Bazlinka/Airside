# Decision 0009: fair corridor hand-off

Date: 2026-09-06

## Decision

When the shared A1/A2 taxi corridor is free and more than one ground-traffic
aircraft is ready to enter it, `AirportSimulation` grants it to the aircraft that
has been waiting for it **longest**, with fleet order breaking ties. The wait
start time comes from the existing `TrafficWaitMonitor` (a new
`TryGetWaitStart` accessor).

`GroundTrafficAircraft.Reposition` takes a `mayEnterCorridor` flag. On a leg that
needs the corridor, an aircraft that does not already hold it and is not granted
entry holds short and records the wait — it does not even attempt the
reservation.

## Reason

Decision 0008 gated the corridor with a single reservation, which prevented
head-on conflicts but let fleet order decide who entered next. After the primary
flight preempted an aircraft off a segment, that aircraft could immediately
retake the corridor ahead of one that had been holding short for much longer —
visibly unfair and, in a longer soak, a starvation risk.

A longest-waiting rule is the smallest change that makes the queue fair. It reuses
the wait timestamps the traffic monitor already keeps, so there is no new state to
persist and the decision stays a pure function of the simulated timeline.

## Consequences

- `GroundTrafficAircraft.Reposition` signature gains `mayEnterCorridor`; callers
  (the simulation, and tests) pass it explicitly.
- New `GroundTrafficAircraft.WantsCorridorNow` lets the simulation see which
  aircraft are queued without poking at internals.
- Save compatibility unchanged — no new persisted fields.
- A soak test asserts neither fleet aircraft is starved (each completes at least
  three full circuits) across forty primary-flight cycles, with zero primary
  conflicts.
- Still not a real scheduler: the rule is first-come-first-served on one corridor.
  Route choice, multiple corridors and priority classes come later.
