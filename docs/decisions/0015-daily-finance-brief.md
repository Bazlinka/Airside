# Decision 0015: daily finance brief

Date: 2026-09-06

## Decision

`AirportSimulation` exposes a read-only `DailyFinance` (`DailyFinanceBrief`) that
projects:

- **expected operating cost** = base daily cost + current weather surcharge +
  current crew payroll
- **expected flight income** = `(TurnaroundRevenue + route income per flight) ×
  DaySeconds / CycleLengthSeconds` (integer division; assumes no delays)
- **expected net** and, when net is negative, **cash runway days** =
  `Cash / -ExpectedNet`

The HUD shows the estimate under the cash line. No save-schema field; the brief
is derived from live state.

## Reason

The airport already has income, payroll and midnight running costs, and insolvency
work will punish multi-day losses — but the player cannot yet see whether today is
projected to earn or burn. A deterministic forward estimate makes staffing, routes
and weather readable as cash decisions without waiting for midnight.

## Consequences

- Pure projection: does not change economy, RNG or persistence.
- Weather used is the *current* sky, not the midnight settlement weather — close
  enough for a HUD hint; the daily report (separate) can show actuals.
- When concurrent flights raise real throughput above one cycle's income model,
  revisit the income side of the brief so it tracks commercial departures/day.

## Migration impact

None.
