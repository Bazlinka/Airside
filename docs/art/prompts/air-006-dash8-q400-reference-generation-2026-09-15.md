# AIR-006 Dash 8-400-class reference generation — 2026-09-15

## Purpose

Create one visual modelling candidate for AIR-006 before authoring the actual
Unity runtime mesh. The image is evidence/reference only: it is not used as a
texture, sprite or substitute for the required 3D aircraft.

## Generator and output

- Generator: OpenAI built-in image generation (model identifier not exposed)
- Generations: one; no edit chain
- Output: `docs/art/candidates/ref_air_dash8_q400_candidate_v01.png`
- Dimensions: 1536 × 1024 PNG with alpha channel
- SHA-256: `2ba00c7e818f7e281256397d3ff605166ff432191d20a36e31b9610d1159c5fd`
- Cost: no separate asset cost recorded

## Exact prompt

> Use case: stylized-concept
> Asset type: 3D modelling reference for the Airside Unity airport-management game
> Primary request: Create an original, unbranded three-quarter orthographic-style studio render of a Dash 8-400-class regional turboprop aircraft, suitable as a modelling reference rather than a finished game sprite.
> Subject and proportions: a long, slim 82-seat high-wing twin-turboprop with a 32.83 m fuselage length, 28.42 m wingspan and 8.34 m overall height; two large under-wing nacelles; swept six-blade propellers; tall T-tail; pointed regional-airliner nose; long main landing gear retracting into the engine nacelles; credible cabin windows, forward and rear doors, control surfaces and tricycle landing gear.
> Composition: elevated three-quarter front-left view, entire aircraft visible with generous padding, propellers stationary and clearly readable, clean neutral studio background or transparent background.
> Style: premium stylised realism, clean simplified engineering forms, coherent production-ready modelling guide, believable mechanical construction, no photoreal scene.
> Palette: warm off-white fuselage, dark cockpit glass, muted charcoal nacelles and tyres, fictional deep coastal-blue tail with one restrained ochre accent.
> Text: none.
> Constraints: no airline or manufacturer branding, no airline names, no registration, no logos, no badges, no watermarks, no real-world livery, no people or occupants, no airport vehicles or ground equipment; do not crop any wingtip, tail, propeller or wheel; this is reference artwork only and must not resemble a flat 2D game sprite.

## Review

Accepted as a modelling candidate. It clearly shows the long high-wing airframe,
large six-blade propellers, elongated nacelles, nacelle-mounted main gear and
tall T-tail. No logo, airline name, manufacturer mark, registration, occupant or
watermark was observed. The runtime model is separately generated project-owned
geometry and retains a true-scale primitive fallback.
