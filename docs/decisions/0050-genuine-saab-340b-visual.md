# 0050 — Genuine Saab 340B visual

Date: 2026-09-15

## Decision

AIR-007 is an original, unbranded Saab 340B-class regional turboprop at the
published 19.73 m length, 21.44 m standard wingspan (not the 22.75 m extended-tip
option) and 6.97 m height. Its project-owned procedural source emits a runtime
glTF/`.bin` kit and editable FBX. The silhouette prioritises a compact low wing,
relatively narrow circular fuselage, conventional empennage (fin + low horizontal
tail — not a T-tail), Dowty four-blade propellers (3.35 m diameter) and retractable
tricycle gear with twin-wheel mains that retract into the nacelles.

`AircraftType.Saab340` now dispatches to AIR-007 with its own pick volume, shadow,
selection marker and follow-camera multiplier. The Hangar type card uses a
transparent thumbnail rendered from the exact runtime glTF. The prior ATR
stand-in remains only as the missing-art primitive fallback; it is no longer the
normal presentation for Rex's two Saab 340Bs.

## Why this aircraft is next

ADR 0046 listed several approved future aircraft after AIR-005. ADR 0048 then
ADR 0049 closed the Dash 8-400 stand-in. The Saab 340B was the last live Adelaide
type still borrowing the ATR silhouette, so replacing it closes the remaining
truthfulness gap before any new aircraft family that is not yet part of the
simulation.

## Sources and rights

- External dimensions, standard-vs-extended wing, and propeller configuration:
  Saab, [Saab 340B product page](https://www.saab.com/products/saab-340); EASA TCDS
  EASA.A.068 (Saab SF340A/340B), issue 26.
- Propeller: Dowty R.354/4… family — four blades, 3.35 m (132 in) diameter per the
  cited TCDS.
- Undercarriage: retractable tricycle with twin-wheel nose and twin side-by-side
  mains retracting forward into the nacelles (Saab 340B / EASA.A.068 layout).
- The shipped mesh, editable source, thumbnail and fallback are project-owned;
  there is no copied model, texture, logo, Rex mark, Saab wordmark, real
  registration or commercial livery.

## Affected systems

- Aircraft art and StreamingAssets copies for AIR-007.
- Type-aware presentation dispatch, camera framing, selection and ground shadow.
- The Hangar aircraft catalogue model status and thumbnail.
- Asset/specification evidence and deterministic model-bound tests.

## Invariants and migration

Simulation still owns aircraft identity, phase, schedules, route timing and all
resource reservations. Presentation only selects a silhouette. No flight plan,
airline, stand, route, save field, save version or migration changes. Speeds,
ranges and bay reservations are unchanged.

## Acceptance evidence

- Emitted bounds are exactly 19.73 × 21.44 × 6.97 m and tyres touch local Y=0.
- 124 named meshes / 6,620 triangles; separate propeller, gear, door and flight
  control names retain the existing animation/pivot contracts.
- Dedicated 480 × 320 RGBA thumbnail rendered from the runtime glTF.
- Multi-angle review renders under `work/review/saab-340b-review-*.png`.
- Unity EditMode / domain harness run as available in this environment.
- No app build or manual packaged-player test requested; overview/follow and
  day/dusk/night packaged visual QA remains open.
