# 0071 — Operations board honesty, schedule feel, no live Adelaide traffic feed

Date: 21 September 2026. Requested by Bailey after play: (1) flights show as due to
depart but nothing is at the terminal; (2) Operations is hard to read for "where am
I in the day?"; (3) taxi/pushback still feel unreal; (4) arrival/departure times
feel wrong — maybe pull live Adelaide traffic.

## Decision and reason

### Board honesty (the empty-gate bug)

The Operations board overlays a synthetic full-day timetable (`AdelaideDayPlan`) on
top of the much smaller live fleet. Uncovered day-plan rows used to print a stand
label (Gate 13 / Bay 50C) and "Scheduled", which reads as "aircraft is at that gate
and due out" when the pavement is empty. Sky traffic and the day plan exist so the
airport feels busy without parking dozens of extra aircraft (ADR 0055).

**Fix:** uncovered day-plan rows keep the published time and route, but STAND is
always "—", STATUS is "Listed" (departures) / "Expected" (arrivals), and
`OnField = false`. Only a live fleet row may claim a stand. The day caption now
says "N on field · M listed ahead" instead of a single "to go" count that mixed
phantoms with metal.

### Where you are in the day

The day strip already had a progress caret and bank density. It now also paints
on-field movement ticks, a persistent **NOW** label on the strip, and a **NOW**
divider on the board between muted past rows and the upcoming list.

### Taxi weave

Presentation weave on taxi (`HumanGroundPose`) was up to ~0.55 m lateral and ~2.2°
heading bias — enough to look like drifting off the centreline. Reduced to ~0.18 m
and ~0.9°. Authored pushback polylines and the OSM taxi router are unchanged this
round; further pushback realism still needs a Unity look at the Gate 13 / bay
curves.

### Schedule feel — authored, not live

Live AI domestic turnarounds were 10–24 minutes (game-speed). They are now
28–44 (turboprop) / 40–58 (narrowbody) / 55–89 (widebody), and the day-plan
arrive→depart dwell matches those bands (35 / 50 / 75). Still deterministic and
seeded.

### No live Adelaide traffic feed

**Do not** ingest FlightAware, ADS-B, AIP published schedules, or airline APIs as a
live sim input. Reasons:

1. **Determinism.** Frame rate must not change outcomes; time is an injected clock;
   random choices are seeded. A live feed breaks replay, soak tests and save/reload
   identity.
2. **Offline / licence.** Every external data source needs a register row, cost and
   fallback. Live feeds fail offline and usually forbid redistribution.
3. **Scope.** The living airport is an authored representative Adelaide day, not a
   mirror of today's real movements.

If Bailey later wants "more like a real YPAD day", the approved path is a **static
authored snapshot** (one representative day of public timetable-shaped slots),
recorded in `docs/data/`, loaded like OSM — not a live socket.

## Affected systems

Presentation (`OperationsWorkspace` model/layout/painter), Presentation taxi weave
(`AirsidePrototype.FleetVisuals.HumanGroundPose`), Simulation (`AirlineOperations.
AiTurnaroundSeconds`, `AdelaideDayPlan` dwell).

## Migration impact

None. No save schema change. Existing saves keep their booked times; only newly
booked AI departures use the longer turnaround bands.

## Guardrails

- Day-plan rows must never invent a stand occupancy the live fleet does not hold.
- Do not add a live traffic client without a new ADR that explicitly overrides this
  one and names the licence, offline fallback and determinism story.
