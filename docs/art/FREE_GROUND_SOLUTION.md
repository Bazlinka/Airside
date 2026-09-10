# Free ground solution for Airside

**Status:** Selected for implementation  
**Date:** 2026-09-10  
**Owner:** Codex research pass  
**Cost:** A$0  
**Target:** `ref_airport_first_playable_day_v01.png` and `ref_airfield_surface_texture_board_v01.png`

## Decision

Build the natural airfield ground as one authored Unity Terrain, using Unity's
built-in URP Terrain shader and four blended CC0 PBR layers. Keep the runway,
taxiway and apron as separate operational meshes above the terrain. This gives
Airside soft grass/dirt/sand transitions, gentle landform relief and large-scale
colour variation without another runtime plugin or another field of cubes.

Unity Terrain is already part of Unity. The optional `com.unity.terrain-tools`
5.3 authoring package is released for Unity 6 and can be removed after the
TerrainData is baked. The shipped player needs no paid terrain package.

## Selected free material set

Download the **2K JPG** set for each source. Import base colour as sRGB and the
normal, AO, roughness and displacement maps as non-colour data. Keep the original
download ZIP and SHA-256 evidence under `work/`; commit only the processed 2K
runtime maps and their `.meta` files.

| Layer | Source | Use in Airside | Licence |
|---|---|---|---|
| Dry grass | [ambientCG Ground 013](https://ambientcg.com/view?id=Ground013) | Main 60–75% infield and paddock layer; tint toward Dry Grass `#8A8A58` | CC0 |
| Green grass | [ambientCG Ground 003](https://ambientcg.com/view?id=Ground003) | Irregular 10–20% patches near drainage, terminal landscaping and shaded scrub | CC0 |
| Worn dirt | [ambientCG Ground 030](https://ambientcg.com/view?id=Ground030) | Runway/taxi shoulders, vehicle wear, fence and scrub transitions | CC0 |
| Coastal sand | [Poly Haven Coast Sand 01](https://polyhaven.com/a/coast_sand_01) | Dune and shoreline transition; tint slightly pale toward Sand `#C8B286` | CC0 |
| Runway asphalt | Existing [Poly Haven Asphalt 01](https://polyhaven.com/a/asphalt_01) v03 | Keep on runway and taxi meshes | CC0; already registered |
| Apron concrete | [Poly Haven Worn Concrete Floor](https://polyhaven.com/a/worn_concrete_floor) | Pale worn apron surface under separate expansion-joint and stain decals | CC0 |

Both [ambientCG](https://ambientcg.com/view?id=Ground013) and
[Poly Haven](https://polyhaven.com/license) explicitly permit commercial use,
redistribution and use without required attribution under CC0. Voluntary artist
credits should still be retained in `docs/data/ASSET_AND_DATA_REGISTER.md`.

## Why the current ground misses the reference

The current runtime creates large flat slabs and repeats one grass texture over
them. Texture replacement alone leaves straight material borders, obvious
tiling, no shallow drainage or shoulder relief, and no natural transition from
grass to dirt to sand. The reference depends on those broad transitions more
than on very fine texture detail.

## Implementation task packet

### Player-visible outcome

From the default overview, the airport reads as one dry-green Kangaroo Island
landform. Grass varies in broad irregular patches. Dirt shoulders soften the
edges of runway, taxiway, apron, fence and scrub. Pale sand appears only toward
the coast. Runway asphalt stays dark and crisp; apron concrete stays pale and
worn. No square terrain boundary, repeated checker pattern or white gap is
visible from the normal camera.

### Files in scope

- `game/Airside/Assets/Airside/Art/Textures/Surfaces/tx_ground_*_v01.*`
- `game/Airside/Assets/Airside/Art/Materials/Terrain/`
- `game/Airside/Assets/Airside/Art/Terrain/terrain_kingscote_first_playable_v01.asset`
- `game/Airside/Assets/Airside/Art/Models/Environment/` only if a low-poly skirt
  mesh is needed to hide the Terrain edge
- `game/Airside/Assets/Airside/Editor/` for a deterministic one-shot TerrainData
  baker
- the narrow presentation hook that instantiates the baked terrain and disables
  the current flat natural-ground base
- `docs/data/ASSET_AND_DATA_REGISTER.md`, `GAME.md`, and `CHANGELOG.md`

### Terrain specification

- One TerrainData asset, approximately **256 m × 220 m × 8 m**.
- Heightmap resolution **257**; alphamap resolution **256**; basemap resolution
  **1024**; heightmap pixel error **8** for the desktop High profile.
- Four TerrainLayers only: dry grass, green grass, worn dirt and coastal sand.
- Use a 12–20 m tile size for grass and dirt, then break repetition with the
  painted layer weights. Do not add high-frequency colour noise to the albedo.
- Keep the runway/taxi/apron meshes 2–5 cm above the terrain to prevent z-fight.
- Sculpt only gentle drainage, shoulders and distant undulation. Operational
  pavement remains level and all simulation coordinates remain unchanged.
- Paint a 2–5 m dirt transition beside operational pavement and a wider irregular
  dirt/sand blend under scrub and toward the coast.
- Add sparse existing VEG-002 grass tufts at silhouette-important edges. Do not
  import photoreal two-million-triangle grass models.
- Use the source OpenGL/Y+ normals; Unity expects Y+ normal maps.
- Bake TerrainData and splat weights in the Editor. Do not generate heightmaps or
  full splatmaps during player startup.

### Material treatment

- Dry grass is the dominant base. Colour grade it into the approved Eucalyptus /
  Dry Grass palette before import; avoid a bright golf-course green.
- Green grass is a low-frequency accent, not a second checker tile.
- Dirt carries the runway shoulder and service wear transition.
- Sand is restricted to coastline/dune areas.
- Asphalt and concrete retain separate shared URP materials so wetness, painted
  markings and operational collection names continue to work.
- Put expansion joints and apron stains in decals or a low-frequency overlay;
  they must not be baked into every repeated concrete tile.

### Acceptance criteria

1. Default day overview resembles the approved first-playable reference in
   ground colour hierarchy: dry-green field, dark runway/taxiway, pale apron,
   warm dirt/sand edges.
2. No obvious repeating texture appears during a 20-second stationary overview
   or while following one arrival and one departure.
3. No rectangular white/grey gaps or visible terrain edge appear in overview.
4. Runway, taxiway, stand and ground-traffic coordinates are unchanged.
5. Wet-surface collection still finds `Runway W`, `Apron `, `Taxiway A`,
   `Infield grass`, `Taxi centre` and `Taxi exit centre`.
6. The Mac packaged player reaches the welcome/game frame without the previous
   startup RAM spike; settled RSS target is **under 1 GB** on the current Mac.
7. Day, dusk, night and rain preserve readable aircraft, markings and pavement.
8. Editor Play and packaged Mac player show the same ground layers.

### Verification

- `bash scripts/sync-art-streaming-assets.sh` and confirm expected asset changes.
- `bash scripts/test-unity.sh`.
- `bash scripts/build-mac.sh`.
- Capture matched default-overview screenshots at day, dusk, night and rain.
- Watch one full arrival and departure, checking wheels never clip below terrain.
- Check `Player.log` for missing terrain, texture, shader and Addressables paths.
- Record texture source URLs, authors, licence, downloaded-file hashes and
  processed-file hashes in `docs/data/ASSET_AND_DATA_REGISTER.md`.

### Must remain unchanged

- Simulation timing, taxi graph, stand positions and collision/reservation rules.
- Aircraft/GSE movement work on `cursor/game-performance-pass-c1bb`.
- Runtime wetness and operational-surface names.
- Existing procedural natural-ground presentation remains available as a
  fallback until the baked terrain passes packaged visual QA.

## Rejected shortcuts

| Shortcut | Reason |
|---|---|
| Replace the current grass PNG only | Leaves flat geometry, hard borders and obvious repetition. |
| Import a large photoreal environment pack | Style and performance are uncontrolled; many “free” packs restrict redistribution. |
| Add MicroSplat immediately | The base package is free, but Airside does not need another runtime shader dependency for four layers. |
| Return to thousands of terrain cubes | Previously blocked startup and caused excessive memory use. |

## Recommended first implementation slice

Build the terrain with Ground 013, Ground 030 and Coast Sand 01 first. Keep the
existing asphalt and concrete for that comparison. Once the geometry and blends
match the reference, add Ground 003 accents and the worn-concrete candidate.
This separates the important landform/blending decision from a later material
polish decision and produces a reviewable screenshot quickly.
