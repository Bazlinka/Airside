# 0096 — Aircraft visual fidelity pass

Date: 22 September 2026.

## Decision

Three presentation-only aircraft defects are fixed, and the propeller/turbofan blur disc
becomes readable at power:

1. **Tyres roll at the real ground speed.** The tyre roll took its speed from
   `AirsideFlightPath.GroundSpeedMetresPerSecond`, which reports a non-zero speed only
   during the takeoff roll and the landing rollout. Every other ground phase returned zero,
   so aircraft slid around the whole Adelaide taxi network on stationary wheels. The roll
   now takes the authored ground-leg pose speed whenever the aircraft is on one, and falls
   back to the circuit speed only for the demo circuit path.
2. **Pushback wheels turn the correct way.** The pushback leg is drawn tail-first, so its
   wheels now counter-rotate instead of rolling forwards while the aircraft moves backwards.
3. **The nose landing gear is no longer painted in the airline livery.** The fuselage
   livery filter accepted any part whose name merely contained `nose`, which also matched
   the nose gear's own `tire_nose_*`, `wheel_nose_*`, `rim_nose_*`, `gear_oleo_nose`,
   `gear_scissors_nose`, `gear_door_nose` and `gear_nose` parts. All 23 shipped aircraft
   models were affected, between 1 and 12 parts each.
4. **The blur disc reads as a propeller.** Individual blades switch off once the blur is
   established, so at takeoff power the disc is the whole propeller. At its previous 0.11
   peak alpha it was close to invisible and a turboprop at full power read as having no
   propellers. Peak alpha is now 0.30 for propellers and 0.26 for turbofan intakes, still
   translucent enough to see the nacelle and far wing through the disc.

## Rationale

Items 1–3 are defects rather than art direction: the aircraft were drawn in a state the
simulation never described. Wheels are the part of an airframe a player watches most
closely on a follow camera during taxi, and livery-painted nose wheels are visible from any
close view. Item 4 is a tuning change, made because the previous value produced an absence
rather than a restrained effect.

## Affected systems

Presentation only. Simulation timing, ground reservations, save data and schedules are
untouched — the tyre roll now *reads* the ground pose that already drove the aircraft's
position, rather than deriving a second, contradictory speed of its own.

The two pure decisions live in `AirsideAircraftParts` (`TireRollMetresPerSecond`,
`TakesFuselageLivery`), which holds no UnityEngine types, so both are covered by the
headless harness alongside the existing part-name contract.

## Evidence

`scripts/test-domain.sh` covers the new rules. The livery filter was additionally
diffed against the authored node names of all 23 runtime `.gltf` models: the change removes
only landing-gear parts and adds no part that was not painted before.

`scripts/test-unity.sh` was attempted and could not run on this Mac: it hangs in the
batchmode licensing reconnect loop already documented on
`docs/build-mac-batchmode-dead-end`. Since the headless harness does not compile
`AirsidePrototype*.cs`, the Unity compile of this branch is unverified and needs an Editor
GUI run before merge. Packaged day/dusk/night visual QA is also outstanding — the disc alpha
in particular is a look change that needs Bailey's eyes on a packaged build.
