# 0018 — Art direction and asset pipeline

**Date:** 2026-09-06  
**Status:** Accepted  
**Decision owner:** Bailey

## Decision

Airside will use a single production art contract:
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`. The visual target is premium
stylised realism presented as a believable Australian architectural miniature.

Art is delivered in approved batches. Generated reference images lock composition,
palette and proportions before runtime production begins. Runtime 3D objects use
models/prefabs; generated flat images are not substituted for aircraft, buildings
or vehicles. Simulation owns state and timing. Presentation animation and VFX may
represent that state but may not drive it.

Every asset uses a stable ID and exact repository path, follows the documented
status lifecycle, and is recorded in the asset/data register before entering a
distributable build. Unity primitive visuals remain as fallbacks during migration.

## Reason

The project is being developed across ChatGPT, Claude, Cursor and Codex. Without
one manifest, tools can generate incompatible styles, invent paths, duplicate
assets or couple presentation work to deterministic simulation. Locking reference
art first gives all contributors the same target and makes asset integration
incremental and reversible.

## Affected systems

- Presentation scene construction and future prefabs
- UI/HUD implementation
- Unity asset import and repository layout
- Asset licensing/source records
- Visual QA and milestone handoffs

## Migration impact

No save or simulation migration. Existing procedural primitives remain active
until approved assets are integrated one batch at a time. Asset GUIDs are
preserved by moving integrated files only inside Unity.
