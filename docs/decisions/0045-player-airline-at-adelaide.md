# 0045 — Player airline at Adelaide (design direction)

Date: 2026-09-13. Agreed with Bailey in a Claude Q&A session. Design only —
nothing is built by this record. Build waits on Bailey's go.

## Goal

An airport run by multiple airlines. The player runs one of them, owns its
aircraft, and organises them to land, take off and fly to other places. The
airport itself runs itself. Keep it realistic to the location.

## Decisions

| Topic | Decision |
|---|---|
| Starter airport | Adelaide (YPAD). Airport is autonomous. |
| Player role | Boss of one airline; player chooses its name and colours at start. |
| Starting fleet | 1 ATR 42-class turboprop (AIR-001 v02). Grow later. |
| AI airlines | One to start: Emu Air (existing `dc_livery_emu_air_v01`), same ATR for now. Shares/competes for runway slots and stands only; not a route rival yet. |
| Control mix | Player picks destination and departure time. Tower sequences pushback, taxi, takeoff and landing — no player landing clearance. On arrival the player picks the stand and sends it to taxi. |
| Destinations | Map of destinations, Australia-wide to start; global later. |
| Range gating | Destinations beyond the owned aircraft's planning range are shown but locked. The ATR's brochure range is ~1,326 km; with passengers and reserves it is planned at **1,100 km**, which locks Sydney, Hobart, Alice Springs and everything further — as in real service. |
| Flight time | Real length: great-circle distance at 556 km/h cruise plus 10 min climb/descent (Kingscote ~23 min, Melbourne ~80 min airborne), plus 7 min taxi out, 5 min taxi in and a 40 min turnaround at the destination. |
| Time controls | 1×, 2×, 4×, 10×, 30×, 60×, plus skip-to-next-event. |
| Money | Deferred to a second step (fares, fuel, leasing/buying). First slice is ownership and scheduling only. |
| Branding | Fictional airline names on real routes. |

## First slice (v1)

1. Adelaide as an `AirportLocation` preset (today: Kingscote, Port Lincoln,
   Coober Pedy only).
2. Airline and livery ownership on each flight (`CommercialFlight` has no owner
   today; the presentation hardcodes Coastline Regional).
3. Destination catalogue: real Australian destinations with distance and block
   time; range check against aircraft type.
4. Off-map flight tracking so aircraft continue while away.
5. Schedule a departure; stand choice and taxi command on arrival.
6. Destinations map UI.
7. Extended time rates and skip-to-next-event.
8. Save/load returns — real-length flights make it necessary.

## Later

Economy; a jet type for longer routes (needs a new model); Emu Air as a real
rival; international destinations.

## Relationship to earlier records

This reverses the "no objectives, no progression, nothing saved" scope of
ADR 0041 as the *next* direction. ADR 0041's bare circuit stays the baseline
until the first slice lands.

## Risks

- Adelaide landside is hidden behind the bare-field flags, so the airport reads
  empty until those buildings return.
- Real-length flights with no saving would lose every session's progress.

## Implementation notes (first slice, 2026-09-13)

- `AirlineOperations` is separate from the circuit `AirportSimulation`, so the
  flight-model work there is untouched. It shares the same clock.
- Four regional bays (`BAY-1`–`BAY-4`): player on BAY-1, Emu Air on BAY-2/3.
  A stand is held on the stand and while taxiing in, released at pushback.
- Player aircraft wait indefinitely for a stand once landed; Skip refuses to jump
  past that decision and the rate drops to 1× when it arises.
- Emu Air picks a random reachable destination and departs 45 min after parking.
- Still open: 3D aircraft driven by the fleets, save/load, economy, and whether
  `DayCycle` becomes a real 24-hour day.

## 3D fleets (2026-09-13)

- The field draws the fleets, not the demo circuit, once an airline starts.
  `FleetVisual` expresses each fleet state as a circuit `AircraftPhase` so the
  existing curves, props, gear, lights, smoke and audio are reused unchanged;
  airborne phases keep the circuit's exact durations.
- Ground routes are presentation geometry on the real pavement: apron bays →
  apron lane → X −300 link → Taxiway F → E2 holding point; vacate by backtracking
  to E2. Aircraft are built once and hidden while away.
- Runway occupancy per movement: lineup 60 s + takeoff; approach + landing +
  vacate 90 s.

## Save/load (2026-09-14)

- One airline save, `airline-save.json` in the player's persistent data folder,
  version 1. Only the airline layer is saved: clock, tower state, RNG state,
  airlines, and each aircraft's state with its timestamps, stands, destination,
  schedule and trips. Recent-event history and the demo circuit are not saved.
- Restores are all-or-nothing: an unrecognised version, type, destination,
  airline, stand conflict or missing player airline refuses the save instead of
  resuming a half-right game. The start screen reports why and offers a new airline.
- Autosave triggers: every command, within 2 s of a fleet event, every 20 s of
  play, focus loss and quit.

## Away catch-up (2026-09-14)

- Saves record real UTC time (format v2; v1 migrates by resuming without catch-up).
- Continue advances by real time away, 60 s minimum and 7 days maximum, using the
  normal event-driven update, so offline results match live play (core rule).
- Player aircraft that land while away wait for a stand exactly as in live play;
  the away summary tells the player.

## Live real time (2026-09-14)

Bailey: "I want time to be accurate of real time… accurate live time", choosing
"always real time" over keeping speed-ups. Simulation time is seconds since a UTC
epoch, shown in Adelaide local time. Pause, rates and skip are removed; catch-up on
Continue is simply aligning to now (capped at a week). Supersedes the 10×–60× and
skip decisions above.
