# Airfield surface texture-board candidate — 2026-09-09

**Status:** Approved by Bailey 2026-09-09 · Integrated.

**Related runtime assets:** TEX-SRF-001…003, `tx_sand_coast_*` and MAT-001 wet

**Visual authority:** Approved REF-001 and REF-005

## Output and provenance

- **Output:** `docs/art/reference/ref_airfield_surface_texture_board_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1536×1024 RGB
- **Prepared dimensions:** 2048×1365 RGB
- **SHA-256:** `ea47219d13d9189b54345f9adac2ac58907a1fd76f041a8ae961e420f96f4125`
- **Input references:** REF-001 airfield surface colour/scale and REF-005
  palette/material support
- **Edit chain:** one reference-guided generation; proportionally resized to
  2048 px wide; no in-painting
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** existing procedural tileable runtime maps and MAT-001 materials remain unchanged

## Exact prompt

```text
Use case: stylized-concept
Asset type: Airside surface-material fidelity board for TEX-SRF-001…003, coastal sand and MAT-001 wet concrete
Primary request: Create one clean catalogue board showing exactly five separate 1:1 square, straight top-down surface texture crops designed with visually seamless, non-directional edge character: dry-green Kingscote grass; worn concrete apron; asphalt runway aggregate; pale coastal sand; and a wet concrete variant. Each crop is a material reference for a premium stylised-realism architectural-miniature airport game.
Input images: Image 1 is approved REF-001 and establishes the actual airfield surface colors, scale and restrained Australian regional character. Image 2 is approved REF-005 and establishes the palette, simplified PBR response and material language.
Layout: Landscape sheet on a neutral warm-grey margin. Arrange five identical-size square swatches with generous gutters, three on the top row and two centred below. Every texture area must remain a perfect visible square, viewed exactly perpendicular from above with no perspective distortion, bevel, frame, object, cast shadow or overlapping element. Put one short exact label centred in the neutral margin directly below each square, never inside a texture crop. No title, legend, numbering or other text.
Exact labels, in this order: "DRY-GREEN GRASS"; "WORN CONCRETE APRON"; "ASPHALT RUNWAY AGGREGATE"; "PALE COASTAL SAND"; "WET CONCRETE". Render each label once only in a clean small uppercase humanist sans-serif, dark Runway Ink #17242A, fully readable and spelled exactly.
Style/medium: Premium stylised-realism PBR albedo/material reference, clean and restrained, with broad soft variation that reads at elevated overview scale and modest fine grain that survives close view. Avoid photographic debris or dramatic micro-detail.
Swatch 1 — dry-green Kingscote grass: Dry Grass #8A8A58 and Eucalyptus #4F6F60 blended into short dense regional turf with soft irregular colour variation; no flowers, individual tall blades, bare objects or obvious repeating clumps.
Swatch 2 — worn concrete apron: Concrete #9CA3A2 family, large quiet tonal variation, subtle aggregate and restrained diffuse service wear; no slab joints, cracks, oil pools, painted markings or directional tire trails.
Swatch 3 — asphalt runway aggregate: Runway Ink #17242A and Tarmac #343B40 family, fine restrained aggregate with even non-directional distribution and soft macro variation; no runway paint, rubber streak lettering, cracks or directional seams.
Swatch 4 — pale coastal sand: Sand #C8B286 family, fine compacted coastal grain with low-contrast natural variation and extremely subtle non-directional ripple texture; no shells, footprints, vegetation, water, dunes or objects.
Swatch 5 — wet concrete: the same concrete family as swatch 2 but slightly darker, cooler and lower-roughness, with a broad restrained damp sheen and soft reflected sky tone; no discrete puddle shapes, raindrops, drain covers, footprints, tire tracks or mirror-like reflections.
Lighting: Flat neutral top-down material-capture lighting. Even exposure and colour, no sun direction, horizon, vignette, depth-of-field blur or cast shadows within the swatches.
Constraints: Exactly five equal square crops and exactly five matching labels outside the texture areas. Keep all crop boundaries clean. Design edges to be visually compatible for tiling with no large feature crossing only one edge, but do not add checkerboard seams or repeat previews. No flowers, painted letters or numbers, logos, signage, aircraft, vehicles, people, props, stains shaped like symbols or text, watermark or signature. No real brands.
```

## Visual verification

- Exactly five equal square, perpendicular top-down material crops.
- All five requested labels are spelled correctly and sit only in the margins.
- Grass has no flowers; concrete and asphalt contain no paint, letters, numbers
  or text-like stains; sand contains no objects or footprints.
- Wet concrete is a darker, cooler variant of the dry concrete family without
  discrete puddles or mirror-like reflections.

## Seamlessness boundary

This generated board demonstrates the desired non-directional material character
but does not prove pixel-perfect edge continuity. Do not crop it directly into
runtime textures without a deterministic seamless-processing and edge-difference
test. The existing procedural runtime maps remain the shipping fallback.
