# Decision 0005: segment clearance and traffic wait diagnostics

Date: 2026-09-06

## Decision

Aircraft reserve only their current taxi segment, plus their assigned stand while taxiing in. Moving to the next segment atomically replaces the previous reservation. A reservation owned by another aircraft cannot be replaced.

Every blocked request enters a traffic wait monitor keyed by aircraft and resource. A wait lasting ten simulation seconds becomes a player-readable traffic warning naming the aircraft, elapsed wait and blocking segment. The warning clears when clearance succeeds.

## Reason

Whole-route locking prevents collisions but wastes capacity and hides the decisions that make taxiway design interesting. Progressive clearance allows multiple aircraft to share a route safely. Explicit wait diagnostics give tests and players evidence when traffic stops moving.

## Consequences

- The second-aircraft scheduler must request clearance before advancing into a segment.
- Segment order must remain deterministic when several aircraft request the same resource on one tick.
- Future deadlock recovery may reroute or reprioritize traffic, but it must preserve the diagnostic event.
