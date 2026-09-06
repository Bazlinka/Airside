# Decision 0015: insolvency / game-over state

Date: 2026-09-06

## Decision

When the airport's cash balance is negative at the close of three consecutive
simulated days, the airport is declared insolvent and the simulation stops. The
check lives on `AirportEconomy.EvaluateDayEndSolvency`, called from the existing
midnight settlement in `AirportSimulation`. An `"Insolvent"` line is written to
the operational event log. Player commands refuse to apply once insolvent. No
save-schema change: the flag is rebuilt by replaying the timeline.

## Reason

Decision 0013 added a recurring cash drain so the airport can go broke, but left
game-over unmodelled. A three-day consecutive grace keeps the failure forgiving
(a single rough day is recoverable) while still giving cash a hard consequence.
Day-end evaluation keeps it clock-driven and frame-rate independent, matching the
rest of the economy.

## Consequences

- `AirportEconomy` tracks `ConsecutiveNegativeDays` and `IsInsolvent`.
- `AirportSimulation.IsInsolvent` exposes the outcome; `Update` no-ops after it.
- A positive day-end cash balance resets the consecutive counter.
- Presentation is unchanged in this change — the event-log title is the visible
  signal for now; a dedicated HUD banner is follow-up work.
- Financing / recovery paths (loans, bailouts) remain future phase-four work.
