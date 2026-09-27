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

## Decision — buildings get facades, blocks get edges

**Building detail is planned in pure code** (`Presentation/BuildingDetail.cs`, UnityEngine-free,
seeded from each OSM id so it never changes between runs) and merged at build time into **one
mesh per material** shared by every building, so the whole airport's new detail costs a
handful of draw calls, not hundreds of objects:

- a parapet on every flat roof (hangars lower, the terminal higher);
- storey window bands on offices, freight and the fire station — panes between mullions with a
  head and sill; about 70 % of panes are lit at night and the rest stay dark, so facades do not
  read as one glowing strip. Lit panes are one renderer (`YPAD building windows lit`) driven by
  the night-glow pass: dark tinted glass by day, warm offices at night, no point lights;
- hangar doors (leaves with seams, a header and floor track) on the wall facing the field, and a
  clerestory roof monitor along the long axis when it fits inside the footprint;
- fire-station appliance bays with roller-door slats; freight loading doors with canopies;
- a control tower built as a tapered shaft, cab floor, inset glass ring with a mullion at every
  corner, overhanging roof, plant cap, and a mast with a cross-arm and a red obstruction light;
  the cab glass joins the night-glow pass;
- rooftop plant scaled to the roof (kept off the terminal's authored skylights and screens);
- the terminal's landside kerb: a canopy on columns, a fascia band and upper-level windows.
  The airside curtain wall keeps its 28-bay glazing unchanged.

Walls now carry UVs that run **along the wall and up it** in metres; the previous world-XZ UVs
smeared every wall texture into vertical streaks.

**Every procedural block is chamfered.** `CreateBlock` (and so `ParentBlock` — vehicles, stands,
signs, props) swaps Unity's sharp cube for a shared `BevelledBox` mesh: a unit cube with
flat-shaded chamfers on every edge and corner. The chamfer is set per axis in local units so it
is the same world size on every edge once scaled (15 % of the smallest side, at most 12 cm).
Meshes are cached by quantised chamfer. Paint-thin blocks (under 5 cm) and art-textured blocks
keep the primitive (invisible bevel / exact UV layout).

## Consequences

- The player can see why their aircraft is not moving and who is in the way, which is the
  information needed to decide whether to re-plan.
- `Why` duplicates the ordering of the decision code. If a new hold is added to
  `AdvanceAircraft` / `RunTowerOnStrip`, `Why` must learn it too; `HoldReasonTests` pins each
  existing kind against a live scenario, and `AskingWhy_ChangesNothing` pins that the query is
  read-only.
- New building detail is opaque merged geometry: roughly seven extra draws for the whole
  airport, one extra night-glow renderer, and no new real lights. `BuildingDetailTests` pins
  that detail stays on its footprint, lit/dark panes, doors, tower cab order and determinism.
- Bevelled blocks carry 96 vertices instead of 24; with ~460 blocks that is about 33k extra
  vertices, all static-batched as before.
