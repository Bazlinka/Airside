# Task packet: concurrent flights — slice 1 (two commercials)

Depends on: `docs/decisions/0019-concurrent-commercial-flights.md`  
Status: **implemented** (slice 1 on `cursor/concurrent-flights-slice1-38b9`)  
Out of scope: N>2, accept-route capacity gating, presentation polish beyond a
second visible aircraft.

## Player-visible outcome

When the airport has enough accepted route demand
(`ScheduledFlightsPerDay >= 4`), a second commercial aircraft operates at the
same time as the first. Stands and taxi segments never double-book; waits are
explained. With fewer scheduled flights, behaviour matches today's single loop
exactly.

## Files / modules

- `Simulation/CommercialFlight.cs` (new) — per-flight state
- `Simulation/AirportSimulation.cs` — list, spawn, tick, yield-to-any-commercial
- `Simulation/GroundTrafficAircraft.cs` — only if stand-picking helpers need to
  know "any busy commercial stand"
- `Tests/EditMode/*` — migrate primary accessors; add dual-flight soak
- `Presentation/AirsidePrototype.cs` — second aircraft transform

## Invariants

- Injected clock; seeded random unchanged for the single-flight path
- Reserve runway / taxi / stands before use
- Frame rate must not change outcomes
- Fleet corridor lock remains fleet-only; commercials use segment priority
- No save-schema bump; rebuild via replay

## Acceptance criteria

1. `ScheduledFlightsPerDay < 4` → exactly one commercial; existing seed/timing
   tests still pass (byte-identical where previously asserted).
2. `ScheduledFlightsPerDay >= 4` → second commercial spawns on half-cycle stagger
   when a stand is free.
3. Fifty dual-flight cycles: zero reservation conflicts; corridor invariant held.
4. Large vs small time steps → identical commercial counts, cash, reputation.
5. Each departure settles its own revenue/delay/reputation/route income.
6. HUD / world shows two commercial aircraft when both are active.

## Test / playtest plan

- Edit-mode: dual-flight soak, step identity, single-flight regression
- Play: accept routes until `ScheduledFlightsPerDay >= 4`, watch two commercials
  and fleet without deadlock for several minutes

## Must not change

- Save schema version
- Corridor single-file rule for fleet
- Weather/research/staffing economics formulas (except additive per-flight
  settlement already implied)
