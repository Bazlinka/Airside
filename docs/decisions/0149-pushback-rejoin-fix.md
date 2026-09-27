# 0149 — Pushback rejoin skips route hooks; apron tests follow ground control

Date: 28 September 2026. Author: Claude, after running the headless suite on `main`. The Round 2
merge (PR #419) was only type-checked, and the suite then showed 3 failures.

## Context

- **Reversing after the push.** From gates 15, 16R and 21, the baked runway 23 route starts with
  a hook: west along the taxilane, then a U-turn back east onto the parallel lane. The
  taxilane-hint path in `PushbackGeometry` (ADR 0146) prepended a synthetic 20 m start to that
  route. The rejoin search then landed on the synthetic start, so the aircraft taxied past the hook
  and drove the hook itself.
  - Aircraft stopped and reversed on the spot, turning the nose at 720 °/s. From gate 21 the A220
    crawled at 0.5 m/s.
  - `GroundMotionSmoothnessTests` caught this.
- **Apron order.** On the regional bays the new pushes end nose-west on the same taxilane.
  - Bay 2 pushes out about 40 m behind bay 1's aircraft and would catch it up. Ground control
    (`GroundTraffic.PathClear`) now holds bay 2, and bay 3 goes second.
  - That is correct behaviour. Two tests had assumed bays 1 and 2 always push first.

## Decision

- `PushbackGeometry` builds the push and taxi from an explicit taxilane line and direction. The
  hint case passes the hint's line and runs the rejoin search over the real route, with no
  synthetic start.
- The rejoin point must now:
  - be at least the corner distance plus 30 m ahead along the taxilane;
  - be within 25 m of the taxilane line;
  - be where the route carries on forward (the next segment within about 72° of the taxi
    direction).
- A hook at the start of a route is skipped instead of driven.
- `Ground_AllowsTwoPushbacksOnOneApronAndHoldsTheThird` and
  `ThirdPushbackOnABusyApron_NamesTheTwoTaxiingOut` still check the same rule: two taxi out at
  once and the third is held with "already taxiing out". They no longer fix which bays go first.

## Verification

- Headless suite: 933/933. The type-check is clean.
- Gate 15 on runway 23 now pushes to the taxilane and taxis straight on, joining the parallel lane
  and curving south-east with no reversal.
- Mac check: pushbacks on runway 23 from gates 15, 16R and 21, and three bay departures at once.
