# BLD-001…003 building fidelity-board candidate — 2026-09-09

**Status:** Generated; Bailey review required. Reference only; not integrated.

**Related runtime assets:** BLD-001 `mdl_terminal_regional_small_v05`, BLD-002
`mdl_hangar_small_v05`, BLD-003 `mdl_operations_shed_v05`

**Visual authority:** Approved REF-001, supported by REF-005 for common scale

## Output and provenance

- **Output:** `docs/art/candidates/ref_regional_airport_building_fidelity_board_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1942×809 RGB
- **Prepared dimensions:** 2048×853 RGB
- **SHA-256:** `e83ec899575b7e582f9f5bf7c9b12a1caf097e3f61b662fab27839f9466c364e`
- **Input references:** REF-001 primary architectural/daylight authority and
  REF-005 common miniature-scale/material support
- **Edit chain:** one reference-guided generation, followed by one targeted
  corrective edit to give the hangar a true two-plane gable roof and attached
  lean-to office; proportionally resized to 2048 px wide
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** existing BLD-001/002/003 v05 runtime meshes remain unchanged

## Exact generation prompt

```text
Use case: stylized-concept
Asset type: Airside game-building fidelity board and shared modelling reference for BLD-001, BLD-002 and BLD-003
Primary request: Create one side-by-side fidelity board showing exactly three separate small Australian regional-airport buildings at the same architectural-miniature scale: (1) a small regional terminal with a glazed airside front, shallow pitched roof, sheltered canopy and attached service wing; (2) a corrugated-metal hangar with a dual-pitch roof, large closed sliding-door panels and a small lean-to office; (3) a compact operations and crew shed with a small porch, roof antenna and externally mounted AC boxes.
Input images: Image 1 is approved REF-001 and is the primary authority for Kingscote airport architecture, proportions, daylight, materials and visual character. Image 2 is approved REF-005 and supports consistent miniature scale, simplified material blocks and palette only.
Scene/backdrop: Neutral warm-grey studio ground plane and pale neutral backdrop. Each building stands alone on a minimal thin base with generous clear separation. No surrounding airport diorama, landscaping, roads, fences or operational clutter.
Style/medium: Premium stylised-realism architectural miniature; clean simplified low-poly geometry, softened bevelled edges, subtle PBR, restrained corrugated-metal and glass response. Match REF-001 rather than inventing a different architectural style.
Composition/framing: Landscape board, daylight elevated three-quarter overview with approximately 48-degree field-of-view character. Arrange exactly three buildings in one horizontal row, all facing the same airside viewing direction, all at the same scale and comparable camera distance. Show each complete silhouette without overlap or cropping. The terminal is naturally wider than the hangar and shed, but doors, windows and storey heights must share a believable common human scale.
Lighting/mood: Warm clear daylight with cool soft shadows, restrained ambient occlusion, no dramatic haze, no tilt-shift blur and no depth-of-field blur.
Color palette: Runway Ink #17242A, Tarmac #343B40, Concrete #9CA3A2, Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A, Safety Yellow #F2C14B, Open Sky #A7C9D9, plus restrained off-white and weathered corrugated metal from REF-001.
Materials/textures: Broad readable blocks of concrete, muted corrugated metal, dark blue-grey transparent glass, roof metal, canopy structure and small functional equipment. Emphasise silhouette, roof form, glazing rhythm and large material zones over tiny detail.
Text: No text, signage, labels, captions, numbering, logos, trademarks, signatures or watermark anywhere.
Constraints: Exactly three buildings only. Terminal must visibly include glazed airside frontage, shallow pitched roof, canopy and service wing. Hangar must visibly include dual-pitch corrugated roof, large sliding doors and lean-to office. Ops shed must visibly include porch, antenna and AC boxes. Glass may show simple reflections and interior darkness only—no fake people, silhouettes, portraits, furniture scenes or readable signage behind glass. No aircraft, vehicles, people, runway, taxiway, UI or extra structures. No real airline or airport branding. Avoid toy-like proportions, excessive greebles, tiny unreadable detail, futuristic architecture, huge international-terminal scale, tropical styling and photoreal grime.
```

## Exact corrective-edit prompt

```text
Edit the generated three-building fidelity board and preserve everything except the hangar corrections below. Keep the exact three-building side-by-side layout, neutral studio setting, camera, daylight, common miniature scale, terminal design on the left, and ops/crew shed design on the right unchanged. Change only the centre hangar: replace its hipped roof with a true simple dual-pitch gable roof made of exactly two long corrugated roof planes meeting at one straight ridge, with vertical triangular gable ends and no sloping roof planes at the short ends. Add one clearly visible small attached lean-to office along the hangar's right-hand side, set lower than the hangar, with a single-slope corrugated roof, one plain door and one dark window. Keep the large closed sliding-door panels clearly visible on the front. Maintain broad simple material blocks and a strong readable silhouette; no extra freestanding building. Preserve exactly three principal buildings. No people or silhouettes behind glass, no signage or text, no labels, logos, watermark, vehicles, aircraft, roads, runway, landscaping additions or UI.
```

## Visual verification

- Exactly three separated buildings at a consistent architectural scale.
- Terminal: glazed airside front, shallow roof, canopy and service wing.
- Hangar: two-plane gable roof, closed sliding doors and attached lean-to office.
- Ops shed: porch, roof antenna and external AC boxes.
- No fake people or silhouettes behind glass; no readable signage, labels,
  logos, aircraft, vehicles, UI or watermark.

This board is reference evidence for a future BLD-001…003 fidelity pass and
packaged-build comparison. It is not a runtime texture, mesh or approval decision.
