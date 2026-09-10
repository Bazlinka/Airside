# 0035 — Adelaide bare-field authored ground mesh

**Date:** 2026-09-10
**Status:** Accepted
**Decision owner:** Cursor (visual-quality pass under Bailey's autonomous brief)

## Decision

1. **Bare Adelaide ground is an authored multi-layer mesh, not a stretched Kingscote
   Terrain and not a single tiled cube.** `AirsideBareField.Enabled` still bypasses
   `AirsideTerrainGround.TryBuild` (Kingscote 384×300 m landform). Instead
   `BuildBareAdelaideField` builds from `AirsideAdelaideGround` (pure functions) via
   `AirsideAdelaideGroundMesh` (one subdivided mesh, ~97×65 verts on High / ~65×45 on
   Medium).

2. **Layer weights and relief are pure functions of world XZ.** Same pattern as
   ADR 0031: headless tests and the runtime mesh sample identical math. The
   operational plateau covering the 3 100 × 45 m runway (plus margin) is dead level;
   subtle relief and a soft boundary lip live only outside that box.

3. **Three CC0 TerrainLayer albedos/normals** (dry grass, green grass, worn dirt)
   blend in the `Airside/AdelaideGround` URP shader using vertex colours as weights
   and world-metre UVs at non-harmonic tile sizes (47 / 37 / 29 m). No runtime
   heightmap or splatmap generation. If the shader or maps are missing, fall back
   to a grass slab with large irregular tiling.

4. **StreamingAssets now includes `Textures/Terrain/` PNGs** so the packaged player
   can resolve them through `ArtRuntimePaths`. Kingscote TerrainLayers still pack
   their own copies for the non-bare path.

5. **Runway stays exactly 3 100 × 45 m.** Multi-scale asphalt detail and dirt
   shoulders sit outside that width; paint lift is raised slightly to reduce
   shimmer. Markings and `AirsideFlightPath.TouchdownX` are unchanged.

## Reason

The bare field's single grass cube tiled every 16 m read as a giant board from
overview. Stretching the Kingscote Terrain prefab would repeat the wrong landform
across Adelaide's 3 400 × 2 309 m footprint. A modest mesh + multi-scale blend
keeps startup small, preserves a flat operational strip, and uses already-licensed
CC0 maps.

## Affected systems

- Presentation: `AirsideAdelaideGround`, `AirsideAdelaideGroundMesh`,
  `AirsidePrototype.BuildBareAdelaideField`, `Airside/AdelaideGround` shader,
  runway shoulder / multi-scale asphalt helpers
- Tooling: `scripts/sync-art-streaming-assets.sh`, `scripts/dotnet-harness`
- Tests: `AdelaideGroundTests`

Simulation, reservations, persistence and the Kingscote Terrain baker are
unchanged.

## Migration impact

None. Presentation-only. No save-schema change.
