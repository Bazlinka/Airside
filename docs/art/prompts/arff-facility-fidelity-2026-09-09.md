# Regional ARFF facility fidelity candidate — 2026-09-09

**Status:** Generated; Bailey review required. Reference only; not integrated.

**Related runtime assets:** `mdl_arff_truck_v01` and `mdl_arff_shed_v01`

**Visual authority:** Approved REF-001 and REF-005

## Output and provenance

- **Output:** `docs/art/candidates/ref_regional_arff_facility_fidelity_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1672×941 RGB
- **Prepared dimensions:** 2048×1152 RGB
- **SHA-256:** `9d544aecf297072efdba472f94bcdc6307fc81af16cf64f174d1a491e46374fa`
- **Input references:** REF-001 architecture/hangar/camera/daylight and REF-005
  scale/material/palette support
- **Edit chain:** one reference-guided generation, followed by one targeted
  corrective edit removing a recognisable grille badge and shifting the render
  from photographic detail to Airside's faceted miniature language;
  proportionally resized to 2048×1152
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** existing ARFF truck and shed runtime prefabs remain unchanged

## Exact generation prompt

```text
Use case: stylized-concept
Asset type: Airside ARFF truck-and-rescue-shed fidelity reference for `mdl_arff_truck_v01` and `mdl_arff_shed_v01`
Primary request: Create one premium stylised-realism architectural-miniature scene of a small regional-airport fire and rescue facility. Show exactly one compact airport rescue/firefighting truck in restrained red and white, parked squarely on a small concrete forecourt immediately outside one modest corrugated-metal rescue shed with one fully open vehicle bay. A larger regional-airport hangar sits well behind them only as a believable scale reference.
Input images: Image 1 is approved REF-001 and is the primary authority for Kingscote regional-airport architecture, hangar scale, elevated miniature camera, daylight and material language. Image 2 is approved REF-005 and supports vehicle proportions, simplified material blocks and palette.
Truck design: A credible compact regional ARFF appliance, approximately medium rigid-truck size rather than a huge international-airport crash tender. Functional cab, dark glazing with no visible occupants, robust chassis, realistic wheel diameter, enclosed rear rescue body with equipment-compartment seams, modest roof water cannon, small restrained emergency lightbar, mirrors and bumpers. Body uses deep practical red with off-white roof and upper panels. Use Safety Yellow #F2C14B only for narrow operational chevrons, step edges, grab points and small conspicuity accents—never as a large decorative body colour. No toy proportions, giant wheels, oversized cannon or cartoon face-like grille.
Rescue shed: One compact practical single-storey shed built from muted pale-grey corrugated metal, a simple shallow dual-pitch gable roof, one wide open bay sized correctly for the truck, dark uncluttered interior, plain side personnel door and one small window. No second bay, station tower, urban fire station styling or oversized civic architecture. Keep the open bay clearly readable behind the parked truck without the truck hiding the entire opening.
Background scale cue: One ordinary corrugated regional-airport hangar from the REF-001 visual language, placed farther behind and visibly larger than the rescue shed. Keep it subdued and slightly lower contrast, with no aircraft or vehicles around it. The hangar is context, not a second hero subject.
Scene/backdrop: Simple concrete ARFF forecourt transitioning to restrained dry-green airfield grass. Sparse low Kingscote scrub only at distant edges. No runway, active apron stand, roads, terminal, aircraft or unrelated equipment.
Style/medium: Premium low-poly stylised realism; clean simplified geometry, softened bevelled edges, restrained subtle PBR, broad readable red/white/corrugated-metal material blocks. Credible operational miniature rather than a die-cast toy or photoreal emergency photograph.
Composition/framing: Landscape, elevated three-quarter overview with approximately 48-degree field-of-view character. Truck and rescue shed dominate the foreground/midground; show the truck's front, side and roof equipment, the complete shed silhouette and open bay. Hangar remains clearly visible in the background for scale. Nothing cropped and no tilt-shift blur.
Lighting/mood: Warm clear regional daylight with cool soft shadows, restrained ambient occlusion and even overview readability. No dramatic emergency lighting, smoke, fire, haze, rain or depth-of-field blur.
Color palette: Runway Ink #17242A, Tarmac #343B40, Concrete #9CA3A2, Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A, Safety Yellow #F2C14B, Open Sky #A7C9D9, Cloud #EEF1EC, plus restrained deep emergency red.
Text: No words, letters, numbers, station name, emergency-service name, vehicle markings, registration plates, logos, crests, flags, badges, trademarks, signatures or watermark anywhere.
Constraints: Exactly one rescue truck, exactly one rescue shed with one open bay and exactly one background hangar. No real fire-service branding or resemblance to a specific agency livery. No people or occupants. Safety Yellow only on operational details. No additional emergency vehicles, ambulances, police vehicles, hoses laid across the forecourt, traffic cones, readable signs, UI or decorative props. Avoid toy-like/chibi proportions, glossy plastic finish, oversized airport crash-tender scale, urban fire-station architecture and excessive micro-detail.
```

## Exact corrective-edit prompt

```text
Edit the generated ARFF facility image while preserving the exact composition, elevated three-quarter camera, one compact rescue truck, one open-bay corrugated rescue shed, one larger background hangar, relative scale, coastal-airfield setting and daylight. Change only the branding and rendering language. Remove the circular three-point grille emblem and every other manufacturer badge, crest, word, number, registration plate, service mark or logo from the truck and buildings; replace the grille centre with a completely plain unbranded dark geometric panel. Make the truck clearly fictional with a neutral original cab fascia that does not resemble any identifiable vehicle manufacturer. Simplify the entire scene into premium low-poly stylised-realism architectural-miniature art: cleaner faceted geometry, softened bevelled edges, broad material blocks, restrained subtle PBR, slightly simplified vegetation and ground texture, and less photographic micro-detail. Keep the truck credible and non-toy-like, with realistic medium rigid-truck proportions, deep red and off-white panels, dark empty cab glass, modest roof cannon and small lightbar. Keep Safety Yellow only on narrow steps, rails, bollards and operational chevrons. Do not add or remove objects. No people, occupants, text, branding, smoke, fire, extra vehicles, UI, watermark or signature.
```

## Visual verification

- Exactly one compact truck, one open-bay rescue shed and one larger background
  hangar, with believable relative scale.
- Truck has a plain fictional grille, no manufacturer badge, no real-service
  livery and no visible occupants.
- Red/white body remains dominant; Safety Yellow is limited to operational
  steps, rails, bollards and chevrons.
- Faceted miniature treatment, readable open bay and no extra vehicles, people,
  text, logos, UI, smoke, watermark or signature.

This image is reference evidence for a future ARFF asset fidelity pass and
packaged-build comparison. It is not a runtime texture, mesh or approval decision.
