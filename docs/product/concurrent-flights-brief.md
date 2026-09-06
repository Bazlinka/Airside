# Design brief: accepted routes add real flights

Status: **designed** — see `docs/decisions/0019-concurrent-commercial-flights.md`
and first slice packet `docs/product/concurrent-flights-slice1-packet.md`.
Owner: Cursor (design 2026-09-06); implementation unassigned.

## Why this is a decision, not just a task

Today `AirportSimulation` runs exactly **one** commercial flight, looping
back-to-back (~7 flights per simulated day, no idle gap). Accepting airline routes
raises `Routes.ScheduledFlightsPerDay` but nothing acts on it — the airport does
not actually get busier. Making routes add flights means **more than one
commercial flight operating at once**, which is the core architectural step the
project plan has been building toward and deliberately deferring until the single
loop was stable. It is now stable (48+ edit-mode tests, deadlock-free multi-aircraft
ground traffic, persistence, economy, reputation).

The single flight is currently entangled with: `ActiveTurnaround`,
`AssignedStand`, `ActiveTaxiRoute`, the economy settlement, reputation, the event
log, `_flightSettled`, `CompletedCycles`, and the `DepartureResetSeconds` reset.
The ground-traffic fleet (`GroundTrafficAircraft`) already solved shared-resource
contention with a priority rule and a single-file corridor lock, but it assumes
**one aircraft has absolute priority and everything yields to it** — that
assumption breaks with two co-equal commercial flights.

## What "done" looks like

- The number of commercial flights operating scales with accepted routes: 0 extra
  routes = today's single loop; more routes = 2, then 3 concurrent flights, up to
  a capacity limit (stands, runway).
- Each flight has its own arrival, stand, turnaround and departure, settles its
  own revenue/delay/reputation, and appears in the event history by id.
- Two flights never deadlock and never occupy the same segment or stand; when
  they contend, one waits and the wait is explained (reuse `TrafficWaitMonitor`).
- Deterministic, frame-rate independent, and reconstructed exactly on load /
  offline catch-up — same bar as everything else.
- Existing guarantees hold: fifty-cycle soak, economy figures, persistence tests
  (some will legitimately change and need re-baselining — call those out).

## Design answers (locked in 0019)

1. **Promotion model** — primary becomes `Flights[0]` in a commercial list.
2. **Priority** — commercial FIFO by spawn time; fleet yields to any commercial;
   corridor lock stays fleet-only.
3. **Capacity** — concurrent commercials capped by stand count; v1 approach-wait
   if stands busy; hard accept gating deferred.
4. **Cadence** — second flight when `ScheduledFlightsPerDay >= 4`, half-cycle
   stagger; always keep one loop when S = 0.
5. **Economy** — per-flight settlement already composes; call it per departure.

## Suggested first slice

See `docs/product/concurrent-flights-slice1-packet.md`: exactly two commercials,
gated on schedule demand, fifty-cycle soak, single-flight path unchanged.
