# AI freight traffic — task packet

Approved by Bailey, 6 October 2026. Branch: `feature/ai-freight-traffic`.

## Outcome and scope

Qantas Freight and DHL Air narrowbody freighters join Adelaide traffic with
operator colours, cargo titles and dawn/late-evening schedules. Add the carriers
on new games and normal existing-game loading without displacing parked aircraft.
Domain Airline, FleetAircraft, AirlineOperations AI scheduling, game startup/load,
shared runtime paint, passenger boarding equipment, and deterministic tests.
Decision: ADR 0232.

## Acceptance

- Two dedicated freighters join once, preserving existing passenger aircraft.
  Reserve QFR/DHL from player setup to avoid code collisions.
- Only fitting free stands are used; a full compatible apron starts cargo away.
  Regional-only airports do not gain unsupported aircraft or empty airlines.
- Cargo uses its own reachable domestic network and Adelaide-local banks; it
  respects curfew and retains deterministic scheduling across daylight saving.
- Save/load preserves role, state and published/actual timetable. No schema change.
- Cargo has no passengers, passenger bus, stairs or bridge docking; hold doors
  retain their shared loading timeline. AI cargo cannot settle player earnings.
- Red/yellow carrier paint remains readable; player conversion paint stays dark.

## Checks and invariants

Focused cargo, freight, boarding, bridge, save and traffic tests, full headless
regression including 30-day soak, generated harness, syntax/whitespace and metadata
checks. Native Unity compile/EditMode and runtime colour review remain separately
recorded. Preserve injected time, seeded randomness, reservation ownership,
exactly-once settlement and existing player contracts/economy. No external assets,
new aircraft geometry, cargo apron/loaders or outstation cargo implementation.
