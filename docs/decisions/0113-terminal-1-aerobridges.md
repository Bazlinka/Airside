# 0113 — Terminal 1 aerobridges that drive to the aircraft

Date: 23 September 2026. Bailey: build gates that move for the plane, for each live gate.

## Decision

Every Terminal 1 gate whose terminal face is within reach gets a moving aerobridge.
That is 17 gates: 12L, 13, 14L, 15, 16L, 16R, 17, 18(L), 18R, 19, 20(L), 21, 22L, 23,
24, 25 and 26L. Each bridge has:

- a fixed rotunda by the building;
- a three-section telescoping tunnel;
- a drive column and bogie on the apron;
- a cab with a bellows canopy.

When a jet is on the gate, the cab drives out to its forward-left (L1) door. It parks
folded back along the terminal face when the gate is empty.

Gates without a bridge:
- **20R and 22R:** the face is about 78 m ahead of the door, so they stay bus/stairs
  stands.
- **27, 28L, 28R and 29:** these lie west of the OSM terminal footprint. With no
  building to hang a bridge from, they board by stairs until that pier is modelled.

### Where (Simulation/AdelaideAerobridges.cs, UnityEngine-free)

- **Derived, not hand-placed.** Sites come from the same OSM gate nose stops and
  terminal footprint the pavement uses, so they cannot drift from the gates.
- **Rotunda:** 3.5 m in front of the terminal face, straight ahead of a 737's L1 door
  and 9 m further to the aircraft's left. The tunnel therefore runs diagonally beside
  the nose, never over it.
- **Reach:** a gate gets a bridge only if every gate jet's L1 door is 12–44 m away.
  That covers the E190, A220, A320, 737-800, 737-8, A321neo, A330, A350, 787-9 and
  787-10.
- **Parking:** retracted to 13 m and folded along the face, 20° out onto the apron.
  The cab sits ahead of the nose stop and more than 12 m to the side, so an arriving
  nose or wing never reaches it, and it is clear of every neighbouring stand's fuselage.
- **Door data:** `AircraftDoors.L1` gives each jet's L1 door (centre, sill and top) in
  the art-root frame. It is generated from each runtime glTF by
  `scripts/generate-aircraft-title-layout.py`, and `--check` fails if it goes stale.

### When (AerobridgeTimeline, a pure function like EngineStartSequence)

- **Arrival:** the bridge may only move once the beacon is off, at 75 s after parking.
  It drives out over 60 s. The L1 door opens 10 s after it is docked.
- **Departure:** the L1 door closes 10 s before the bridge moves. The bridge pulls back
  over 60 s, starting 250 s before the push, so it is clear before the beacon comes on
  at 180 s.
- **Player aircraft:** the bridge stays until fuel, catering, baggage and boarding are
  complete.
- **Door timing:** `EngineStartSequence` now takes its door state from the bridge on a
  bridged gate. Stairs stands keep the old timing.
- **Simulation effect:** none. The simulation never waits for a bridge, and a push is
  never delayed by one.

### How it is drawn (Presentation/AirsidePrototype.Aerobridges.cs)

- **Parts:** primitive parts (per the art rules, as a procedural fallback), under their
  own "Aerobridges" root, not the static-batched airfield root, because they move.
- **Docked pose:** read from the live aircraft view, so the cab meets the door wherever
  the aircraft is drawn.
- **Motion:** the cab sweeps about the rotunda and telescopes, rather than sliding in a
  straight line.
- **Easing:** the drawn position eases toward the timeline at no more than a full swing
  in 40 simulated seconds, so a jump (a loaded save, a push, a cancellation) never
  teleports a bridge.

## Evidence

- `AerobridgeTests` (7 tests), run by `scripts/test-domain.sh` (744/744 passing), cover:
  - which gates get bridges;
  - reach for every jet type;
  - rotunda and parked-cab clearances from their own and neighbouring stands;
  - docking only after the beacon is off, with the door opening only when docked;
  - the door closing before retracting, and the bridge clear before the beacon comes on.
- `scripts/generate-aircraft-title-layout.py --check` passes for the title layouts and
  the door table.
- A plan-view render from the computed data shows docked tunnels beside each nose and
  parked cabs along the face. It was drawn with 737 and A350 footprints at every gate.
- Not yet checked: the Unity editor compile and a Mac play check.

## Affected systems

- **Simulation:** `AdelaideAerobridges` (new) and `EngineStartSequence` (door state on
  bridged gates).
- **Presentation:** `AirsidePrototype.Aerobridges` (new), plus the build and update
  hooks in `AirsidePrototype` and `YpadPavement`.
- **Scripts:** the door table in `generate-aircraft-title-layout.py`.

## Migration

None. Saves and simulation outcomes are unchanged.

## Follow-ups

- Model the western T1 pier so gates 27–29 get bridges.
- A widebody second bridge (L2) for the code E gates.
- Authored bridge art to replace the primitives, per
  `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`.
- Pushback tugs: see `docs/architecture/PUSHBACK_TUGS_PLAN.md`.
