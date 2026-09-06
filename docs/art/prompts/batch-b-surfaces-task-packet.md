# Batch B — world surfaces, markings and environment

**Status:** Generated/Modelled candidates delivered 2026-09-06 — awaiting Bailey
review (see `batch-b-surfaces-generation-2026-09-06.md`).  
**Date:** 2026-09-06  
**Decision / contract:** `docs/decisions/0022-art-direction-and-asset-pipeline.md`,
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`  
**Visual authority:** Approved Batch A references in `docs/art/reference/`
(REF-001 day, REF-002 dusk, REF-005 materials/palette). Do not redesign aircraft,
buildings, vehicles, liveries, palette or HUD language from Batch A.

## Player-visible outcome

When Batch B is later Integrated (separate task), the Kingscote greybox gains
believable asphalt, concrete apron, dry grass and building metal, plus readable
runway/taxi/stand markings, edge/apron lights and small airfield props — matching
the approved miniature look — while simulation behaviour and procedural fallbacks
remain unchanged until each asset is Verified.

This packet only authorises **production and review** of candidates. It does
**not** authorise Unity scene wiring or removal of `CreatePrimitive` fallbacks.

## Scope

| In scope | Out of scope |
|---|---|
| TEX-SRF-001…004 basecolour PNGs | Batch C aircraft/buildings/vehicles models |
| TEX-ENV-001 glass mask | Batch D animation / VFX |
| TEX-DEC-001…002 wear/stain decals | Brand wordmark / splash (BRD-001, UI-ILL-001) |
| MAT-001 shared material library (Unity later) | Runtime integration / `.meta` GUID work |
| WLD-001 markings kit FBX | Simulation, saves, Domain code |
| WLD-002 lighting kit FBX | Redesign of Batch A refs |
| WLD-003 props kit FBX | Real airline marks, watermarks, signatures |
| Prompt/evidence record + asset register rows | Changing reservation / clock / RNG rules |

All runtime paths below are relative to
`game/Airside/Assets/Airside/Art/`. Create folders only when placing files.
Unity `.meta` files are created in the Unity Editor on import — do not invent
GUIDs by hand.

## Deliverables (manifest IDs)

### Surfaces (tileable PNG, power-of-two, normally 2048×2048, sRGB basecolour)

| ID | Exact path | Requirement |
|---|---|---|
| TEX-SRF-001 | `Textures/Surfaces/tx_asphalt_runway_basecolor_v01.png` | Seamless restrained aggregate; Tarmac `#343B40` family; **no painted markings** |
| TEX-SRF-002 | `Textures/Surfaces/tx_concrete_apron_basecolor_v01.png` | Seamless large slab variation; Concrete `#9CA3A2` family; joints separate or shader-scaled |
| TEX-SRF-003 | `Textures/Surfaces/tx_grass_kingscote_basecolor_v01.png` | Seamless dry-green regional grass (Dry Grass `#8A8A58` / Eucalyptus `#4F6F60`); no flowers or objects |
| TEX-SRF-004 | `Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png` | Neutral corrugated metal tintable for hangar/shed |

### Environment / decals

| ID | Exact path | Requirement |
|---|---|---|
| TEX-ENV-001 | `Textures/Environment/tx_terminal_glass_mask_v01.png` | Window variation mask; no fake people or unreadable signage |
| TEX-DEC-001 | `Textures/Decals/dc_runway_wear_v01.png` | Transparent subtle rubber/wear; no text |
| TEX-DEC-002 | `Textures/Decals/dc_apron_stains_v01.png` | Transparent restrained service wear; no text |

### Materials (Unity-side; after textures land)

| ID | Exact path | Requirement |
|---|---|---|
| MAT-001 | `Materials/mat_airfield_surface_library_v01.mat` | Shared asphalt, concrete, grass, glass, painted line and metal materials referencing the textures above |

MAT-001 may wait until textures are Approved and opened in Unity 6.3 LTS. Document
the intended material slots in the generation evidence if the `.mat` is deferred.

### World kits (metres, named materials, precision geometry)

| ID | Exact path | Requirement |
|---|---|---|
| WLD-001 | `Models/Props/mdl_airfield_markings_kit_v01.gltf` | Runway centre/edge/threshold, taxi centreline, two stand stop bars — **precision meshes**, not AI-painted text |
| WLD-002 | `Models/Props/mdl_airfield_lighting_kit_v01.gltf` | Runway edge, taxiway, apron floodlight, obstruction lights — readable at dusk (match REF-002) |
| WLD-003 | `Models/Props/mdl_airfield_props_kit_v01.gltf` | Windsock, cones, barriers, signs, baggage dollies — match REF-001 / REF-005 scale |

