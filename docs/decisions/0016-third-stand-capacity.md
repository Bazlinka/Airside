# Decision 0016: first buildable capacity upgrade (third stand)

Date: 2026-09-06

## Decision

The airfield starts with two stands. The player can buy a third stand for a
one-off cost of 8000 (`AirportCapacity`). Purchase is a persisted command
(`build-stand`), replayed on load and offline catch-up, so stand count is
reconstructed with no new save field.

`AirportTaxiNetwork` gains stand 3 lead-in and geometry (Z = 26). The primary
flight picks among `Capacity.StandCount` stands. Ground-traffic arrivals use
`AirportSimulation.AlternateStand`: with two stands the historical other-stand
rule (seed-identical); with three, the lowest-index free stand.

## Reason

Phase four and the project-plan build order call for a single meaningful
capacity upgrade once money flows exist. A third stand is the natural hook for
later concurrent commercial flights (see `docs/product/concurrent-flights-brief.md`)
and gives the player a lasting spend decision that changes the airfield.

## Consequences

- `AirportCapacity` tracks stand count; `AirportSimulation.BuildThirdStand` spends
  and expands.
- Taxi routes, ground traffic and presentation markings include stand 3.
- HUD shows stand count and a build button.
- Concurrent flights remain a separate design pass; this change only unlocks the
  physical capacity those flights will need.
