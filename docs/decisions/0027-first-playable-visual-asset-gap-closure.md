# 0027 — First-playable visual asset gap closure

**Date:** 2026-09-07  
**Status:** Accepted  
**Decision owner:** Bailey

## Decision

The remaining visual work for the Mac first playable is defined as ordered Batch F
slices in `docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md`.

Production concentrates first on authored replacement assets for the hero
turboprop, regional terminal and shared Unity material family. Turnaround
vehicles/people, Australian vegetation/boundary modules, and reusable
animation/VFX/UI finish follow only after the preceding slice is integrated and
verified in the packaged Mac build.

Procedural primitives, generated glTF kits and current runtime animation remain
fallbacks. Increasing procedural object density or mesh count alone does not count
as completing an authored Batch F asset.

## Reason

A repository-wide audit found that asset loading, references and presentation
coverage are now broad, but the most visible objects are still procedural
placeholders. Further broad prop generation would add quantity without closing the
gap to REF-001/003/005. Ordered replacement slices give Cursor exact keys, pivots,
budgets, fallbacks and acceptance checks while preserving the first-playable focus
from decision 0024.

## Affected systems

- Art manifest, evidence and source register
- Addressables/Resources prefab keys and existing glTF fallbacks
- Aircraft/building/vehicle presentation hooks
- Procedural people, vegetation, fence, terrain and landside builders
- Batch D authored clips/VFX prefabs and system-control icons

## Migration impact

No simulation or save migration. Operational geometry and reservations remain
unchanged. Asset integration is presentation-only and must retain the current
fallback chain.
