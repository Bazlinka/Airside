# Decision 0018: daily operations report

Date: 2026-09-06

## Decision

At each simulated midnight settlement, `AirportSimulation` publishes a
`DailyReport` into `AirportDailyReports`: flights completed that day, turnaround
and route income, delay cost, operating cost, net cash change, reputation change,
closing weather and crew count. The latest seven reports are kept. Reports are
rebuilt by replaying the timeline (no save-schema field). The HUD shows the
latest report beside the operations log.

## Reason

Phase four called for a daily report panel, and the design pillars say a short
visit should reveal what changed. Midnight already settles costs; packaging that
moment as a readable day card makes the economy and operations legible without
reading the raw event stream.

## Consequences

- Day-close event text includes the report summary.
- Presentation shows a "DAILY REPORT" card when at least one day has closed.
- Further analytics (charts, weekly rollups) can consume `AirportDailyReports`.
