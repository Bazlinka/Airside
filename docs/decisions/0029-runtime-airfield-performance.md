# 0029 — Combined airfield, shared materials, Addressables on demand, graphics ladder

**Date:** 2026-09-09  
**Status:** Accepted  
**Decision owner:** Cursor (under Bailey performance pass)

## Decision

1. **P0 tile airfield retired.** `BuildAirfield()` no longer emits the 745-tile
   Terrain11 operational dump or the 3,160-tile terrain-kit paddock dump. One
   slab per operational surface (`BuildCombinedOperationalSurfaces`) plus the
   WLD-004 context terrain kit covers the same ground. `UseTileOperational` and
   `UseTilePaddock` stay false. Greybox fallbacks are a handful of combined
   coast/paddock/hill slabs, not thousands of cubes. IL2CPP still compiles
   `BuildAirfieldTerrain11Operational`, but that method now forwards to the
   combined pads.
2. **P0 texture and material sharing.** Streamed PNGs cache by path + colour
   space + wrap (`AirsideArtTextures`). Surfaces assign
   `Renderer.sharedMaterial` from `AirsideMaterialLibrary.CreateShared`. Tints and
   animated emission go through `MaterialPropertyBlock`. Unique materials remain
   only where the shader state itself is animated (wetness on the shared paved
   set, horizon-dome cull).
3. **P1 static scenery.** Runtime airfield children are marked static and combined
   with `StaticBatchingUtility`. Distant hills / paddock / terrain base get a
   coarse LOD. Aircraft, GSE, lights, weather, hold-short pulses, flags, hangar
   doors, clouds and people stay dynamic.
4. **P1 Addressables on demand.** Startup no longer `Resources.LoadAll`s the 66
   prefabs. Keys resolve when first requested. A packaged Addressables catalog
   (`StreamingAssets/aa/settings.json`) is initialised when present; otherwise a
   Resources provider fills `airside-prefab/<key>` one asset at a time. StreamingAssets
   glTF copies stay until a Mac import pipeline is proven.
5. **P2 graphics ladder.** High is the documented PC look: 4× MSAA, SMAA high,
   140 m / 4 cascades, 12 additional lights, two probes. Medium: 2× MSAA, SMAA
   high, 55 m / 2 cascades, 4 additional lights, apron probe only at 64 px.
   Default vsync is 1. Choose Medium when GPU memory is under 2 GB or the CPU
   has ≤4 cores and under 8 GB RAM.
6. **P2 probes.** Apron probe re-renders only when `ProbeBand(daylight, wetness)`
   changes (night / dusk / day / wet). Terminal probe is skipped on Medium and is
   not periodically re-rendered on High.

## Reason

The generated cube airfield blocked the packaged player's first frame, duplicated
texture and material state, and kept scenery dynamic. High-path graphics stay;
cheaper machines get a Medium ladder instead of silently dropping bloom/SSAO.

## Affected systems

- Presentation only (`AirsidePrototype`, loaders, material library, quality)
- `QualitySettings` PC vsync/MSAA; PC URP dynamic batching
- EditMode `PresentationLayoutTests`

## Migration impact

No save or simulation change. Frame rate still must not change simulation
outcomes. Mac Play must confirm the combined pads + WLD-004 kit still read as
Kingscote at overview and follow, day/dusk/night.

## Follow-up (do not stop)

- Bake real Addressables groups on Mac and drop duplicate StreamingAssets kits
  once the player loads only from those groups
- Author LOD meshes for kit buildings instead of runtime combine
- Bake the terminal probe; keep one apron probe for rain/night
- Remaining during-build `GameObject.Find` probes (kit-presence gates) can use
  the incremental `AirsideSceneIndex.Remember` path once every kit placer
  registers names
- Author a single fuel-farm / ALS mesh instead of greybox fallbacks
- Runtime `ArtGltfLoader.TryPlaceCombined` now stamps fence bays, lamps,
  ALS stations, REIL, cones, barriers, signs, bins, dollies, windsock poles,
  stairs, GPU carts, scrub and eucalyptus as cached combined meshes; authored
  single-mesh kits would still cut remaining submeshes
- Static combine skips whole dynamic subtrees (GSE, clouds, birds, boats)
  so those transforms still move after `StaticBatchingUtility.Combine`
