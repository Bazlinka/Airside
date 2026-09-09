# Airside image reference index for AI tools

Use this page as the shortest reliable entry point when creating Airside visual
work. The canonical rules remain `ART_DIRECTION_AND_ASSET_SPEC.md`; this index
only maps the image evidence and prevents tools from treating every PNG as equal.

## Read first

1. `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` — palette, camera, style, paths,
   lifecycle and exact asset IDs.
2. `docs/art/FIRST_PLAYABLE_ART_SOURCING_CHECKLIST.md` — every first-playable
   item to source/author (wheels, props, engines, trees, GSE, audio, …) with
   in-git / quality / target-path columns for the refine pass.
3. `docs/art/FREE_CC0_SOURCE_AUDIT_2026-09-09.md` — verified free-source
   candidates and the evidence-based decision not to replace a stronger in-git kit.
4. `docs/art/reference/ref_airport_first_playable_day_v01.png` — authoritative
   daytime composition, airport layout and world design.
5. `docs/art/reference/ref_airport_first_playable_dusk_v01.png` — authoritative
   dusk, apron and runway-lighting treatment.
6. `docs/art/reference/ref_asset_scale_and_palette_v01.png` — authoritative
   silhouettes, proportions, materials and palette.

Add these only when the task needs them:

- `ref_turnaround_service_zones_v01.png` for aircraft-service positions.
- `ref_operations_hud_v01.png` for UI hierarchy and visual language; its baked
  words are layout evidence only and must not be copied into runtime images.

All five files above are Approved. Do not overwrite or restyle them.

## Approved modelling boards (Bailey 2026-09-09)

Bailey accepted these fidelity / style sheets. They are modelling and material
targets only — not runtime textures, billboards or mesh substitutes. Runtime
kits densify against them via PreferArtKit / PreferSurface / ARFF prefab prefer.

| Related asset | Approved reference | Intended use | Status |
|---|---|---|---|
| VEG-002 | `docs/art/reference/ref_kingscote_coastal_scrub_style_sheet_v01.png` | Five mallee/shrub forms, three grass patches, three rock groups and two dune-edge mixes at human scale | Approved · Integrated (`mdl_kingscote_scrub_kit_v02`) |
| WLD-004 | `docs/art/reference/ref_kingscote_context_terrain_catalogue_v01.png` | Modular paddock berms, low hills, dunes, turquoise shallows and deep-water context around a protected blank operational zone | Approved · Integrated (`mdl_kingscote_context_terrain_v02`) |
| BLD-001…003 | `docs/art/reference/ref_regional_airport_building_fidelity_board_v01.png` | Common-scale terminal, hangar and ops-shed silhouette/material target matched to REF-001 | Approved · Integrated (runtime v05 preferred) |
| AIR-001 / VEH-001…004 / PRP-001 | `docs/art/reference/ref_turnaround_service_set_day_dusk_v01.png` | Same safe turnaround arrangement at daylight and soft dusk, with overview-readable service fleet and connections | Approved · Integrated (fleet v06 / GSE v03 preferred) |
| CHR-001 / CHR-002 | `docs/art/reference/ref_airside_character_silhouette_kit_v01.png` | Three ramp roles plus six passenger stand/walk/sit silhouettes at one overview-readable miniature scale | Approved · Integrated (CHR kits v02 preferred) |
| TEX-SRF-001…003 / coast sand / MAT-001 wet | `docs/art/reference/ref_airfield_surface_texture_board_v01.png` | Five top-down square surface targets; labels remain outside crops; visual tiling guidance only — do not crop into runtime | Approved · Integrated (`tx_*_v02` preferred over v01) |
| ARFF truck / rescue shed | `docs/art/reference/ref_regional_arff_facility_fidelity_v01.png` | Compact fictional red/white rescue appliance at a modest open-bay shed, with background hangar scale cue | Approved · Integrated (`mdl_arff_*_v02` preferred) |

BRD-001 and UI-ILL-001 were Approved by Bailey on 2026-09-07 and promoted into
runtime art (see production assets below).

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

- Mac overview / follow / day-dusk matrix verification of each densify pass
  against the Approved modelling boards above.
- Batch F1 Mac FBX bake verification for unfinished F1 items where glTF remains
  the runtime fallback.
- Later fleets, interiors, passengers, construction illustrations and companion
  art remain out of scope until their gameplay milestones are approved.

The exact paths, hierarchy, budgets, hooks and acceptance checks are in
`docs/art/prompts/batch-f-first-playable-visual-assets-task-packet.md`.

## Naming and prompt guardrails

- Lowercase snake case: `category_subject_variant_v01.ext`.
- Keep the asset ID, prompt, dimensions, generator/source, edit chain, rights,
  attribution, cost and fallback beside every delivered batch.
- Repeat the approved palette and elevated three-quarter miniature camera.
- No real airline marks, trademarked branding, watermarks, signatures or baked
  runtime UI text.
- Aircraft, buildings and vehicles require real 3D assets; a concept PNG is not
  a runtime substitute.
