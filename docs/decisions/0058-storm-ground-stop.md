# 0058 — Storms hold the runway (a ground stop)

Date: 2026-09-20

## Decision

Weather now has a real operational consequence, not only a look and a daily cost
(ADR 0013). While `Weather.At(now)` is `Storm`, the tower withholds every *new*
landing or takeoff clearance on both strips (`AirlineOperations.RunTowerOnStrip`).
A movement already underway — rolling, on final, taxiing — is never interrupted;
this only stops the next one from starting. Arrivals already have an
indefinite-wait state to fall into (`HoldingForLanding`, the existing holding
pattern); departures do the same at `HoldingShort`. Both queues release the
moment the storm block ends, oldest-waiting first, exactly as they do today
outside a storm.

`AirlineOperations.CurrentWeather` and `IsGroundStopped` expose this to
Presentation. `OperationsWorkspaceModel.Subtitle` now names the current weather
and appends "GROUND STOP" while a storm is holding traffic, so a queue that
stops moving has a visible reason instead of looking stuck.

## Reason

ADR 0013 explicitly left "weather affecting approach/turnaround timing" and "a
storm closes the runway" as future work, timing-invariant permitting. Until now
`Weather` was cosmetic (fog dimmed visibility, rain wet the pavement) plus a
flat daily surcharge — a storm looked worse but changed nothing a player did.
Gating the tower is the smallest change that gives weather teeth: it uses the
existing holding-pattern and hold-short states as the "storm reserve" rather
than inventing a new one, and it touches one decision point
(`RunTowerOnStrip`) instead of the aircraft state machine.

## Determinism (the invariant this had to respect)

`Weather.At` is already a pure function of simulated time — no RNG, identical
under any step size or offline catch-up (ADR 0013). Gating clearance on it
keeps that: `RunTowerOnStrip` re-evaluates `Weather.At(now)` at whatever `now`
it is called with, so the same instant always answers the same way regardless
of how the caller arrived there.

The one place this needed a real fix rather than just being naturally true:
`AirlineOperations.NextEventAt()` (the skip-to-next-event catch-up path) used
to consider only `_mainRunwayFreeAt`/`_crossRunwayFreeAt` as "when the runway
might next do something." A strip can be free-at-or-before `now` yet still be
storm-held, and `RunTowerOnStrip` checks that against `now` itself rather than
any tracked "reopens at" time — so a big skip could land past the moment the
storm actually cleared and grant the queued clearance later than a run of
small steps would have. `NextEventAt` now also considers the start of the next
weather block whenever a runway is wanted and the current block is a storm,
so catch-up revisits the check at the same block granularity live play does.

Verified, not just reasoned about: block 34 of the timeline (122400–125999s)
hashes to `Storm` with the game's fixed weather function, bracketed by `Clear`
(block 33) and `Fog` (block 35). Two new tests in `RunwayWeatherTests`
(`Storm_HoldsTheClearanceUntilWeatherClears`,
`Storm_ReleaseIsIdenticalWhetherSteppedBySecondOrSkippedToTheNextEvent`) put a
restored `HoldingForLanding` aircraft through exactly that window and assert
the same final state whether the clock steps second-by-second or jumps
straight to the next event. The pre-existing 36-hour
`Timeline_IsIdenticalForAnyStepSizeOrSkipping` test independently crosses the
same storm block (its horizon runs past it) and stayed green across all three
of its stepping strategies without modification.

## Consequences

- A storm now backs up traffic for roughly its one-hour block (blocks are
  independent draws, so consecutive storm blocks are rare — about 0.09% for
  two in a row) rather than only tinting the sky.
- No new save field: the ground stop is derived from `Weather.At(_processedTo)`
  the same way the existing daily surcharge is, so old saves resume under the
  new rule from their next tower decision with no migration.
- Flight timing still is not touched anywhere else — dispatch cost, prep
  duration, airborne time and the daily operating surcharge are all unchanged.
  This is additive to ADR 0013, not a revision of it.
- Lightning/thunder presentation for storms remains future work (still not
  added).

## Tests

`RunwayWeatherTests` (+2): `Storm_HoldsTheClearanceUntilWeatherClears`,
`Storm_ReleaseIsIdenticalWhetherSteppedBySecondOrSkippedToTheNextEvent`.
`OperationsWorkspaceTests` (+1):
`Operations_SubtitleNamesTheWeatherAndFlagsAGroundStop`.
`scripts/test-domain.sh`: 484/484.
