# 0026 — Prefab / Addressables art loader scaffold

**Date:** 2026-09-07  
**Status:** Accepted (implementation scaffold)  
**Decision owner:** Cursor (under Bailey 0025 backlog)

## Decision

1. Runtime art resolution goes through `ArtPresentationLoader`: Unity prefab under
   `Resources/Airside/Prefabs/<kit-basename>` first, then StreamingAssets glTF
   (`ArtGltfLoader`), then the caller's procedural fallback.
2. The Addressables package is added to the Unity project manifest so Mac Editor
   can create groups; the first shippable bridge is Resources prefabs so kits can
   be swapped without waiting for Addressables content builds.
3. Prefab keys match glTF basenames (`mdl_hangar_small_v02`, etc.) to keep Prefer
   v02 / fallback v01 naming stable.

## Reason

Decision 0025 requires moving off the interim filesystem glTF parser toward
Unity-imported meshes with authored URP materials. A loader facade and drop
folder let Bailey import real prefabs on the Mac without rewriting every caller.

## Affected systems

- Presentation loaders (`ArtPresentationLoader`, `PlaceBuildingOrFallback`, vehicle builds)
- `Packages/manifest.json` (Addressables dependency)
- `Assets/Resources/Airside/Prefabs/` drop target

## Migration impact

No save or simulation change. Existing StreamingAssets kits remain the default
until prefabs are dropped in. When Addressables groups land, swap the prefab
branch of `ArtPresentationLoader` to async Addressables loads.
