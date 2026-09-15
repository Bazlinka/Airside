# 0049 — Genuine Dash 8-400 visual

Date: 2026-09-15

## Decision

AIR-006 is an original, unbranded Dash 8-400-class regional turboprop at the
published 32.83 m length, 28.42 m wingspan and 8.34 m height. Its project-owned
procedural source emits a runtime glTF/`.bin` kit and editable FBX. The silhouette
prioritises a long high-wing fuselage, two long nacelles, swept six-blade
propellers, nacelle-mounted main landing gear and a tall T-tail.

`AircraftType.Dash8Q400` now dispatches to AIR-006 with its own pick volume,
shadow, selection marker and follow-camera multiplier. The Hangar type card uses
a transparent thumbnail rendered from the exact runtime glTF. The prior ATR
stand-in remains only as the missing-art primitive fallback; it is no longer the
normal presentation for QantasLink's Dash 8-400.

## Why this aircraft is next

ADR 0046 listed several approved future aircraft after AIR-005. ADR 0048 then
identified the Dash 8-400 as milestone 3 because an aircraft of this type already
operates in the live Adelaide schedule while still using an explicitly labelled
ATR stand-in. Replacing a visible stand-in closes a current truthfulness gap before
adding another aircraft family that is not yet part of the simulation.

## Sources and rights

- External dimensions, 82-seat baseline, performance and propeller configuration:
  De Havilland Canada, [Dash 8-400 specification sheet](https://dehavilland.com/wp-content/uploads/2026/07/DHC_Dash8_Spec-Sheet_v11_Digital.pdf).
- The generated image is retained only as a reviewed modelling candidate under
  the OpenAI service terms; its exact prompt and hash are recorded in
  `docs/art/prompts/air-006-dash8-q400-reference-generation-2026-09-15.md`.
- The shipped mesh, editable source, thumbnail and fallback are project-owned;
  there is no copied model, texture, logo, registration or commercial livery.

## Affected systems

- Aircraft art and StreamingAssets copies for AIR-006.
- Type-aware presentation dispatch, camera framing, selection and ground shadow.
- The Hangar aircraft catalogue model status and thumbnail.
- Asset/specification evidence and deterministic model-bound tests.

## Invariants and migration

Simulation still owns aircraft identity, phase, schedules, route timing and all
resource reservations. Presentation only selects a silhouette. No flight plan,
airline, stand, route, save field, save version or migration changes.

## Acceptance evidence

- Emitted bounds are exactly 32.83 × 28.42 × 8.34 m and tyres touch local Y=0.
- Visual revision (2026-09-15): 174 named meshes / 18,856 triangles (was 139 /
  6,728). Same asset path and animation/pivot part-name contracts; silhouette
  now uses fitted cabin glazing, a pitched flight deck, continuous nacelles,
  tip fences and a joined T-tail.
- Dedicated 480 × 320 RGBA thumbnail rendered from the runtime glTF.
- First integration: Unity 6.3 EditMode 328/328. Visual revision: domain
  pre-check + offline mesh review; Unity EditMode / packaged overview-follow QA
  still open (no Mac build that session).

## Known limit found after merge (2026-09-15)

Parked at the regional bays as drawn (model root on the stop), AIR-006 on 50D or 50E next to
a turboprop on the other has 3.3–3.5 m between outlines, below ICAO code C's 4.5 m; two
Dash 8-400s there would be 1.2 m apart. All other bay pairings keep at least 4.5 m.
`Layout_ParkedRegionalAircraftKeepCodeCClearance` pins this so any new shortfall fails. A
stand-assignment rule is a separate decision: barring the Q400 from 50D/50E outright can
leave an aircraft without a bay overnight with six aircraft on six bays.

## Visual quality pass (2026-09-15)

Second in-place silhouette pass on the same AIR-006 path and envelope:

- Continuous high-wing / fuselage saddle (centre wing box + outboard fairings) instead of a stepped root valley
- Single aerodynamic nacelle loft with intake lip, gear-bay belly, exhaust taper and wing fillet
- Framed four-pane flight deck with sill/brow fitted to the nose (no dark mask slab)
- Pitched six-blade props with clear hubs/spinners and Hangar-readable tip stripes
- Longer nacelle-mounted mains with thicker oleos and open doors
- Soft dorsal fin-root fillet and rounded T-tail saddle
- Even cabin window pitch on the curved skin

Evidence: generator validate (exact 32.83 × 28.42 × 8.34 m, tyres on y=0);
182 named meshes / 26752 triangles; Hangar thumbnail regenerated; StreamingAssets
synced; orthographic front/side/top and game-camera reviews under `work/review/`.
Unity EditMode re-run deferred to Mac (`scripts/test-unity.sh` — no Unity binary in
this environment). Prior Mac verification on this branch was 331/331.
