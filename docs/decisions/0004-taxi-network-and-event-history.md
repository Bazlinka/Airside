# Decision 0004: named taxi routes and operational history

Date: 2026-09-06

## Decision

Ground movement uses a simulation-owned taxi network made from stable segment identifiers and route points. Both prototype stands share Taxi A1 and Taxi A2, then use a stand-specific lead-in. Aircraft reserve the route segments during taxi movement, while Unity uses the route points to draw smooth motion.

The simulation also owns a bounded operational event history. It records flight assignment, landing, stand arrival, priority-crew decisions, turnaround delays, pushback approval and departure results with simulation timestamps and flight identifiers.

## Reason

Named infrastructure creates the foundation for real traffic conflicts and later construction choices. A visible event history makes cause and effect traceable during live play and after offline catch-up without exposing a technical debug log.

## Consequences

- New taxiways require stable segment IDs and validated connected routes.
- Shared segments must be reserved before movement.
- Presentation may smooth movement but cannot invent a route or operational event.
- The current whole-route reservation is conservative; multi-aircraft work will reserve and release individual segments as aircraft cross them.
