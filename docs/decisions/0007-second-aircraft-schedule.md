# Decision 0007: the second aircraft runs a schedule

Date: 2026-09-06

## Decision

The second aircraft (`GroundTrafficAircraft`, `GT-201`) no longer shuttles
endlessly along A1 and A2. It now runs a repeating schedule: taxi in on A1, taxi
in on A2, taxi to Stand 2, hold at Stand 2 for a dwell, taxi out on A2, taxi out
on A1, depart, then wait a gap before the next arrival.

Each leg is an ordered entry with the resources it needs, its end points and its
duration. The aircraft holds its current leg's reservations until it can take the
next leg's, so it releases each segment progressively. The primary flight keeps
absolute priority through the existing yield step, so `ReservationConflicts`
stays zero and the second aircraft's waits are recorded only in the traffic wait
monitor.

## Reason

A fixed shuttle proved the shared-segment contention but was not a believable
operation and did not exercise stand contention. A schedule with a stand visit
is the next step toward several simultaneous aircraft and gives the reservation
system a second aircraft that competes for a stand as well as the taxiways.

Keeping the leg list data-driven means adding intermediate holds, a second stand
option, or a third aircraft later is a change to the table rather than the
control flow.

## Consequences

- The presentation layer shows the second aircraft's phase name ("Taxi to
  Stand 2", "At Stand 2", "Departing", ...) and whether it is holding.
- Save compatibility is unchanged: the schedule is a pure function of elapsed
  simulated seconds and the reservation table, with no new persisted fields.
- A future real scheduler will vary timings and add more aircraft; the leg model
  and the priority rule stay.
- When the primary flight is assigned the same stand the second aircraft is
  already committed to (its choice was locked in earlier in the arrival), the
  second aircraft yields the stand and holds on the taxiway until the flight
  departs — visible through the traffic wait monitor.

## Follow-up (same day)

The second aircraft now chooses its stand at the start of each arrival: it reads
the primary flight's current assignment and targets the other stand, rebuilding
its leg list (`BuildCircuit`) for Stand 1 or Stand 2 geometry. The choice is
locked for that arrival, so a later primary reassignment to the same stand is
resolved by the yield rule rather than by the second aircraft switching mid-taxi.
This keeps the two aircraft off each other's stand in the common case while
staying deterministic (the choice is a pure function of the seeded assignment).
