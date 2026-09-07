# Airside image reference index for AI tools

Use this page as the shortest reliable entry point when creating Airside visual
work. The canonical rules remain `ART_DIRECTION_AND_ASSET_SPEC.md`; this index
only maps the image evidence and prevents tools from treating every PNG as equal.

## Read first

1. `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` — palette, camera, style, paths,
   lifecycle and exact asset IDs.
2. `docs/art/reference/ref_airport_first_playable_day_v01.png` — authoritative
   daytime composition, airport layout and world design.
3. `docs/art/reference/ref_airport_first_playable_dusk_v01.png` — authoritative
   dusk, apron and runway-lighting treatment.
4. `docs/art/reference/ref_asset_scale_and_palette_v01.png` — authoritative
   silhouettes, proportions, materials and palette.

Add these only when the task needs them:

- `ref_turnaround_service_zones_v01.png` for aircraft-service positions.
- `ref_operations_hud_v01.png` for UI hierarchy and visual language; its baked
  words are layout evidence only and must not be copied into runtime images.

All five files above are Approved. Do not overwrite or restyle them.

## Review candidates

None pending. BRD-001 and UI-ILL-001 were Approved by Bailey on 2026-09-07 and
promoted into runtime art (see production assets below).

Previously reviewed here:

| ID | Candidate source | Runtime path | Status |
|---|---|---|---|
| BRD-001 | `docs/art/candidates/airside_wordmark_light_v01.png` | `Art/Brand/airside_wordmark_light_v01.png` | Approved · Integrated |
| UI-ILL-001 | `docs/art/candidates/ui_splash_airport_dawn_v01.png` | `Art/UI/Illustrations/ui_splash_airport_dawn_v01.png` | Approved · Integrated |

## Existing production assets

- Surface and environment PNGs: `game/Airside/Assets/Airside/Art/Textures/`
- Approved glTF model kits: `game/Airside/Assets/Airside/Art/Models/`
- Approved runtime UI icons/panels: `game/Airside/Assets/Airside/Art/UI/`
- Generation and edit evidence: `docs/art/prompts/`
- Source, terms and fallback register: `docs/data/ASSET_AND_DATA_REGISTER.md`

Use the runtime assets when the task is implementation. Use Approved references
when the task is concept generation. Do not use the composite icon sheets as a
new style authority; the individual runtime icons are the prepared outputs.

## What is still missing

- Batch F1 authored AIR-001 v05 turboprop (**Integrated** — Mac FBX bake in Resources;
  Bailey accepted), then BLD-001 v05 terminal and MAT-001 material family
  (**Integrated** on `cursor/batch-f1-terminal-materials`; Bailey packaged playtest pending). Current
  glTF/runtime materials remain fallbacks for unfinished F1 items.
- Batch F2 turnaround vehicle and character replacements.
- Batch F3 vegetation, fence/gate, terminal forecourt and non-operational context.
- Batch D's actual `.anim`, controller and VFX prefab deliverables, grouped into
  Batch F4 with UI-ICO-005 system-control icons — **Integrated** (Toolkit chrome).
- Packaged Mac camera-matrix verification of each replacement before the next
  Batch F slice begins.

The exact paths, hierarchy, budgets, hooks and acceptance checks are in
`docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md`.

Later fleets, interiors, passengers, construction illustrations and companion art
are deliberately out of scope until their gameplay milestones are approved.

## Naming and prompt guardrails

- Lowercase snake case: `category_subject_variant_v01.ext`.
- Keep the asset ID, prompt, dimensions, generator/source, edit chain, rights,
  attribution, cost and fallback beside every delivered batch.
- Repeat the approved palette and elevated three-quarter miniature camera.
- No real airline marks, trademarked branding, watermarks, signatures or baked
  runtime UI text.
- Aircraft, buildings and vehicles require real 3D assets; a concept PNG is not
  a runtime substitute.
