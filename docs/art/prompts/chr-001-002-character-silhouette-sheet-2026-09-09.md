# CHR-001 / CHR-002 character silhouette-sheet candidate — 2026-09-09

**Status:** Approved by Bailey 2026-09-09 · Integrated.

**Related runtime assets:** CHR-001 `mdl_ramp_crew_kit_v02` and CHR-002
`mdl_passenger_kit_v02`

**Visual authority:** Approved REF-003 and REF-005

## Output and provenance

- **Output:** `docs/art/reference/ref_airside_character_silhouette_kit_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1983×793 RGB
- **Prepared dimensions:** 2048×819 RGB
- **SHA-256:** `d4d46ccf5b98c00fdff539f54576ba39666e99f61c66d8e5f8e49fe2ce5933dc`
- **Input references:** REF-003 turnaround scale/readability and REF-005
  miniature proportions/material support
- **Edit chain:** one reference-guided generation; proportionally resized to
  2048 px wide; no in-painting
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** existing CHR-001/002 v02 runtime kits remain unchanged

## Exact prompt

```text
Use case: stylized-concept
Asset type: Airside character silhouette fidelity sheet and modelling reference for CHR-001 and CHR-002
Primary request: Create one clean character-kit catalogue sheet containing exactly nine full-body miniature figures at one consistent adult scale: three distinct ramp-service roles and six distinct civilian passengers. The ramp roles are (1) a ramp marshaller in high-visibility clothing holding two clearly visible marshalling wands, one in each hand; (2) a fueler in practical high-visibility apron workwear; and (3) a baggage handler in practical high-visibility apron workwear. The six civilians must be exactly two standing poses, two walking poses and two seated poses.
Input images: Image 1 is approved REF-003 and establishes Airside turnaround scale, overview readability, daylight and operational palette. Image 2 is approved REF-005 and establishes miniature character proportions, material simplicity and common scale.
Scene/backdrop: Pale neutral studio backdrop with one simple horizontal Concrete #9CA3A2 apron strip beneath all figures. No airport scenery, aircraft, vehicles, buildings, furniture beyond two plain low backless bench blocks required to support the seated poses, luggage or equipment.
Style/medium: Premium low-poly stylised-realism 3D character maquettes; simplified clean geometry, tapered arms and legs, softened faceted edges, subtle matte PBR, strong readable silhouettes. Adult proportions should be slightly simplified for an architectural miniature but not chibi, not doll-like, not bobble-headed and not photoreal.
Composition/framing: Landscape sheet, elevated three-quarter orthographic-like view with every figure fully visible and clearly separated. Arrange the three ramp roles together in the left portion and the six civilians in an orderly sequence across the rest of the sheet. Maintain identical ground scale and believable 1.65–1.90 metre adult height variation. Do not overlap figures. The seated figures must remain clearly readable as seated poses rather than shortened standing characters.
Ramp-role design: Dark navy or charcoal work trousers and boots, simple Safety Yellow #F2C14B high-visibility vest or jacket blocks with restrained reflective pale bands. Marshaller has two Safety Yellow/orange wands and a clear stable stance. Fueler and baggage handler must have visibly different neutral silhouettes through restrained garment, glove, cap or stance variation, without logos or written role labels.
Passenger design: Six varied but anonymous adults with restrained everyday clothing in Runway Ink #17242A, Tarmac #343B40, Concrete #9CA3A2, Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A and Cloud #EEF1EC. Exactly two stand neutrally, exactly two show readable mid-stride walking silhouettes, and exactly two sit naturally on separate plain low backless bench blocks. Vary height, body shape and clothing silhouette modestly while keeping one coherent kit style.
Faces and identity: Heads use minimal blank stylised facial planes with no detailed eyes, portraits, likenesses, identifiable people, expressions or photoreal skin detail. Hair is simplified geometric mass only.
Lighting/mood: Soft warm studio daylight with cool soft contact shadows, even exposure and no depth-of-field blur.
Text: No words, labels, captions, numbers, logos, clothing marks, signatures or watermark anywhere.
Constraints: Exactly nine people total: 3 ramp workers plus 6 civilian passengers. Exactly one marshaller, one fueler and one baggage handler. Exactly two standing, two walking and two seated civilian poses. All limbs tapered and anatomically connected. Overview-readable silhouettes; clear hands and paired wands for the marshaller. No children, mascots, airline uniforms, police/military clothing, branded PPE, logos, flags, luggage, crowds, duplicated figures, extra people, detailed faces, chibi proportions, giant heads, photoreal humans, black outlines or baked UI.
```

## Visual verification

- Exactly nine adult figures: three ramp roles and six civilians.
- Civilians split into two standing, two walking and two seated poses.
- Marshaller holds two visible wands; fueler and baggage-handler silhouettes
  remain distinct through clothing and stance.
- Tapered connected limbs, anonymous faceted faces and overview-readable forms.
- No clothing logos, role labels, identifiable people, extra figures, luggage,
  UI, watermark or signature.

This sheet is reference evidence for a future CHR-001/002 fidelity pass and
packaged-build comparison. It is not a runtime texture, mesh or approval decision.
