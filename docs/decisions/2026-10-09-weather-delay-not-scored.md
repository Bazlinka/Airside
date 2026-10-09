# Weather delay is not scored against punctuality

Date: 2026-10-09
Status: Accepted — Bailey's request to improve weather consequences across the board

## Decision

Seconds of a player pushback delay attributed to `DelayCause.Weather` (a storm ground stop at the stand,
or a helicopter held by weather) are subtracted before the flight's reliability change and on-time streak
are scored (`FlightEconomics.ControllableLateness`). Pay, delay display and the delay breakdown are
unchanged: the player still sees the weather delay, it just no longer costs reliability.

## Reason

Real airlines separate controllable from weather delay. A storm that grounds the field for an hour was
already attributed to weather in the breakdown but still dropped reliability by up to 2 points a flight.

## Affected systems / migration

`AirlineOperations` settlement only. No schema change; saves are unaffected. Past flights are not rescored.

## Considered and not done

Fog low-visibility runway spacing (150 s instead of 90 s). It worked mechanically but pushed
`ArrivalClearanceTests.ExpectedClearance_MatchesWhenTheTowerActuallyClears` (85 % threshold) under its limit
because the landing-clearance estimator cannot foresee the knock-on shifts. It needs estimator work first.
