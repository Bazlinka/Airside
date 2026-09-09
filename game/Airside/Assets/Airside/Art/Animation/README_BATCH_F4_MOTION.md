# Batch F4 reusable motion map

Presentation-only rates live in `AirsideReusableMotion`. Simulation timing
remains authoritative; these numbers drive visuals only.

| Packet ID | Clip / controller key | Runtime hook | Authority |
|---|---|---|---|
| ANM-AIR-001 | `anm_propeller_spin_v01` | `SpinPropellers` → `PropRpmForPhase` | Phase enum |
| ANM-AIR-002 | `anm_gear_deploy_v01` | `GearBias` | Phase enum |
| ANM-AIR-003 | `anm_cabin_door_v01` | `CabinDoorBias` | Phase enum |
| ANM-AIR-004 | `anm_beacon_nav_v01` | BeaconHz / NavSteady / StrobeHz | Phase + unscaled time |
| ANM-VEH-001 | `anm_vehicle_wheel_v01` | VehicleWheelRpmTaxi | Service task progress |
| ANM-VEH-002 | `anm_vehicle_door_v01` | existing door hooks | Task progress |
| ANM-VEH-003 | `anm_hose_extend_v01` | existing hose hooks | Task progress |
| ANM-VEH-004 | `anm_cart_roll_v01` | VehicleWheelRpmService | Task progress |

Unity `.anim` / AnimatorController authoring can replace the numeric table later
without changing reservation, save, or economy outcomes. Until then the C# table
is the reusable authored path.
