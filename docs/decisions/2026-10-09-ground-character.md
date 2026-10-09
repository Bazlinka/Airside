# Ground character and restrained surface wear

Status: Accepted — Bailey requested a less perfect, more authentic ground/world.
Date: 2026-10-09. Task: #687. Owner: Codex.

## Decision

Add original procedural character to the existing ground materials: local bare/matted
islands, dry/damp colour variation, interrupted mowing and metre-scaled fine grain.
Carry the same dry-ground character through the Adelaide mesh boundary; keep the
mapped satellite/land-cover shapes and water treatment. Surrounding land gains
restrained nearby grain that fades before the distant satellite view.

Opaque upward-facing pavement distinguishes asphalt resurfacing/sealed cracks from
offset concrete slab joints and slab ages. World anchoring uses the retained flight
origin, pixel derivatives filter thin lines, and camera distance fades fine detail.
Apron repairs have more small/occasional larger patches and two surface ages batched
separately. Whole patches fit within mapped apron edges, with a one-metre corner inset.

## Reason

Scanned textures and broad noise alone leave large ground areas reading as one
uniform material. Different use, maintenance and moisture should create distinct
local variation. Keep the miniature readable and maintained rather than filling
operational surfaces with rubble or adding invented geographic features.

## Affected systems and limits

AdelaideGround, Surroundings and Pavement shaders; shared `GroundCharacter.hlsl`;
AirsideMaterialLibrary; apron wear placement and its existing renderer. No new
texture samples in the shared character functions. One extra static repair batch
at Adelaide replaces the old coarse joint batch when the detailed shader is available;
coarse joints remain as the missing-shader fallback. No new per-frame objects or
update loop. Existing material fallback
remains available when a shader is missing.

Terrain heights, pavement outlines, markings, simulation, weather timing, save
format and aircraft remain unchanged. No external source/asset/cost. Existing
registered texture licences and attribution still apply.

## Migration and verification

No save/data migration. Focused apron placement regression and C# syntax checks;
shader source/diff inspection. Native shader compilation, day/dusk/night wet/dry
appearance, visual strength and GPU performance remain unverified. No player
build or full test suite requested. Exact evidence: `docs/testing/ground-character-2026-10-09.md`.
