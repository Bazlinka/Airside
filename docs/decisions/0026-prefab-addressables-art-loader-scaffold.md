# 0026 — Prefab / Addressables art loader scaffold

**Date:** 2026-09-07  
**Status:** Accepted (implementation scaffold; Resources prefabs + runtime Addressables locator)  
**Decision owner:** Cursor (under Bailey 0025 backlog)

## Decision

1. Runtime art resolution goes through `ArtPresentationLoader`:
   1. Addressables key `airside-prefab/<kit-basename>` — a runtime
      `AirsidePrefabAddressables` locator exposes every
      `Resources/Airside/Prefabs` asset under that key via
      `AirsideResourcesProvider` (`Resources.Load`) until Bailey builds Editor
      Addressables groups on Mac
   2. Direct `Resources.Load("Airside/Prefabs/<kit-basename>")` fallback
   3. StreamingAssets glTF (`ArtGltfLoader`)
   4. Caller procedural cuboid fallback
2. The Addressables package is in the Unity project manifest; Resources remains the
   shippable content source until Bailey imports authored FBX and builds real groups.
3. Prefab keys match glTF basenames (`mdl_hangar_small_v02`, etc.) to keep Prefer
   v02/v03 / fallback naming stable. Standalone prop prefabs may use their own
   basename (e.g. `mdl_passenger_stairs_v01`). Lofted hero kits use distinct ids
   (e.g. `mdl_regional_turboprop_01_lofted_v01`) — never race an existing
   `*_v04` filename.
4. Prefabs may ship without authored `.mat` files when they include
   `AirsideRuntimeMaterialBinder`, which applies Lit profiles at Awake.

## Reason

Decision 0025 requires moving off the interim filesystem glTF parser toward
Unity-imported meshes with authored URP materials. A loader facade and drop
folder let Bailey import real prefabs on the Mac without rewriting every caller.
The Resources drop folder plus a runtime Addressables locator prove the
production key contract (`airside-prefab/<key>`) end-to-end before Editor groups
and authored FBX land.

## Affected systems

- Presentation loaders (`ArtPresentationLoader`, `AirsidePrefabAddressables`)
- `Packages/manifest.json` (Addressables dependency)
- `Assets/Resources/Airside/Prefabs/` drop target
- `AirsideRuntimeMaterialBinder`

## Migration impact

No save or simulation change. Existing StreamingAssets kits remain the default
for aircraft/buildings until Bailey drops matching prefabs. When Editor
Addressables groups register the same keys, they participate in
`Addressables.ResourceLocators` alongside (or instead of) the runtime locator.
