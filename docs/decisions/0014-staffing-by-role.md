# Decision 0014: staffing by role

Date: 2026-09-06

## Decision

The airport employs ground crew (`AirportStaffing` in the simulation layer). The
baseline headcount (4) runs turnarounds at the normal rate. Hiring more shortens
turnarounds (down to 0.8×); dropping below the baseline stretches them (each
missing crew adds 0.18 to the duration multiplier) and produces delays.

Crew are paid a daily wage (55 each) settled with the other running costs at each
simulated midnight. Hiring costs a one-off 120 and is a persisted command
(`hire-crew` / `release-crew`), replayed on load and offline catch-up, so the
headcount is reconstructed exactly with no new save field.

`TurnaroundWorkflow` gains an optional `staffingFactor` (default 1.0). Its
`Duration` now multiplies by `staffingFactor × (priority ? 0.7 : 1.0)` and
short-circuits to the raw value when the product is exactly 1.0 — so a game at the
baseline headcount, with or without a priority crew, produces byte-identical
timing to before. Every existing seed and timing test is unaffected.

## Reason

Phase four calls for "staffing by role" and "service shortages create visible and
numerical consequences" (also a phase-two exit criterion that was only partly
met). Staffing gives the player a standing spend decision that trades cash for
turnaround speed and reputation, and it makes payroll a second recurring expense
alongside the weather-driven running cost — more pressure to keep the airport
earning.

## Consequences

- `AirportEconomy` gains a generic `TrySpend`; `PurchasePriorityCrew` now routes
  through it (unchanged behaviour).
- `AirportSimulation` gains `Staffing`, `HireGroundCrew`, `ReleaseGroundCrew`, and
  builds each `TurnaroundWorkflow` with the current staffing factor.
- The daily settlement adds `Staffing.DailyWage` to the running cost.
- HUD shows the headcount, payroll and an understaffed warning, with hire/release
  buttons.
- Only one role so far. ATC, engineering and a proper roster with shifts are
  future work; the model (a factor per role feeding the relevant system) should
  extend.
