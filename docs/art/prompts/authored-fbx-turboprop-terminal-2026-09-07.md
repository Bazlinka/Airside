# Authored FBX turboprop + terminal (AIR-001 / BLD-001)

**Date:** 2026-09-07  
**Tool:** `scripts/generate-authored-fbx-turboprop-terminal.py` (numpy mesh builders → glTF via Batch C packer; FBX via `assimp export`)  
**Decision:** 0025 item 2 (replace placeholder 3D); Batch C task packet FBX paths

## Deliverables

| ID | Paths |
|---|---|
| AIR-001 authored | `Models/Aircraft/mdl_regional_turboprop_01_authored_v01.fbx` + companion `.gltf`/`.bin` |
| BLD-001 authored | `Models/Buildings/mdl_terminal_regional_small_authored_v01.fbx` + companion `.gltf`/`.bin` |

Distinct ids — does not overwrite lofted/v04 greybox kits. PreferArtKit order:
authored → lofted (aircraft only) → v04 → … → v01 → primitives.

## Topology (vs lofted boxes)

- Turboprop fuselage: 20-segment lathe with 14 stations (74 meshes total), cylindrical
  engines/nacelles/spinners/tires/prop hubs; cabin window row + livery stripe;
  wing fences; separated gear doors, cabin/cargo doors, props.
- Terminal: body/roof/caps/glass with denser mullions + transom; canopy with
  cylindrical posts/columns; service wing + baggage door.

## Regeneration note

`write_kit` preserves existing Unity `.meta` GUIDs so Addressables/Resources
references stay stable across densify passes.

## Runtime path

1. **Now:** StreamingAssets companion glTF via `ArtGltfLoader` (Linux-safe).
2. **Mac:** Unity imports `.fbx` → menu **Airside → Art → Bake Authored FBX Prefabs**
   writes `Resources/Airside/Prefabs/mdl_*_authored_v01.prefab` so
   `airside-prefab/<key>` serves Unity-imported meshes.

## Licence

Project-owned procedural. No third-party pack. Register row added same commit.
