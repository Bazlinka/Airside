# Human operations task packet

Date: 28 September 2026  
Branch: `feature/human-operations`

## Player-visible outcome

Every parked passenger aircraft should look worked by people. Passengers deplane and board through
the access appropriate to the stand: integral turboprop airstairs, stair truck plus apron bus at a
remote jet stand, or inside a docked aerobridge. Ramp teams marshal arrivals, place safety equipment,
fuel, cater, handle bags, supervise boarding and support pushback.

## Scope

- `Simulation/BoardingFlow.cs`: deterministic passenger, stair-truck and remote-bus timelines.
- `Simulation/RampCrew.cs`: deterministic crew roles, tasks, positions and progress.
- `Presentation/AirsidePrototype.Boarding.cs`: pooled people, buses, paths, animation and equipment.
- Existing Quaternius CHR-003 people and existing GSE models only; no new external asset source.

## Invariants

- Humans are a presentation of existing fleet and turnaround state, not autonomous simulation agents.
- Pause, time acceleration, reload and catch-up reconstruct the same activity from simulation time.
- Passenger presentation never changes departure timing, reservations, staffing or save schema.
- Passenger paths stay clear of the fuselage centreline and use the aircraft's real access equipment.
- A global cap and object pools bound animated characters and vehicles.

## Acceptance criteria

1. All three Adelaide boarding contexts show people: airstair, remote bus/stairs and aerobridge.
2. Remote buses receive arriving passengers, clear, return for departure and leave before pushback.
3. Up to eight concurrent aircraft can show role-correct crew, not one global crew pair.
4. Crew visibly animate and carry task-readable equipment for all seven activity families.
5. Passengers use offset walking lanes on the apron and single-file stairs.
6. Domain and human-specific Unity tests pass; the project compiles in Unity.
7. A graphics-on packaged playtest checks clipping, scale, bridge visibility and frame time.

## Must remain unchanged

- Turnaround duration and staffing economics.
- Flight, stand, runway, taxi and service-vehicle reservations.
- Save format and deterministic simulation outcomes.
- CHR-003 licensing and committed Unity material GUIDs.

## Playtest matrix

- Saab/Q400/ATR: deplane and board via the integral forward airstair.
- 737/A320 at 20R, 22R or 27–29: bus arrives, passengers transfer, stair truck clears before beacon.
- Jet at a bridged T1 gate: people are visible through bridge glazing only while fully docked.
- Player turn: fuel, catering, baggage and boarding crew actions match the active prep stage.
- AI-heavy period: several stands show different teams without duplicated global equipment.
- Pushback: headset worker and wing walker appear during tug approach and clear before taxi.

## Verification record

- Domain suite: 972/972 passed.
- Human Unity EditMode suites: `BoardingFlowTests` 7/7 and `RampCrewTests` 14/14 passed.
- Full Unity EditMode: 1301 passed, 3 failed and 2 inconclusive. The three failures are outside this
  slice: opening Q400 count, busy-day separation conflicts and a GATE-13/GATE-24 expectation.
- Mac build: passed at `work/builds/Airside.app`.
- Packaged graphics-on soak: six minutes, clean completion, latest windows at 60 fps / 16.7 ms p95.
- Manual/close-up visual acceptance: still required. The first automated shot fired after the followed
  aircraft had pushed; the corrected close capture stalled before its first heartbeat. Do not treat
  clipping, bridge visibility or character scale as visually approved yet.
