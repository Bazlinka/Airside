# WLD-004 context terrain style-sheet candidate — 2026-09-09

**Status:** Approved by Bailey 2026-09-09 · Integrated.

**Related runtime asset:** WLD-004 `mdl_kingscote_context_terrain_v01`

**Visual authority:** Approved REF-001 and REF-005; VEG-002 scrub candidate used only for vegetation consistency

## Output and provenance

- **Output:** `docs/art/reference/ref_kingscote_context_terrain_catalogue_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1680×945 RGB
- **Prepared dimensions:** 2048×1152 RGB
- **SHA-256:** `5bbfe437e38732c062648ebb23b3099afd9ae809dc3ad55cce8f547a4f9d929e`
- **Input references:** REF-001 airport landscape/daylight, REF-005 asset
  proportions/material/palette, and the VEG-002 coastal scrub candidate
- **Edit chain:** one reference-guided generation, followed by one targeted
  corrective edit to simplify terrain and water and replace a cliff with a low
  hill; proportionally resized to 2048×1152
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** existing WLD-004 runtime geometry and coastal material maps remain unchanged

## Exact generation prompt

```text
Use case: stylized-concept
Asset type: Airside game-environment modelling reference and catalogue sheet for WLD-004 context terrain accents
Primary request: Produce one premium low-poly stylised-realism terrain accent kit for the context surrounding a small Australian regional airport with a Kingscote / Kangaroo Island character. Show clearly separated modular pieces: soft paddock berms, distant low hills, coastal dunes, shallow turquoise-water shallows, and deeper blue-water pieces. Include dry-grass, eucalyptus-scrub, sand and water material zones. These are environmental context pieces only, never runway, taxiway or apron geometry.
Input images: Image 1 is the approved airport overview and establishes the coastal regional-airport proportions, palette and warm daylight. Image 2 is the approved asset scale and palette reference. Image 3 is the companion coastal-scrub candidate and establishes vegetation shapes and materials only.
Scene/backdrop: Clean neutral warm-grey studio ground and backdrop. Arrange the terrain modules as an orderly catalogue sheet with generous clear separation and unambiguous silhouettes. In the middle, leave a large perfectly flat blank rectangular concrete/apron-coloured placeholder, entirely empty, to demonstrate the protected operational-airfield zone. Surround it with separated context modules without connecting them into a real coastline or map.
Style/medium: Premium stylised-realism architectural miniature rendered as clean low-poly 3D concept art; simplified faceted geometry, softened bevelled edges, subtle PBR, soft readable silhouettes; credible Australian coastal terrain rather than fantasy scenery.
Composition/framing: Elevated three-quarter orthographic-like catalogue view, approximately 48 degree field-of-view visual character, landscape sheet. Every module fully visible with ground contact and space around it. The empty central operational placeholder must remain visually dominant and unmistakably flat.
Lighting/mood: Warm soft studio daylight with cool soft shadows, restrained ambient occlusion, clear material separation, no dramatic haze or depth-of-field blur.
Color palette: Runway Ink #17242A, Tarmac #343B40, Concrete #9CA3A2, Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A, shallow turquoise derived from Coastal Blue, Open Sky #A7C9D9.
Materials/textures: Matte dry grass, sparse eucalyptus scrub, pale sand, translucent-looking shallow turquoise water, deeper coastal-blue water, subdued concrete placeholder. No runway markings or operational details.
Text: No text, labels, captions, numbering, logos, signatures or watermark anywhere in the image.
Constraints: Context only. Keep the operational airfield blank in the middle as one flat concrete/apron placeholder. No runway, taxiway, parking stand, terminal, aircraft, vehicles, people, roads, fences or UI. No real map coastline. No roads cutting operational areas. Do not make a single connected diorama; preserve catalogue separation between modular pieces. No photoreal terrain, tropical vegetation, steep mountains, cliffs, flowers or fantasy forms. No real brands or trademarks.
```

## Exact corrective-edit prompt

```text
Edit the generated terrain catalogue sheet while preserving its overall camera, orderly modular catalogue layout, generous separation, large empty flat concrete placeholder in the centre, and the same inventory categories. Change only the terrain rendering language: make every land, dune, shallow-water and deep-water module clearly low-poly premium stylised-realism with simplified faceted geometry, broad clean material planes, restrained subtle PBR and softened silhouettes. Remove the steep eroded cliff formation at upper right and replace it with a low gently rolling distant-hill module. Simplify the water into broad calm coastal-blue and turquoise planes with sparse stylised low-poly ripples; remove photoreal wave foam, photographic seabed detail and dramatic surf. Keep eucalyptus scrub sparse, low, rounded and consistent with the companion scrub reference. Keep all operational content absent: no runway, taxiway, markings, roads, buildings, aircraft, vehicles, people, text, labels, logos, watermark or map-like coastline. The central concrete placeholder must stay entirely blank, flat and visually dominant.
```

## Visual verification

- Clearly separated paddock, hill, dune, shallow-water and deep-water modules.
- Large central concrete placeholder remains flat, blank and visually dominant.
- Soft faceted low-poly terrain and water language; no steep cliffs or
  photoreal surf.
- No runway, taxiway, markings, roads, buildings, vehicles, aircraft, people,
  labels, logos, UI, watermark or real-map coastline.

This image is the Approved modelling target for the WLD-004 fidelity pass. It is not
a runtime texture, terrain mesh, material set or approval decision.
