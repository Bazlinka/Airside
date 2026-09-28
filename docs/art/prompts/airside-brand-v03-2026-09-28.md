# Airside v03 brand redesign — 2026-09-28

**Assets:** BRD-004 wordmark, BRD-005 app icon, BRD-006 launch mark
**Status:** Integrated candidate at Bailey's request; Unity/Dock verification pending.

## Process

OpenAI built-in image generation was used for concept exploration only. The first two route-network
concepts were rejected because they read as a turbine and a generic junction. The selected direction
was then rebuilt as clean, deterministic project-owned artwork by
`scripts/generate-airside-brand-v03.py`; no generated bitmap is shipped.

## Selected concept prompt

```text
Use case: logo-brand
Asset type: master symbol concept for AIRSIDE, a premium airport and airline-management strategy game
Primary request: Create a compact original AS monogram where the A and S are fused into one clever geometric symbol. Use negative space and one sweeping flight-route cut to make it feel like air-traffic control, route planning and forward movement. The mark should be instantly recognisable, confident and premium, with the restraint of a top-tier strategy-game studio identity. It must be a genuinely new design, not the old arch-and-runway A.
Style/medium: flat vector logo; bold simple geometry; exact clean edges; subtle custom cuts; no illustration
Composition/framing: one centered near-square symbol; strong silhouette; balanced negative space; readable at 24 px; generous transparent margin
Color palette: bright aqua #3FD0C9 main shape, Cloud #EEF1EC secondary cut or shape, exactly one small warm amber #FFB547 control-point accent; graphite #17242A only if required for negative separation
Constraints: genuine transparent background; symbol only; no full wordmark; no literal aircraft; no wings; no propeller; no turbine; no radial spokes; no compass; no runway perspective; no airport tower; no globe; no map pin; no badge enclosure; no gradient; no shadow; no 3D; no mockup; no fine detail; no watermark; original design only
Avoid: generic tech-company infinity mark, sports logo, pinwheel, route junction with four spokes, old letter-A runway logo
```

## Production outputs

- `docs/art/source/airside_{wordmark_light,app_icon,brand_mark}_v03.svg`
- `docs/art/candidates/airside_{wordmark_light,app_icon,brand_mark}_v03.png`
- `game/Airside/Assets/Airside/Art/Brand/airside_{wordmark_light,app_icon,brand_mark}_v03.png`
- matching packaged copies under `StreamingAssets/Airside/Art/Brand/`

The final PNGs use Avenir Next Bold from macOS, exact AIRSIDE spelling, real alpha for the mark and
wordmark, and a rounded Runway Ink field for the app icon. The generator is the source of truth.
