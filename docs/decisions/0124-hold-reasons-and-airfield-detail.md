# 0124 — Hold reasons, building detail, airfield lighting and softer geometry

Date: 27 September 2026. Author: Claude, at Bailey's request: "Descriptions for why holding
short? Not just holding? Adding detail to terminal and all buildings. Improving objects in game
to be less blocky. Lights on aircraft runway etc."

## Decision — every hold explains itself

**The reason is derived, never stored.** `AirlineOperations.Why(FleetAircraft)` returns a
`HoldReason` (kind, the aircraft responsible, runway, when it clears, queue position, a detail
string and any other aircraft involved). It re-asks the same questions, in the same order, as
the pushback, stand and tower decisions in `AdvanceAircraft` / `RunTowerOnStrip`, reusing their
helpers (`NextTaxiReleaseAt`, `IsLeadInFree`, `GroundResourceHolder`, `LongestWaiting`,
`VacateCrossesHolder`, `IsOccupyingRunway`, `SuggestStand`). It never writes state, so the
deterministic timeline is untouched and the save stays at v14. Results are cached per aircraft
for the current processed second, state and funds, so the HUD can ask every frame.

`GroundTraffic.PathClear` gained an overload that reports the aircraft that blocked the path;
the old boolean form calls it.

Kinds: cancelled, curfew, turnaround (the prep stage), apron busy (the aircraft already taxiing
out), gate lead-in blocked, taxi route blocked, runway occupied (landing / departing aircraft),
wake separation, storm ground stop, queue position ("number 2 for 23 — behind QFA412"),
arrival first, departure first, no stand free (every bay / every gate / your base is full),
choose a stand (the player's decision, with the tower's auto-park time), held airborne, and in
maintenance.

**Wording lives in Presentation** (`HoldReasonText`): a sentence for the selected-aircraft card,
the Ops board and the attention band ("Holding short 23 — QFA412 (737-8) landing, clear in
about 1 min"; "Waiting to push back — apron busy with RXA201 and VH-SUN") and a short tag for
field labels and phase chips ("hold · 737-8 landing", "push wait · apron busy",
"no stand free"). Aircraft are named by the flight number the boards show, with the type.

## Consequences

- The player can see why their aircraft is not moving and who is in the way, which is the
  information needed to decide whether to re-plan.
- `Why` duplicates the ordering of the decision code. If a new hold is added to
  `AdvanceAircraft` / `RunTowerOnStrip`, `Why` must learn it too; `HoldReasonTests` pins each
  existing kind against a live scenario, and `AskingWhy_ChangesNothing` pins that the query is
  read-only.
