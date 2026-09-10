# AIR-001 final ATR 42 starter — generation record

**Date:** 2026-09-10
**Generator:** project-owned deterministic Python/NumPy geometry pipeline
**Script:** `scripts/generate-air-001-atr42.py`

## Output

- `Art/Models/Aircraft/mdl_atr42_starter_v01.fbx`
- `Art/Models/Aircraft/mdl_atr42_starter_v01.gltf` + `.bin`
- `Resources/Airside/Prefabs/mdl_atr42_starter_v01.prefab`
- Matching runtime glTF/`.bin` under `StreamingAssets/Airside/Art/`

## Specification

Fictional and unbranded ATR 42-600-class starter aircraft. Use the official
three-view dimensions of 22.67 m length, 24.57 m wingspan, 7.59 m height and
3.93 m propeller diameter. Preserve the recognisable high wing, twin underwing
nacelles, six-blade propellers, tapered cabin/nose, high tailplane, fuselage-side
main-gear fairings, twin nose wheels and tandem main wheels.

Separate only gameplay-readable movement: propellers; nose/left/right gear and
doors; all six tires/wheels/rims; left/right flaps, ailerons, elevators and
spoilers; rudder; forward cabin door; cargo door. Add cockpit glazing, 13 cabin
windows per side, navigation/beacon/landing/taxi lights, antennas, pitots,
exhausts and restrained panel accents. Use an Airside-owned blue/white fictional
livery with no airline logo, ATR wordmark, registration, watermark or occupants.

## Verification

- Deterministic bounds: 24.57 × 7.59 × 22.67 m
- 158 named meshes; 19,824 vertices; 8,712 triangles
- Unity ModelImporter Resources bake
- Targeted aircraft EditMode tests
- Packaged macOS follow-camera review

Project-owned procedural asset. No third-party art is embedded; official ATR
material was used only as factual dimensional and visual reference.
