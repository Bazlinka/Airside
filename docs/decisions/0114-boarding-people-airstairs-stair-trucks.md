# 0114 — Boarding people, turboprop airstairs and stair trucks

Date: 23 September 2026. Bailey asked for three things:
- people, mainly for boarding animations, so that boarding looks realistic;
- stairs for the Saab and Dash 8, as in real life;
- aircraft doors that open and close at the right times.

## Decision

### People (CC0, Quaternius)

- **Source.** Nine characters from Quaternius *Ultimate Modular Men* and *Ultimate
  Modular Women*, both CC0 1.0:
  - seven passengers: casual, hoodie, suit, holiday, formal and so on;
  - two ramp workers in hi-vis, imported but not yet placed.

  All nine share one 62-bone rig, and the `Walk` clip is in place (no root motion).
  - Why these packs: CC0, rigged and animated, low-poly (5–6.6k triangles), and their
    look matches the game's flat-shaded art.
  - Rejected alternatives: Kenney *Animated Characters* (too blocky at apron scale) and
    Mixamo (its licence does not allow redistributing the raw files in a public repo).
- **Import.** `scripts/import-quaternius-people.py` (Blender `bpy`) re-exports them to
  `Assets/Resources/Airside/Characters/*.fbx`:
  - It keeps only Walk, Idle, Idle_Neutral, Interact and Wave.
  - It forces every material opaque. The men's pack ships with alpha 0, which made those
    characters invisible.
  - Licences and per-file SHA-256 hashes are in `docs/data/quaternius/`. The register
    row is CHR-003.
- **Runtime.** The Animator is disabled. `AnimationClip.SampleAnimation` samples the walk
  on the **simulation clock**, with the stride scaled to walking pace, so pause and
  time-scale behave. Each figure is redrawn in the project's opaque surface material,
  and its height varies from 1.58 to 1.88 m. At most 60 figures are shown, drawn from a
  pool.

### Who walks, and when (Simulation/BoardingFlow.cs, UnityEngine-free)

`BoardingFlow` is a pure function of fleet state and time, like
`EngineStartSequence` and `AerobridgeTimeline`. **Presentation only:** nothing in the
simulation waits for a passenger.

- **Mode by stand:**

  | Stand | Mode | What passengers use |
  |---|---|---|
  | Bridged T1 gate | `Aerobridge` | the tunnel; no one walks outside |
  | Jet on a stand without a bridge (20R, 22R, 27–29) | `StairTruck` | a stair truck |
  | Turboprop (Saab 340, Dash 8 / Q400, ATR) | `IntegralAirstair` | its own forward airstair door |

- **Load.** Passenger count is the type's seats × a stable 62–94 % load factor, hashed
  from the registration and trip. `AircraftCatalogue.TypicalSeats` parses the seats from
  the catalogue text.
- **Deplaning** begins 5 s after the door opens, one passenger every 2.6 s on a
  turboprop and every 2.0 s on a jet.
- **Boarding (AI)** is timed so the last passenger, with a 110 s walk allowance, is
  aboard before the door closes.
- **Boarding (player)** fills the player's own *Boarding* prep stage. Boarding never
  starts until 20 s after deplaning ends.
- Each passenger has a staggered start and their own pace of 1.15–1.44 m/s. They climb
  the stairs at 0.55 m/s.

### Doors

- `EngineStartSequence.For` takes its door state from the first rule that applies:
  1. the bridge (ADR 0113);
  2. the stair truck (`BoardingFlow.StairTruckDoorsOpen`): open after the truck is on,
     shut 240 s before the push, with the truck gone at 230 s (before the beacon at
     180 s);
  3. the existing airstair timing: open 90 s after parking, shut 25 s before the push.
- **Turboprops.** The forward door on the Saab 340, Q400 and ATR is converted into an
  **airstair door** (`AirstairDoor` component). It is re-pivoted at its bottom outer
  edge, and treads, handrails and posts are added on its inner face. It folds down
  until the bottom step reaches the apron:
  `OpenDegrees = −side·(90° + asin(sill / height))`. It animates at 55°/s.
- **Widebodies.** The widebody `door_left_1` is now renamed `CabinDoor`, so the A330,
  A350 and 787 L1 doors open and close too.

### Stair trucks

A stair truck is primitive geometry: chassis, cab, and a flight of stairs built to the
jet's L1 sill height (from the generated `AircraftDoors.L1` table). Each truck:
- drives up from 22 m out, 40 s after the aircraft parks;
- takes 40 s to position;
- leaves before the push.

Trucks are pooled.

### Paths

A passenger walks from the nearest point on the Terminal 1 footprint (1.2 m out) to an
approach point ahead of and outboard of the door, clear of the propellers. From there
they go to the foot of the stairs and up to the sill. Deplaning passengers walk the
same path in reverse.

## Not done (follow-ups)

- Ramp crew figures are imported but not placed.
- There are no buses for remote stands, and no stair-climb clip; the walk clip is used
  on the stairs.
- Walk paths do not avoid other aircraft or vehicles.
- Pushback tugs are still planned in `docs/architecture/PUSHBACK_TUGS_PLAN.md`
  (becomes ADR 0115).

## Evidence

- `scripts/test-domain.sh` passes 750/750, including `BoardingFlowTests`. Those tests
  cover:
  - mode per stand;
  - seat counts (Saab 34, Q400 82, 737-800 174);
  - deplaning only after the door opens;
  - everyone aboard before the door closes (Saab, Q400, and a 737 at GATE-27);
  - no walkers on bridged gates;
  - stair truck order: truck on before the door opens, door shut before it leaves,
    truck gone before the beacon.
- A Blender Cycles lineup render of all nine re-exported FBX files shows correct
  materials and scale.
- **Still needed:** a Unity compile of the new Presentation partial, then a
  `scripts/test-unity.sh` run. Also check the FBX import in play:
  - the Generic rig and clips from `Resources`;
  - the Saab and Q400 airstair folding to the ground;
  - a stair truck at gate 27.
