# Decision 0017: research progression (operations efficiency)

Date: 2026-09-06

## Decision

The airport has a real-time research track (`AirportResearch`). The first project,
**Operations Efficiency**, costs 2500 to start, takes one simulated day
(`DayCycle.DaySeconds`), and when complete permanently reduces the base daily
running cost by 100. Only one research project can run at a time. Starting
research is a persisted command (`start-research`), replayed on load and offline
catch-up, so completion is reconstructed from the timeline with no new save field.

Research does not change flight or turnaround timing — only the economy — so
existing seed and timing tests stay green.

## Reason

Phase four and the project plan call for research as a progression layer alongside
money and reputation. A single timed project that lowers operating cost gives the
player a clear long-horizon spend decision and makes cash management more
interesting without opening the concurrent-flights architecture yet.

## Consequences

- `AirportSimulation` exposes `Research` and `StartOperationsResearch`.
- Daily settlement subtracts `Research.DailyOperatingDiscount` from the base cost.
- HUD shows research status and a start button.
- Further research branches (baggage, surface movement, etc.) can reuse the same
  start/update/complete pattern.
