# 0065 — Departure turn jump, unrealistic departure spacing, stand-queue overflow, apron light starvation

Date: 2026-09-20

## Decision

Four independent, concrete bug fixes from a real play session's report ("planes kinda take off
and then jump over a bit and then like back up again"; "holding takes a bit unrealistic in how
long they take before they proceed to take off after a plane has left"; "when a plane lands and
its gate is full it needs to have a place to go instead of being stuck"; apron floodlights
"doesn't light up the apron").

1. **Departure-turn jump-then-back-up, fixed.** `AirsidePrototype.ApplyDepartureTurn` compared
   `progress` (a phase-local 0..1) against `DepartureTurn.TurnEstablishedProgress` (0.88)
   regardless of which phase it was called for. That threshold is only meaningful on
   **Departed's** own progress scale. During **Takeoff** — a completely different phase
   duration (ground roll + initial climb) — progress legitimately crosses 0.88 well before the
   phase ends, at which point the code fell into the "extra distance past establishment" branch
   using `AirsideFlightPath.Departed`'s reference point as if the aircraft were already in that
   phase. Since Takeoff's own progress has nothing to do with Departed's X range, this snapped
   the aircraft sideways/forward mid-climb — then it "backed up" the instant the phase actually
   became Departed and progress genuinely reset to 0. Fixed: the established-track branch now
   only ever applies when `phase == AircraftPhase.Departed`.
2. **Departure spacing shortened to the actual runway occupancy.** `AirlineOperations.
   RunTowerOnStrip`'s departure branch used the full `TakingOff` state duration (ground roll
   **plus** the initial climb, already well clear of the tarmac) as "how long the runway is
   occupied," then added wake separation on top — double-counting airborne climb time that has
   nothing to do with runway occupancy. The queued next departure/arrival now waits only for
   lineup + the ground roll to rotation (`TakeoffRollExactSeconds`) plus the (unchanged) wake
   separation — mirroring the arrival side's existing `ClearOfRunwaySeconds` vs. full landing
   duration distinction, which this file already drew correctly for arrivals but never had for
   departures.
3. **Stand-queue overflow no longer collapses aircraft onto one point.** `AdelaideGround.
   AwaitingPose` / `HoldingShortPose` queued aircraft back along a taxiway by
   `AwaitingSpacingMetres * slot`, clamped to the taxiway's own length. Past that point — a busy
   day with more arrivals waiting for a stand than the taxiway is long — every further aircraft
   sampled the exact same clamped point and stacked on top of each other instead of getting a
   real place to wait. Now uses `GroundPath.PointAtDistance`, which already existed for exactly
   this ("beyond either end it continues in a straight line along the end segment") but wasn't
   being used here.
4. **Apron floodlights raised out of URP's per-object light starvation.** `m_AdditionalLightsPerObjectLimit`
   (`AirsideRuntimeQuality.HighAdditionalLights`/`MediumAdditionalLights`, and the matching
   value baked into `PC_RPAsset.asset`/`Mobile_RPAsset.asset`) capped every renderer to the
   nearest 12 (4 on Medium) real-time lights, scene-wide. The field has 10 apron floods
   competing against hundreds of runway edge/threshold/PAPI/ALS lights across a ~3.9 km strip,
   plus stand markers and landside streetlights, for that same shared budget on any ground mesh
   they're all near — easily starving the apron floods specifically, which matches "lights look
   fine in theory but the apron doesn't actually light up." Raised to 24 (High) / 12 (Medium).

## Reason

All four are real play-session reports, not hypothetical code review — the same standard this
session has held to throughout (ADR 0059-0064). Each was verified by tracing the exact code path
with concrete numbers before touching anything, per this project's standing bar for "real bug,"
not "this could maybe be a problem":

- (1) traced RotateProgress/TurnEstablishedProgress against `AircraftPhase.Takeoff`'s own
  duration to confirm the crossover is real and not just theoretically possible.
- (2) compared `TakeoffRollExactSeconds` (ground roll only) against `TakeoffExactSeconds`
  (roll + initial climb) to confirm the current code counts genuinely-airborne time as runway
  occupancy, and found the arrival side already drawing the correct distinction the departure
  side was missing.
- (3) found `HoldingShortPose`'s own missing-departure-stand branch already extrapolates
  correctly (`hx - fx * back`, unclamped) right next to the two branches that don't — the same
  file solves this correctly in one place and not the other two, confirming it's an oversight,
  not an intentional cap.
- (4) confirmed via the checked-in `.asset` files that both the Editor and a real build already
  run classic URP Forward with a strict serialized per-object cap (`m_AdditionalLightsRenderingMode: 1` /
  PerPixel, `m_AdditionalLightsPerObjectLimit`) rather than Forward+, so the cap is a real,
  active constraint, not a moot one.

## Consequences

- (1) and (4) are Presentation/Unity-only — reviewed by inspection, no headless coverage.
  (4) also required editing the two committed `.asset` files directly, since
  `AirsideRuntimeQuality.Apply()` only writes the pipeline asset in a real build
  (`WritesPipelineAsset => !Application.isEditor`) — Editor play uses the checked-in asset
  values as-is regardless of the code constants.
- (2) and (3) are Simulation-layer and fully covered by `scripts/test-domain.sh`: **500/500**,
  no regressions. No existing test asserted an exact `RunwayFreeAt`/queue-position value that
  the shortened departure spacing or corrected queue math would have broken (checked
  `DualRunwayTowerTests.cs` specifically — its assertions are `>0`/`>=` floor checks, not exact
  equalities).
- `AirsideRuntimeQuality.HighAdditionalLights`/`MediumAdditionalLights`'s existing test
  (`PresentationLayoutTests.cs`) updated to the new values — Presentation-only, outside the
  headless harness, but kept consistent for whenever `scripts/test-unity.sh` runs.
- As with every Presentation-only fix this session: reasoned and traced, not confirmed by eye.
  (2) and (3) being Simulation-layer changes are the most trustworthy of the four, since the
  headless suite genuinely proves their logic; (1) and (4) still need a real look — (4) in
  particular is a strong, well-evidenced diagnosis but the exact right cap without a
  performance/visual look is still a guess within a reasonable range.

## Tests

`scripts/test-domain.sh`: 500/500 (no new tests added — (2) and (3) are existing-path
corrections covered by the existing tower/stand-choice suites' behavioural assertions, not new
named tests). `PresentationLayoutTests.HighAdditionalLights`/`MediumAdditionalLights` updated to
match the new constants (outside the headless harness). (1) and (4) remain owed a real look in
Unity — now four open "reasoned, not confirmed" items alongside the night-lighting work from
ADR 0063/0064.
