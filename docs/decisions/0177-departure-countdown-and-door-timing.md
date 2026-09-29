# 0177 — Departure countdown and door timing

Date: 29 September 2026

## Context

Each stand type ran its own clock before a push. Aerobridge gates shut the door about 260 s
out, stair trucks at 240 s, turboprop airstairs at 25 s, and the tug arrived at 150 s. A
turboprop's No.2 engine started 120 s out while its door was open and passengers were
boarding (a player's Boarding stage forced the door open until the push), and jets started
their engines at the gate. Doors swung at a fixed rate on unscaled real time, so they moved
while paused and lagged at high game speed, and the hold doors simply followed the passenger
door.

## Decision

- `Simulation/DepartureCountdown` is the one timeline, from the booked push T:
  hold shut T-5:00 (after a player's Baggage stage), a player's prep and boarding done T-4:00,
  door shut T-2:30 after the last walkers and a headcount, bridge or stair truck away from
  T-2:10, beacon once they are clear (turboprop T-1:50, jet T-1:00), a turboprop starting No.2
  then No.1 on the stand, and a jet starting them during the push (T+15 s, T+45 s).
  Late steps close up behind a late turnaround rather than overlapping it.
- A player's prep now finishes `PrepEndsBeforeSeconds` (240 s) before the push, and the
  planner's lead includes it.
- `EngineState` carries a 0..1 passenger-door and hold-door position. `BoardingFlow` gives both
  as eased functions of game time over each door's own time (8 s plug door, 10 s airstair, 6 s
  hold); presentation sets the door angles straight from them. The hold opens for unloading
  after arrival and for loading before departure; the passenger door for deplaning and boarding.
- Deplaning starts once the door or airstair has finished opening (12 s).

## Consequences

- Nothing can start or move out of order; `DepartureCountdownTests` checks every stand type for
  AI and player flights second by second.
- Player bookings need 4 more minutes of lead; a turnaround that starts late loses those minutes
  before the push is late.
- The demo circuit keeps its simple open-at-stand swing.
