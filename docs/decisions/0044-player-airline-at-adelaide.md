# 0044 — Player airline at Adelaide (design direction)

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
| Range gating | Destinations beyond the owned aircraft's range (~1,300 km for the ATR) are shown but locked. Perth, Sydney, Brisbane, Darwin are the obvious locked ones. |
| Flight time | Real length (e.g. Kingscote ~30 min, Port Lincoln ~50 min, Mount Gambier ~1 h, Coober Pedy ~2 h). |
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
