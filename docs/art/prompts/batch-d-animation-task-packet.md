# Batch D — animation, feedback and weather presentation

**Status:** Ready for production (task packet; clips/prefabs not yet delivered)  
**Date:** 2026-09-06  
**Decision / contract:** `docs/decisions/0022-art-direction-and-asset-pipeline.md`,
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`  
**Depends on:** Batch C models Approved (or temporary primitive stand-ins already in
`AirsidePrototype`). Simulation remains the authority for timing and ownership.

## Player-visible outcome

Aircraft and vehicles look alive: props spin, gear/doors move at phase boundaries,
service vehicles play short service loops mapped to turnaround tasks, and weather
gets restrained rain/wetness presentation — without changing simulation outcomes.

This packet authorises **clip/prefab production and review**. It does **not**
authorise animation driving reservations, cash, delays or save state.

## Scope

| In scope | Out of scope |
|---|---|
| ANM-AIR-001…004 propeller, gear, door, lights | New Domain/Simulation rules |
| ANM-VEH-001…004 wheels + service loops | Batch C remodel |
| VFX-001…004 touchdown, heat, rain, wetness | Full particle spectacle |
| Evidence + register rows | Removing primitive fallbacks |
| | Companion / multiplayer |

Paths relative to `game/Airside/Assets/Airside/Art/`.

## Deliverables

| ID | Exact path | Trigger / behaviour |
|---|---|---|
| ANM-AIR-001 | `Animation/Aircraft/anm_propeller_spin_v01.anim` | Loop while engines active; greybox already spins primitives |
| ANM-AIR-002 | `Animation/Aircraft/anm_gear_cycle_v01.anim` | Deploy/retract only at presentation phase boundaries |
| ANM-AIR-003 | `Animation/Aircraft/anm_cabin_door_cycle_v01.anim` | Open at stand after arrival; close before pushback |
| ANM-AIR-004 | `Animation/Aircraft/anm_aircraft_lights_v01.controller` | Nav / beacon / landing lights by phase + day/night |
| ANM-VEH-001 | `Animation/Vehicles/anm_vehicle_wheels_v01.anim` | Wheel rotation from presentation movement |
| ANM-VEH-002 | `Animation/Vehicles/anm_fuel_service_v01.anim` | Park → hose → service → retract; duration from task |
| ANM-VEH-003 | `Animation/Vehicles/anm_baggage_service_v01.anim` | Arrival → cart activity → leave; mapped to bag tasks |
| ANM-VEH-004 | `Animation/Vehicles/anm_bus_service_v01.anim` | Door + settle; mapped to passenger tasks |
| VFX-001 | `VFX/vfx_touchdown_smoke_v01.prefab` | Brief restrained smoke on touchdown |
| VFX-002 | `VFX/vfx_engine_heat_v01.prefab` | Subtle close-view heat only |
| VFX-003 | `VFX/vfx_rain_airfield_v01.prefab` | Camera/world rain; weather state controls it |
| VFX-004 | `VFX/vfx_wet_surface_response_v01.prefab` | Material wetness, not a full-screen filter |

## Rules

- Animation **decorates** sim state; it never decides phase length or resources.
- Prefer short loops and event clips over long timeline cutscenes.
- Match Batch A dusk lighting language (REF-002) for night/light work.
- snake_case `_v01`; bump version for a visibly different candidate.

## Acceptance criteria

- [ ] Every ID exists at the exact path (or path updated in the same commit).
- [ ] Prop/gear/door/lights respond only to presentation phase / day-night inputs.
- [ ] Service clips finish when the mapped turnaround task completes (or abort cleanly).
- [ ] VFX respect performance tiers; rain can be disabled.
- [ ] Register + evidence committed with the files.
- [ ] Seeded simulation tests remain unchanged (presentation-only).

## Must remain unchanged

- Deterministic clock, reservations, economy, saves.
- Batch A refs and Batch B/C Approved assets.
- Primitive fallbacks until Integration is Verified.

## Handoff after this packet

1. Produce clips (prop + lights first is fine — greybox already prototypes prop spin).
2. Review → Approved.
3. Narrow Integration PR wiring Animator/VFX to existing presentation hooks.
4. Then Batch E UI art or phase-five polish.
