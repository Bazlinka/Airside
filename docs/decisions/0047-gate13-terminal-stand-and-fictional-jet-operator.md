# 0047 — Gate 13 terminal stand and the fictional jet operator

Date: 2026-09-15. Follows ADR 0046, whose exit criteria this slice meets. Bailey's task
packet approved making the parked AIR-005 737-8 at Gate 13 one genuine AI aircraft, flown
by a fictional unbranded operator, with no economy or player purchase.

## Decision

1. **Terminal gates are a stand system of their own.** `AdelaideLayout.TerminalGates`
   (generated) carries each gate's nose stop, heading and nose-datum routes, separate from
   the regional `Bays`. `AdelaideGround` dispatches `TaxiIn`, `TaxiOut`, `StandPose` and
   `StandLabel` by stand kind; `AdelaideGround.Bay()` throws for a terminal gate instead of
   its historical fall back to 50D. `AirlineOperations.StandFits` allows jets
   (`NeedsTerminalGate`: the Boeing 737-8 type) only on gates and turboprops only on bays,
   enforced in `AddAircraft`, `AssignStand`, AI stand choice and save restore.
   `FreeStands()` now means free regional bays (every existing caller serves turboprops);
   `FreeStandsFor(type)` is the type-aware query.
2. **Gate 13 geometry is OSM-anchored.** The real parking line starts at the T1/T2/B1 node
   but OSM's terminal apron starts 50 m north (z = 358). A derived "Gate 13 apron link"
   polygon fills exactly that band — T2/T1 centreline below, the OSM apron edge above, inside
   that edge's x-extent — so it renders as apron and never overlaps the OSM apron. Routes, for
   the jet's nose datum: taxi-in via E2 … T2 eastbound, one left turn onto a 60 m straight
   lead-in to the stop; pushback tail-first straight back, tail swinging east onto T1 so the
   nose ends on the T1 centreline facing west, arriving along T1's own direction; 25 s tug
   disconnect; forward taxi-out along T1/T2 onto the normal route to runway 05.
3. **Jets are steered by nose and main gear.** `GroundLegPart.TrackMetres` (19 m for gate
   legs) points the body from a main-gear point on the path to the nose datum, instead of
   along the nose tangent; a 39.5 m fuselage steered off its tangent swings over the grass
   in every turn. Regional legs keep `TrackMetres = 0` and are unchanged.
4. **Reservations are derived from state, not stored.** A gate is held while its aircraft is
   parked, taxiing in and — unlike a regional bay — through the whole taxi-out (the pushback
   happens on the gate). Its lead-in (`AdelaideGround.LeadInResource`) is held during taxi-in
   and taxi-out. An AI arrival takes a gate only when gate and lead-in are free; a gate
   pushback starts only when the lead-in is free. The runway keeps the existing tower
   sequencing. `AirlineOperations.GroundResourceHolder` exposes the holder for tests/tools.
   Because nothing new is persisted, saves, catch-up and live play agree by construction.
5. **Wattlebird Jet** (id `WTB`, livery `#2F7F86`, no logo, decal or wordmark) is the
   project's fictional operator, with one 737-8, fictional registration **VH-WTJ**, at Gate 13.
   It flies a fixed mainland rotation by completed trips — MEL, SYD, MEL, BNE, SYD, PER, MEL,
   CBR — inside the existing 06:00–21:00 AI day. It draws **no** random numbers, so the
   regional carriers' seeded sequence is untouched.
6. **The static preview is gone.** The fleet presentation builds the jet through the
   type-aware AIR-005 path (model, framing, pick volume, shadow, selection marker), so there
   is exactly one 737, selectable and followable with its registration, airline, type and
   live state, and shown on the field tags and Flights board.

## Migration

No save-schema change (still v3): the jet saves as an ordinary fleet record (`TypeId`
`B38M`, stand `GATE-13`, airline `WTB`). `AirlineSave.Restore` builds operations with
`AirlineOperations.AdelaideStands` (bays + gates). Older saves gain the operator after
away catch-up via `AddMissingTerminalOperators()`, idempotently: skipped when the jet's
registration already exists or its gate is missing or taken, and the airline is only
added together with its aircraft.

## Affected systems

`scripts/generate-ypad-layout.py` → `Simulation/AdelaideLayout.cs` (gate routes, apron link);
`Simulation/GroundMotion.cs`, `AdelaideGround.cs`, `AirlineOperations.cs`, `AirlineSave.cs`;
`Domain/Airline.cs`; Presentation fleet visuals, selection card, stand names and load path.
Regional bays 50A–50F, their routes, the regional carriers, live time, economy and the
first-session flow are unchanged.

## Known limits (next slices)

- Circuit and en-route profiles are still the ATR's: the 737 flies approach/takeoff at
  turboprop speeds and cruises no higher than FL250. Timings come from `LegTiming`.
- Shared taxiways between gate and regional traffic are not reserved against each other
  (as before for regional traffic); only the gate, its lead-in and the runway are.
- Gate 13 has no painted lead-in line or stand markings yet.
- Packaged visual QA of taxi-in, pushback and taxi-out is outstanding (no build requested).
