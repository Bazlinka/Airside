# Brand and splash candidate generation — 2026-09-07

**Status:** Generated; Bailey review required. Not approved or integrated.  
**Assets:** BRD-001 and UI-ILL-001  
**Visual authority:** Approved REF-001, REF-002 and REF-005

## BRD-001 — light wordmark

- **Output:** `docs/art/candidates/airside_wordmark_light_v01.png`
- **Generator/source:** project-owned deterministic Pillow script,
  `scripts/generate-airside-wordmark.py`
- **Dimensions:** 2048×512 RGBA, transparent background
- **SHA-256:** `4a0bab20b2eed64858f720ab972da75d0b2a191221ff539084d9aced7f9855bc`
- **Edit chain:** direct scripted render; no third-party image input
- **Cost:** none
- **Attribution:** none
- **Fallback:** Unity-rendered `AIRSIDE` text or no wordmark

Design intent: exact `AIRSIDE` spelling in a calm wayfinding wordmark. The mark is
an abstract A containing a runway centreline. Cloud text, Coastal Blue mark and
Safety Yellow centreline use the approved palette. It has no tagline or baked
legal copy.

## UI-ILL-001 — dawn splash

- **Output:** `docs/art/candidates/ui_splash_airport_dawn_v01.png`
- **Generator:** OpenAI built-in image generation
- **Generated dimensions:** 1672×941 RGB
- **Prepared dimensions:** 3840×2160 RGB via proportional image resize
- **SHA-256:** `d70ae9de23b2decd1f7891a91ddb8308ba22c895a421fe2402b042ce75178fb8`
- **Input references:** REF-001 daytime composition, REF-002 dusk lighting,
  REF-005 asset scale/material/palette
- **Edit chain:** one reference-guided generation; resized to the manifest's exact
  3840×2160 delivery dimensions; no in-painting
- **Cost:** no per-asset cost surfaced by the built-in generator
- **Attribution:** none known
- **Fallback:** the existing live Unity airport camera behind the title UI

### Exact prompt

```text
Use case: stylized-concept
Asset type: 16:9 dawn splash illustration candidate for the Airside Unity airport-management game
Primary request: Create a premium stylised-realism architectural miniature of the same fictional Australian regional airport shown in the references, at quiet dawn just before the first operational wave. Preserve the airport's established visual language: one runway, curved taxiway, compact concrete apron, small glass-and-corrugated-metal terminal, hangar, windsock, restrained eucalyptus and dry-green coastal landscape, fictional twin turboprops and small ground-service vehicles. The image should feel calm, capable and quietly anticipatory—not dramatic.
Input images: Image 1 is the authoritative daytime composition and asset-design reference; Image 2 is the authoritative dusk lighting and airfield-lighting reference; Image 3 is the authoritative asset scale, silhouette, material and palette reference.
Scene/backdrop: coastal Kangaroo Island regional airport at blue-gold dawn, low warm sun at the horizon, cool soft shadows, subtle apron lights still visible, large open sky.
Style/medium: polished 3D-game key art, premium stylised realism, clean simplified geometry, softened edges, subtle PBR material response; visibly a game-world miniature rather than a real photograph.
Composition/framing: wide 16:9 desktop splash composition; elevated three-quarter camera close to the approved default view; airport activity concentrated mainly in the right and lower-right half; preserve broad quiet negative space across the upper-left and left-centre for Unity-rendered title and controls; no UI panels in the image.
Lighting/mood: calm blue-gold dawn, warm natural rim light, cool soft shadows, readable runway and apron, no orange fog wash.
Color palette: Runway Ink #17242A, Tarmac #343B40, Concrete #9CA3A2, Eucalyptus #4F6F60, Dry Grass #8A8A58, Sand #C8B286, Coastal Blue #39708A, Safety Yellow #F2C14B, Cloud #EEF1EC, Open Sky #A7C9D9.
Constraints: match the approved aircraft, vehicle, building and airfield proportions from the references; believable service positions; no text anywhere; no title; no UI; no real airline logos; fictional abstract livery blocks only; no trademarks; no watermark; no signature.
Avoid: photoreal documentary look, toy-city cartoon, saturated teal-orange grading, giant terminal, jet airliners, airport crowds, illegible signage, baked interface elements.
```

## Review checks

- BRD-001 spelling is exact and the background has real alpha.
- UI-ILL-001 is 3840×2160, contains no visible text or UI, and follows the
  approved airfield layout, aircraft family and dawn/dusk lighting language.
- Both files use repository naming and remain under `docs/art/candidates/` until
  Bailey explicitly approves promotion.
