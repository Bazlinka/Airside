# 0026 — Prefab / Addressables art loader scaffold

**Date:** 2026-09-07  
**Status:** Accepted (implementation scaffold; first Resources prefab landed)  
**Decision owner:** Cursor (under Bailey 0025 backlog)

## Decision

1. Runtime art resolution goes through `ArtPresentationLoader`:
   1. Unity prefab under `Resources/Airside/Prefabs/<kit-basename>`
   2. Addressables key `airside-prefab/<kit-basename>` when a catalog locates it
   3. StreamingAssets glTF (`ArtGltfLoader`)
   4. Caller procedural cuboid fallback
2. The Addressables package is in the Unity project manifest; Resources remains the
   shippable bridge until Bailey builds Addressables groups on Mac.
3. Prefab keys match glTF basenames (`mdl_hangar_small_v02`, etc.) to keep Prefer
   v02/v03 / fallback naming stable. Standalone prop prefabs may use their own
   basename (e.g. `mdl_passenger_stairs_v01`).
4. Prefabs may ship without authored `.mat` files when they include
   `AirsideRuntimeMaterialBinder`, which applies Lit profiles at Awake.

## Reason

Decision 0025 requires moving off the interim filesystem glTF parser toward
Unity-imported meshes with authored URP materials. A loader facade and drop
folder let Bailey import real prefabs on the Mac without rewriting every caller.
The first Resources prefab (`mdl_passenger_stairs_v01`) proves the path end-to-end
before authored FBX drops land.

## Affected systems

- Presentation loaders (`ArtPresentationLoader`, stairs / building / vehicle builds)
- `Packages/manifest.json` (Addressables dependency)
- `Assets/Resources/Airside/Prefabs/` drop target
- `AirsideRuntimeMaterialBinder`

## Migration impact

No save or simulation change. Existing StreamingAssets kits remain the default
for aircraft/buildings until Bailey drops matching prefabs. Addressables loads
no-op safely when no catalog/key exists.
