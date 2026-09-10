# 0031 — CC0 Unity Terrain for the airfield ground

- **Date:** 2026-09-10
- **Owner:** Cursor (implementation), following `docs/art/FREE_GROUND_SOLUTION.md`
- **Status:** Implemented on `feature/cc0-terrain-ground`; awaiting Unity compile,
  Mac build and packaged visual QA

## Decision

Replace the flat, repetitive natural-ground slab with one authored Unity Terrain
built on the built-in URP Terrain Lit shader and four CC0 PBR TerrainLayers, as
specified in `FREE_GROUND_SOLUTION.md`. No MicroSplat, no paid asset and no
runtime terrain plugin.

The terrain is baked once in the Editor and instantiated by the player. The
procedural `Airfield terrain base` slab remains as a fallback.

## Reason

Texture replacement alone could not reach the reference. The old ground was one
grass PNG repeating every 5 m across a dead-flat 210 × 180 m slab, so it showed
straight material borders, an obvious tile grid, no drainage or shoulder relief,
and no transition from grass to dirt to sand. The reference depends on those
broad transitions more than on fine texture detail.

## Affected systems

- `Presentation`: `AirsideTerrainField` (authored landform, no UnityEngine
  types), `AirsideTerrainGround` (instantiate the baked prefab),
  `AirsidePrototype.BuildAirfield` (one branch), `AirsideStaticWorld.IsDynamic`
  (one name).
- `Editor`: `AirsideTerrainBakerMenu`, driven by `scripts/bake-terrain.sh`.
- Art: `Art/Textures/Terrain/` (12 maps), `Art/Textures/Surfaces/` (apron
  concrete v03), `Art/Terrain/` (bake output),
  `Resources/Airside/Prefabs/mdl_kingscote_terrain_v01.prefab` (bake output).
- Tooling: `scripts/generate-cc0-terrain-ground.py`,
  `scripts/sync-art-streaming-assets.sh`, `scripts/dotnet-harness/Harness.csproj`.

Simulation, aircraft movement, taxi routes, collision logic, stand positions,
saves and the HUD are untouched. There is no persisted schema change, so no
migration is needed.

## Where this differs from FREE_GROUND_SOLUTION.md

1. **Maps ship at 1024px, not 2K.** Sources were downloaded at 2K as specified
   and their hashes recorded, but the committed runtime maps are 1024. This
   matches the already-registered asphalt v03 set and serves the sub-1 GB settled
   RSS target in acceptance criterion 6. Sixteen 2K maps would add roughly 60 MB
   of decoded texture memory for detail that is never seen: the closest the
   camera gets to bare ground still spans several metres per screen inch.

2. **TerrainLayer maps live in `Art/Textures/Terrain/`,** not
   `Art/Textures/Surfaces/tx_ground_*`. Surfaces is the runtime-loaded set that
   resolves through `ArtRuntimePaths`/StreamingAssets; TerrainLayer maps are
   Editor-imported and must not be duplicated into StreamingAssets. Keeping them
   in a separate folder is what makes the sync exclusion honest rather than a
   special case on a filename.

3. **The terrain material lives in `Art/Terrain/`,** not
   `Art/Materials/Terrain/`, so all four bake outputs sit together and
   `bake-terrain.sh` names one directory.

4. **Low-frequency luminance is divided out of every layer's albedo.** The packet
   says not to *add* high-frequency colour noise; this instead *removes*
   large-scale variation. A photographic ground texture carries a bright or dark
   blotch that becomes the tell when tiled across 256 m. Flattening it means all
   large-scale variation comes from the splatmap, which is what the packet asks
   for. Tile-scale spread is now 0.5–2.0 luminance points with per-pixel detail
   std preserved at 8.9–23.8.

5. **Two mid-frequency fixes the packet does not mention.** Ground 030's grass
   tufts were suppressed (2.34% of pixels, but the most distinctive feature, so
   they read as repeating landmarks), and the apron concrete was desaturated and
   contrast-halved. Both are documented in the generator.

6. **Height-based blending is enabled on the terrain material.** Straight weight
   blending averages two ground types into mud; height blending lets dirt sit
   down into the grass. This is material state (`_EnableHeightBlend` plus its
   `_TERRAIN_BLEND_HEIGHT` keyword) and is set the way URP's own
   `TerrainLitShaderGUI` sets it.

7. **No TerrainCollider.** `com.unity.modules.terrainphysics` is not in the
   project, and aircraft and vehicles are placed by the simulation rather than by
   physics. `com.unity.modules.terrain` is now an explicit manifest dependency.

8. **The plateau is flat, not merely "operational pavement remains level".** The
   whole footprint X [-64, 66] × Z [-12, 60] is pinned to one Y. The simulation
   owns every coordinate on it, so the terrain is given no opinion about height
   anywhere an aircraft or vehicle can reach, rather than being sculpted around
   each pad.

9. **Pavement clearance is 2–5 cm only on the lowest pad.** The terrain sits at
   -0.045 and the lowest operational pad top (runway blast W/E) is at -0.010, so
   that pad is 3.5 cm proud. The apron (+0.060) and Stand 3 (+0.095) stand
   further proud than 5 cm. Their Y is existing operational geometry and moving
   it would shift gameplay surfaces, which this change is not allowed to do.

10. **The terrain boundary is hidden by a lip in the landform,** not by a skirt
    mesh. The last 15 m of every edge drops 1.55 m so the edge tucks under the
    existing context scenery. No new mesh was needed.

11. **`Infield grass` is kept, not removed.** Its top is at -0.41, below the new
    terrain, so it is hidden rather than deleted. It is named in the wet-surface
    collector and in acceptance criterion 5.

## Migration impact

None. No persisted schema changes. A checkout that has not run
`scripts/bake-terrain.sh` renders the previous procedural slab.

## Outstanding

The `.asset`, `.terrainlayer`, `.mat` and `.prefab` bake outputs are not
committed: this was implemented on a Linux VM with no Unity editor, and
hand-authoring a TerrainData heightmap and splatmap as YAML would have been an
unverifiable binary-equivalent guess. Run `scripts/bake-terrain.sh` once on the
Mac and commit the outputs with their `.meta` files.

Acceptance criteria 1, 2, 3, 6, 7 and 8 all need the packaged Mac player and
remain unverified.
