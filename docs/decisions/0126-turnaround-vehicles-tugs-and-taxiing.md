# 0126 — Turnaround vehicles on the field, fleet pushback tugs, and taxiing

Date: 27 September 2026. Author: Claude, at Bailey's request ("tugs and vehicles aren't
appearing at all", "improve taxiing").

## Context

The turnaround vehicles have been built and sequenced since ADR 0115/0116, but they never
appeared. `AirsideFocusMode.ShowGroundVehicles` and `ShowStandEquipment` are false on the default
bare Adelaide field (the ADR 0032/0041 clean-field rule), so every one of them was left null.
`UpdateGateServicing` then ran against the nulls every frame and drew nothing. Pushback tugs
had never been built for the fleet: `PUSHBACK_TUGS_PLAN.md` was still "planned", and the one
static tug was never moved.

## Decision — vehicles

- **New `AirsideFocusMode.ShowTurnaroundVehicles`, on in every build.** The turnaround is
  operation, not scenery. It covers the fuel, catering and baggage vehicles, the apron bus, stairs,
  chocks, the GPU cart and the pushback tugs. Landside cars, the ARFF truck and legacy props stay
  behind `ShowGroundVehicles`, so the clean-field rule still applies to scenery.
- **The player's turnaround** keeps its sequenced set (ADR 0115/0116). It now also places the
  chocks at the nose gear and the GPU cart by the nose; both were built but never placed.
- **AI aircraft on stand** get a pooled fuel truck and baggage train (up to 4 aircraft at once).
  Their windows come from `ApronServiceSchedule`: the vehicles arrive a few minutes after the
  aircraft parks, leave well before its pushback, and drive in from and back to a park point
  clear of the wing.
- **Fleet pushback tugs** (`PushbackTugTimeline`, pure, and `AirsidePrototype.PushbackTugs.cs`)
  work every tail-first pushback, AI included, from a pool of 8 VEH-004 tugs (scaled 1.3× for
  widebodies). Each tug:
  - reverses onto the nose gear 150–60 s before the booked departure;
  - stays on the towbar through any hold and through the push;
  - unhooks and drives clear during the simulation's 25 s disconnect pause — ahead, then round
    to the side the aircraft will not turn, a half-span plus 8 m out;
  - leaves as the aircraft taxis.

  The towbar eye is placed on the drawn model's own nose gear, measured once per type from the
  `Gear nose` renderer. The side the towbar is on is measured off the tug kit. Presentation
  only: push and taxi timing do not change.

## Consequences

- Vehicles now appear on the default field. The Mac soak must confirm the 8 tugs plus the
  4 AI service sets don't cost the 60 fps budget.
- `PresentationLayoutTests.FocusMode_DefaultLaunchKeepsTheFocusedCircuit` now also pins
  `ShowTurnaroundVehicles` on. `PushbackTugTests` covers the tug phases, the towbar contact,
  continuity, wing clearance, the side choice and the AI service windows.
- Tugs are not yet a scarce simulated resource (plan item 7). That would change timing and
  saves.
