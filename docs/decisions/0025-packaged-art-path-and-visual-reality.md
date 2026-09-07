# 0025 — Packaged-build art path and visual delivery reality

**Date:** 2026-09-07  
**Status:** Accepted (Bailey visual assessment)  
**Decision owner:** Bailey

## Decision

1. **Packaged builds must load runtime art from a shippable location.** The interim
   filesystem glTF/PNG loaders (`ArtGltfLoader`, `AirsideTheme`, surface PNG loads)
   resolve files via `ArtRuntimePaths`: prefer
   `StreamingAssets/Airside/Art`, then fall back to `Assets/Airside/Art` in the
   Editor. Contributors re-run `scripts/sync-art-streaming-assets.sh` whenever
   runtime art under `Assets/Airside/Art` changes.

2. **The custom glTF parser is interim, not the production pipeline.** Production
   presentation will move to Unity-imported meshes/prefabs and/or Addressables with
   authored URP materials. Procedural cuboids remain fallbacks only.

3. **Honest visual status.** As of 2026-09-07 packaged-build playtesting, Airside's
   presentation is roughly **~20% of the screenshot / REF target**. Most completed
   work is simulation, systems and reference planning. Batch C kits that appear
   "Integrated" are low-poly placeholders (e.g. turboprop ~336 verts / 14 meshes,
   terminal ~120 verts / 5 meshes) with no authored materials; environment is still
   a flat green plane with block buildings and IMGUI debug-style HUD.

4. **Visual delivery backlog (ordered).** Do not treat reference images as shipped
   content. Next presentation work, after the StreamingAssets path fix, follows:

   1. Art pipeline: StreamingAssets (now) → Unity import / Addressables (next).
   2. Replace placeholder 3D set (turboprop, terminal, hangar, ops, vehicles, props).
   3. Build the environment (terrain, coast, vegetation, fencing, roads, sky).
   4. Create a coherent URP material library (normals, roughness, AO, wet variants).
   5. Redo lighting and rendering (shadows, AO, probes, tonemapping, day profiles).
   6. Rebuild the HUD (UI Toolkit or uGUI; replace dense IMGUI).
   7. Add motion and life (gear, doors, wheels, service animation, audio).
   8. Integrate Bailey-approved brand assets (wordmark + dawn splash) into menu/load.

5. **Brand candidates stay candidates** until Bailey's visual approval, then Unity
   import and menu/loading-screen wiring.

## Reason

A playtest of the packaged macOS build showed integrated art silently missing
because loaders searched `Application.dataPath` (Editor Assets layout). Players
only saw coloured primitives. Continuing to generate art against an Editor-only
loader wastes production. Separately, calling low-poly kits "Integrated" without
stating fidelity misleads the first-playable plan.

## Affected systems

- Presentation runtime loaders and StreamingAssets sync
- Packaged macOS builds / playtest evidence
- Art direction status language and first-playable visual acceptance
- Future Addressables / prefab pipeline

## Migration impact

No save or simulation migration. Existing `Assets/Airside/Art` remains the authoring
source of truth; StreamingAssets is a sync target for the interim filesystem
loaders. When Addressables/prefabs land, StreamingAssets glTF copies can be removed.
