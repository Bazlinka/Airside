# Task packet: Batch C — first-playable 3D set

Depends on: `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` (Batch C manifest),
approved Batch A references (`docs/art/reference/`).  
Status: **planned** — no candidate generated or modelled yet.  
Out of scope: Batch B world surfaces/markings (separate packet), Batch D
animation/VFX, anything in the later-production backlog (terminal interiors,
passenger agents, cargo/GA fleets, modular terminal construction).

This packet exists so the next contributor with image-generation and/or Unity
modelling access (ChatGPT for concept/orthographic references; Cursor, Codex
or Bailey for the actual FBX/glTF modelling and Unity import) can start Batch C
without re-deriving scope from the full art spec. It does not generate or
model anything itself — no image-generation or 3D-authoring tool was available
in the session that wrote it.

## Player-visible outcome

The primary aircraft, the two ground-traffic fleet aircraft, the terminal,
hangar, operations shed and core service vehicles render as real 3D models
instead of procedural primitives, at the approved visual direction, with the
existing primitive/material fallback retained until each replacement is
integrated and verified (Batch A's own gate: "missing art cannot change
simulation state, timing, saves or offline replay").

## Files / modules in scope

Exact IDs and paths from the Batch C table in
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`:

- `AIR-001` regional turboprop model — primary + traffic livery variants
- `AIR-002`/`AIR-003` Coastline Regional / Emu Air livery decals
- `AIR-004` neutral traffic livery (used by `GT-201`/`GT-202`)
- `BLD-001` small regional terminal, `BLD-002` hangar, `BLD-003` operations shed
- `VEH-001` fuel truck, `VEH-002` baggage tug + 3 carts, `VEH-003` apron bus
- `PRP-001` service-equipment kit (stairs, chocks, cones, towbar, bins, GPU)

Integration touches `game/Airside/Assets/Airside/Art/Models/**`,
`Textures/Decals/**`, and whichever `Presentation/*` MonoBehaviours currently
instantiate primitives for these entities (aircraft transforms in
`AirsidePrototype.cs`, service-vehicle transforms per `UpdateServiceVehicles`).

## Relevant decisions and invariants

- Decision 0018/0022 (`ART_DIRECTION_AND_ASSET_SPEC.md`) governs palette,
  ownership, folder contract, file/import rules and the integration
  acceptance criteria — read it in full before modelling.
- Fictional airlines only: Coastline Regional, Emu Air, Southern Cross Link,
  Gulf Connect, Redgum Air (`AirportRoutes.cs`). AIR-002/003 must use only the
  two liveries already in the manifest; do not invent new airline branding in
  this batch.
- Simulation/presentation separation: model geometry and materials carry no
  gameplay logic; animation rigging is Batch D's concern, but Batch C models
  must expose the named separated parts Batch D will need (gear, propellers,
  doors, control surfaces per AIR-001; cab/wheels/hose per VEH-001; tug +
  three carts with articulation points per VEH-002; doors/wheels per VEH-003).
- First-playable budget: two LODs plus a simple collider per world asset;
  shared materials/atlases over unique 4K textures.

## Acceptance criteria

1. Each delivered file's ID, exact path and status move from `Planned` through
   `Generated/Modelled` → `Review` → `Approved` in the Batch C table, with
   Bailey's approval recorded the same way Batch A's was.
2. Every file has a `docs/data/ASSET_AND_DATA_REGISTER.md` entry (source,
   licence/terms, cost, attribution, fallback) before it is referenced from
   Unity.
3. Unity references the approved file with the existing primitive/material
   retained as fallback until the new prefab loads successfully.
4. Verified at overview and follow camera, at day/dusk/night, per the spec's
   integration acceptance criteria — including that active/holding/delayed
   states stay distinguishable without reading the HUD.
5. No change to simulation state, timing, saves, offline replay or any
   EditMode test outcome — this is a presentation-only batch.
6. `GAME.md` and `CHANGELOG.md` record the integration and verification in the
   same commit, per the spec's own rule.

## Suggested order

Terminal, hangar and operations shed first (they anchor the static scene and
are needed regardless of aircraft progress), then the primary aircraft model
(highest player-visible impact), then service vehicles, then the neutral
traffic livery so `GT-201`/`GT-202` stop sharing the primary's placeholder
geometry.

## What must remain unchanged

- `AirportSimulation`, `AirportTaxiNetwork`, `ReservationTable` and all
  `Simulation/*` classes — no code in this batch should need to touch them.
- The 57 existing EditMode tests must keep passing untouched; this batch adds
  no new domain/simulation code.