## Style and generation rules

- Match REF-001 / REF-002 / REF-005 materials and palette. Prefer quiet tileables
  over noisy photographic scans.
- Surface textures: tileable; no logos, numbers, arrows or baked runway paint on
  basecolours (paint lives in WLD-001).
- Decals: straight alpha; subtle; must not scream at overview zoom.
- Models: FBX or glTF source; metres; sensible pivots; two LODs preferred for
  first playable; shared materials over unique 4K maps.
- No real airline branding, watermarks, signatures or named living-artist imitation.
- Name files lowercase snake_case with `_v01`; bump version for a visibly
  different candidate — never overwrite an Approved file in place.

### Suggested texture prompts (edit per tool; keep evidence)

Shared prefix: *Premium stylised-realism Australian regional airfield texture for
Airside; architectural miniature; restrained PBR basecolour; seamless tile;
2048×2048; match approved Airside palette; no text, logos, markings, watermark.*

- **TEX-SRF-001:** dark asphalt aggregate near `#343B40`, soft variation, runway
  surface without paint.
- **TEX-SRF-002:** pale concrete apron near `#9CA3A2`, large quiet slabs, optional
  faint joint hint only.
- **TEX-SRF-003:** dry-green Kingscote grass mix `#8A8A58` / `#4F6F60`, no flowers.
- **TEX-SRF-004:** neutral corrugated metal, soft specular-friendly albedo, tintable.
- **TEX-DEC-001 / 002:** transparent PNG wear/stain overlays, sparse, soft edges.

Markings/lights/props should be modelled or kitbashed from clean geometry using
REF-001/002/005 orthographic intent — do not bake unreadable AI text into meshes.

## Evidence and register

For each delivered file or coherent sub-batch:

1. Add or update a prompt/evidence note under `docs/art/prompts/` (generator,
   date, dimensions, edit chain, cost/terms).
2. Add a row to `docs/data/ASSET_AND_DATA_REGISTER.md`.
3. Set manifest Status to `Generated/Modelled`, then `Review`, then `Approved`
   only after Bailey accepts the look.
4. Fallback until Integrated: existing Unity primitives in
   `AirsidePrototype.cs` / procedural materials.

## Acceptance criteria

- [ ] Every ID above exists at the exact path (or an explicit deferred note for
      MAT-001 only) with snake_case `_v01` naming.
- [ ] Surface PNGs are power-of-two, tile without obvious seams, and contain no
      painted markings or text.
- [ ] Decals use transparency and stay subtle at overview camera distance.
- [ ] WLD-001 markings are mesh-based and read clearly in an elevated
      three-quarter view comparable to REF-001.
- [ ] WLD-002 lights support a dusk-readable layout comparable to REF-002.
- [ ] WLD-003 props match Batch A scale language (REF-005).
- [ ] Palette identity matches the art contract (Tarmac, Concrete, Dry Grass,
      Eucalyptus, Sand, Coastal Blue accents where relevant).
- [ ] Asset register + prompt evidence committed in the same change as the files.
- [ ] Manifest statuses updated; no silent overwrite of Approved Batch A refs.
- [ ] No Domain/Simulation/Persistence behaviour change in this batch.

## Playtest / review steps (before Integration)

1. Open REF-001, REF-002 and REF-005 beside the candidates.
2. Tile each surface at least 2×2 and check seams and noise at overview zoom.
3. Drop markings/lights/props into a scratch Unity scene (or DCC viewport) at
   metre scale next to a stand-sized placeholder; confirm readability.
4. Bailey marks Approved in the manifest; only then schedule Integration.

## Must remain unchanged

- Approved Batch A reference files and their composition/palette/HUD language.
- Deterministic simulation, clock, seeded random, reservations, saves.
- Procedural primitive fallbacks until a later Integration task wires Approved
  assets and verifies day/dusk/overview/follow performance on Mac.
- Decision numbering: art pipeline is **0022**; daily ops report remains **0018**.

## Handoff after this packet

1. Produce candidates (textures first is fine; kits can follow).
2. Review → Approved.
3. Separate narrow PR: Unity import + MAT-001 + optional early scene wiring with
   fallbacks still present.
4. Then Batch C model packet / production.
