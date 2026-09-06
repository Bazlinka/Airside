# Decision 0019: concurrent commercial flights — design

Date: 2026-09-06

## Decision

Accepted routes will eventually drive **real concurrent commercial flights**.
This decision locks the architecture for the first implementation slices. No
gameplay code lands in this change — only the design, so the first code PR can
stay narrow.

### 1. Promotion model (not a parallel sidecar)

Refactor the current primary loop into the first entry of a commercial
`List<CommercialFlight>` (working name). There is no forever-special "the
flight" beside a second system.

- Each `CommercialFlight` owns: id, phase/operation, assigned stand, taxi route,
  turnaround, settlement flag, and event-log flight id.
- `AirportSimulation` owns the list, spawning, and the shared reservation /
  corridor orchestration.
- The existing single-flight fields (`ActiveAircraft`, `AssignedStand`, …) become
  accessors for `Flights[0]` during migration, then disappear once tests are
  moved.

**Why not a sidecar:** a parallel "scheduled arrivals" system would duplicate
settlement, stands, taxi and wait diagnostics, and leave the primary forever
special. Promotion churns ~15 tests once; a sidecar churns forever.

### 2. Priority: commercial FIFO, fleet always yields to any commercial

- **Among commercial flights:** earlier `SpawnedAt` wins runway, taxi segments
  and stand claims (stable id breaks ties). Waiting flights use
  `TrafficWaitMonitor` with an explained hold.
- **Ground-traffic fleet:** yield to **any** commercial flight's required
  resources for the tick (generalise today's "yield to primary" step). Corridor
  lock stays fleet-only; commercial flights still do not take the corridor lock
  — they keep segment priority via the yield step, same as today for the
  primary.
- Deadlock freedom stays by construction: every commercial makes progress under
  FIFO (no circular wait among commercials if stands are reserved before taxi
  commit — see capacity); fleet never blocks commercials; corridor remains
  single-file for fleet only.

### 3. Capacity: stands gate acceptance; airborne hold is out of scope for v1

- `MaxConcurrentCommercialFlights = Capacity.StandCount` (2 today; 3 after the
  third-stand upgrade merges).
- `AcceptPendingRoute` also refuses when projected schedule demand cannot fit:
  for v1, refuse accept if `Routes.ScheduledFlightsPerDay` would exceed
  `StandCount * FlightsPerStandPerDayCap` **or** simply gate the *second
  concurrent aircraft* on `ScheduledFlightsPerDay >= SecondFlightThreshold`
  without changing accept rules yet (first slice — see below).
- No airborne hold stack in v1. If both stands are busy, a due spawn waits in a
  deterministic "approach queue" (sim-only, not a visible stack) until a stand
  is free, with a wait diagnostic. Hard block of route accept comes when we
  generalise beyond two flights.

### 4. Cadence: map schedule onto staggered spawns

- Let `S = Routes.ScheduledFlightsPerDay` (sum of accepted routes' flights/day).
- Always run **one** commercial loop (today's behaviour) when `S == 0` (no
  accepted routes) — the airport still has GA/charter activity so the scene is
  never empty.
- When `S >= SecondFlightThreshold` (recommend **4**), allow a **second**
  commercial flight. Spawn the second aircraft at
  `primary.CycleStartedAt + CycleLengthSeconds / 2` (half-cycle stagger), then
  each re-spawns on its own cycle after departure reset.
- Visual chaos control: hard cap concurrent commercials at `StandCount`; stagger
  beats random spawn.

### 5. Turnaround / economy / reputation compose per flight

Already per-flight units (`TurnaroundWorkflow`, `Economy.CompleteFlight`,
`Reputation.RecordDeparture`, route income). Each commercial settlement calls
them independently with that flight's delay. Event log lines carry that
flight's id. `CompletedCycles` becomes total commercial departures (rename to
`CompletedCommercialFlights` when convenient).

Daily report / away summary keep summing economy and reputation totals — no
schema change.

## First implementation slice (approved)

**Goal:** exactly two commercial flights when `ScheduledFlightsPerDay >= 4`,
else one — prove fifty cycles with zero reservation conflicts and identical
large/small steps.

### Task packet

1. Introduce `CommercialFlight` (extract state from current primary fields).
2. `AirportSimulation.Flights` list; migrate tick loop to `foreach` commercials
   then fleet yield-to-any-commercial + corridor handoff.
3. Gate: if `Routes.ScheduledFlightsPerDay >= 4` and `Flights.Count < 2` and a
   free stand exists, spawn second flight at half-cycle offset.
4. Stand assignment: second flight takes the stand the first does not hold;
   if none free, delay spawn (approach wait) with diagnostic.
5. Settlement: each departure settles its own economy/reputation/route income.
6. Tests: fifty-cycle dual-flight soak; large vs small step identity; seed
   identity when `S < 4` (single-flight path byte-identical); accept-route still
   persists.
7. Presentation: second aircraft visual (reuse ground-traffic mesh style or
   duplicate primary prefab); HUD shows both flight ids.
8. Do **not** yet change route-accept capacity rules; do **not** yet go beyond
   two commercials.

### Explicitly deferred

- N>2 commercials, slot scheduling UI, airborne holds, fleet priority redesign
  beyond "yield to all commercials", save-schema fields for in-flight commercials
  (replay from commands + clock must rebuild — same as today).

## Migration / test impact

- Expect to rewrite accessors in ~15 edit-mode tests that touch `ActiveAircraft` /
  `AssignedStand` directly.
- Persistence replay remains command-based; no new save version for slice 1.
- Third-stand PR (#4) raises `StandCount` and naturally raises the concurrent cap
  later; slice 1 hard-codes max 2 commercials even if three stands exist.

## Consequences

- Brief `docs/product/concurrent-flights-brief.md` moves to **designed**.
- Next code PR: `cursor/concurrent-flights-slice1-…` implementing the task packet.
- Insolvency / research / daily-report / capacity PRs remain independent; rebase
  slice 1 onto main after merges as needed.
