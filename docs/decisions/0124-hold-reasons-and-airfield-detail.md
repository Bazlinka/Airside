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

## Decision — lights in the right place, the right colour, at the right time

**Aircraft.** The glTF kits export every lamp node with a zero transform and the lens baked into
the mesh, so each aircraft `Light` sat at the airframe origin: nav lights and strobes lit the
belly. Lights now hang off a `Lamp pivot` child at the lamp mesh's bounds centre (procedural
lamps are centred, so nothing moves for them). Nav and beacon lenses stay visible and glow
through emission — nav lenses in their colour when on, a white flash on each strobe pulse,
the beacon lens pulsing with its light. The widebody belly beacon (`beacon_bottom`, on the
787, A330neo and A350) is renamed so it is recognised and flashes.

**Airfield lenses are merged fixtures.** Every runway, taxiway, stand, threshold, PAPI, approach
and guard lens used to be its own cube. `PlaceYpadLens` now appends a low-poly domed fixture
(`AirfieldFixture`: octagonal metal base, smooth two-ring lens) into **one mesh per colour
group**, one shared mesh for all metal bases, and one additive halo mesh per group (a ground
pool plus two crossed cards per lens, with a generated radial falloff). About a dozen lens and
halo renderers replace several hundred objects; halos are switched off by day.

Each group keeps **its own colour** — the amber caution-zone edges were being repainted white
by the night pass and now stay amber — and answers daylight by its role: edge, taxi and stand
lenses go dark-glass by day, approach lights, thresholds and PAPI stay readable at noon,
guard lights stay bright. New fixtures: **blue taxiway edge lights** beside the centreline
lights (off aprons and outside runway strips) and **red stop bars** across the taxiway at every
main-runway holding position. T1 streetlights get an arm and a lens that warms at night with a
glow pool under it. No real lights were added.

## Decision — rounder moving parts, a readable player base

**Aerobridge tunnels** use a rounded-rectangle tube (`BevelledBox.RoundedTube`, 0.6 m corners)
instead of a box. The rounding lives only in the cross-section because each section is
restretched along its length every frame; a chamfered cube there would have grown metre-long
end bevels. The drive column is a cylinder.

**Stair trucks** gain wheels, a bumper, handrails on posts and a platform guard. The registered
`mdl_passenger_stairs_v02` kit is **not** used: it is one fixed height, and the truck's flight is
built to each aircraft's door sill, which the kit cannot match.

**The player base** was solid livery boxes. Each module is now grey cladding with a livery fascia
band, windows, an entry, and on the maintenance/handling modules a hangar door with a header.
Only parts named `… livery` / `… livery band` take the livery tint. The authored hangar and shed
kits stay on the legacy 1:20 field where they already are; the base modules keep their
stage-specific sizes, which the fixed kits do not offer.

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
- Lens groups: ~12 opaque lens draws + one fixture-base draw + ~12 additive halo draws at night,
  against several hundred lens objects before. Stop bars are always lit at night, not switched
  by clearance. `AirfieldFixtureTests` pins the geometry and each group's day response.
- Bevelled blocks carry 96 vertices instead of 24; with ~460 blocks that is about 33k extra
  vertices, all static-batched as before.
