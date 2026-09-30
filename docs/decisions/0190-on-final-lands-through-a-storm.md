# 0190 — An arrival already on final lands through a storm

Date: 30 September 2026. Author: Cursor, from Bailey's report: a plane on final will not land during a storm (CA103, card "Ground stop: storm over the field").

## Player-visible outcome

An aircraft already on final lands while the storm is still over the field. Departures stay at the hold until the storm hour ends. An arrival that has not reached final yet keeps holding, further out, and joins the approach when the storm block ends.

## Cause

ADR 0058 said a movement already underway — including one on final — is never interrupted, and that the ground stop only withholds the next clearance. The tower treated `HoldingForLanding` as "not started", so every arrival on the drawn final was held for the rest of the hour. The landing estimate did the same, parking the aircraft on the approach until the weather block ended.

## Decision

While `Weather.At(now)` is `Storm`:

- An aircraft in `HoldingForLanding` is cleared to land, subject to the same runway, vacate and crossing checks as in clear weather. A departure is not launched to make room for it; if the vacate would drive through someone holding short, the arrival waits.
- A departure in `HoldingShort` stays on the ground stop.
- An arrival still `Inbound` does not join final until the next non-storm block. Its card says the ground stop.

The landing estimate follows the tower: an aircraft already on final is not pushed past the storm, and a departure that the tower will not launch is not counted ahead of it.

## Not changed

Storm look, lightning, the daily operating cost, runway direction, and the ground-stop label on departures. No save field: the rule is derived from `Weather.At` and the aircraft state.

## Tests

`RunwayWeatherTests`: an on-final arrival lands during the storm hour and the departure does not; an inbound stays inbound until the block ends; second-by-second and skip-to-next-event still agree.
