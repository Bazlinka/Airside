# Decision 0008: a ground-traffic fleet gated by a corridor lock

Date: 2026-09-06

## Decision

The single second aircraft becomes a small fleet of `GroundTrafficAircraft`,
managed by `AirportSimulation` as an ordered list. The first fleet is:

- `GT-201` — role `ArriveDepart`: taxis in, parks on whichever stand the primary
  flight is not assigned, taxis out, departs, repeats.
- `GT-202` — role `Reposition`: taxis in, holds in a run-up bay off the A2 end,
  taxis back out — never uses a stand. Starts 25 seconds after `GT-201`.

Every fleet aircraft reserves a single-file lock, `AirportTaxiNetwork.Corridor`,
for the whole time it is on the A1/A2 taxiway. Only one fleet aircraft can hold
it, so they queue instead of meeting head-on. The primary flight does not use the
lock — it keeps priority on the segments themselves through the existing yield
step.

## Reason

Phase five needs several simultaneous aircraft on the reservation system. A full
N-aircraft scheduler with routing and negotiation is a large change and touches
the economy, turnaround and persistence, which all still assume one primary
flight. A fixed-priority fleet on the ground-traffic side delivers the visible
and mechanical result now:

- The primary flight is never blocked (yield step), so `ReservationConflicts`
  stays zero and every existing guarantee holds.
- Fleet aircraft contend for the shared segments and stands and queue through the
  corridor lock; a prolonged wait is explained by the traffic wait monitor.

**Deadlock freedom** is by construction: the primary flight never waits, so it
always frees its segments; at most one fleet aircraft is ever on the corridor, so
the corridor holder is only ever contended by the primary and therefore always
makes progress; a repositioning aircraft never touches a stand, so it cannot form
a stand/corridor hold cycle with an arriving aircraft.

## Consequences

- `AirportSimulation.GroundTraffic` is now `IReadOnlyList<GroundTrafficAircraft>`.
  Presentation builds and moves one model per fleet aircraft and lists them in
  the HUD.
- Save compatibility is unchanged: each aircraft's motion is a pure function of
  elapsed simulated seconds, the reservation table and the primary flight's stand
  assignment, plus a constant start delay — no new persisted fields.
- Known cosmetic gap: when the primary flight preempts a segment a fleet aircraft
  was moving on, that aircraft snaps back to the start of its current leg. The
  presentation lerp softens it; a future scheduler will hold position instead.
- Next: a fairer corridor hand-off (currently fleet order wins after a preemption),
  more fleet aircraft, and eventually promoting the primary flight to a peer in
  the same list.
