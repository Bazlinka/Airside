# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- **Concurrent flights design.** Decision 0019 locks promotion-to-list model,
  commercial FIFO priority, stand-based concurrency cap, schedule cadence
  (`ScheduledFlightsPerDay >= 4` → second flight), and per-flight settlement.
  First-slice task packet ready. Brief marked designed. No gameplay code in this
  change.
- **Daily operations report.** At each simulated midnight the sim publishes a
  `DailyReport` (flights, income, delays, running cost, net cash, reputation,
  weather, crew). Keeps the latest seven; rebuilt by replay. HUD shows the
  latest card. See `docs/decisions/0018-daily-operations-report.md`.
- **Research progression.** `AirportResearch` — first project Operations Efficiency
  (2500, one simulated day) permanently cuts base daily running cost by 100.
  Start is a persisted `start-research` command (replayed on load). Does not
  change flight timing. HUD shows progress / complete. See
  `docs/decisions/0017-research-operations-efficiency.md`.
- **Buildable third stand.** `AirportCapacity` — first capacity upgrade. Spend
  8000 (`build-stand` command, replayed on load) to unlock Stand 3; taxi network
  gains lead-in geometry; primary flights and ground traffic use the new stand.
  Two-stand behaviour stays seed-identical. HUD shows stand count and a build
  button. See `docs/decisions/0016-third-stand-capacity.md`.
- **Insolvency / game-over.** Cash negative at three consecutive simulated day
  closes declares the airport insolvent: simulation freezes, player commands
  refuse, and an `"Insolvent"` event is logged. Tracked on `AirportEconomy`
  (`ConsecutiveNegativeDays`, `IsInsolvent`); rebuilt by replay, no save-schema
  change. Presentation untouched. See `docs/decisions/0015-insolvency-game-over.md`.
- Test line.
- **Staffing by role.** `AirportStaffing` — ground crew, baseline 4. The baseline
  runs turnarounds unchanged (`TurnaroundWorkflow` gains an optional
  `staffingFactor` that short-circuits at 1.0, so every seed/timing test is
  byte-identical); extra crew shorten turnarounds, understaffing lengthens them
  into delays. Hire (120, persisted `hire-crew`/`release-crew` commands) and a
  daily wage settled with the running costs. `AirportEconomy.TrySpend` generalises
  the old priority-crew purchase. HUD shows headcount/payroll with hire/release
  and an understaffed warning. 60/60 tests. See `docs/decisions/0014-staffing-by-role.md`.
- **Simulated weather + daily running costs.** `Weather` (deterministic from the
  timeline, biased mild) changes every 5 simulated minutes. Each simulated
  midnight the airport pays `BaseDailyOperatingCost` (400) plus a weather
  surcharge (0–160) via `AirportEconomy.PayOperatingCosts` — the economy now has
  a drain, so cash can fall and route income / on-time performance matter for
  solvency. Weather does not affect flight timing (keeps every seed/timing test
  green). HUD shows current weather; away summary reports operating cost. 56/56
  tests; macOS build runs. See `docs/decisions/0013-weather-and-daily-running-costs.md`.
- Route proposals can now be **declined** (a persisted `decline-route` command).
  The HUD offer panel gains a Decline button; `AirportRoutes` exposes
  `OffersDeclined` and `ScheduledFlightsPerDay` (sum of accepted routes'
  flights/day), shown on the HUD. 51/51 tests. Groundwork for
  `docs/product/concurrent-flights-brief.md`.
- The "welcome back" away summary now also reports **route income earned** and the
  **reputation change** while the player was away (design pillar: a short visit
  should reveal what changed). `AirportEconomy` tracks `TotalRouteIncome`.
  48/48 tests.
- **Reputation** (0–100, starts 50). On-time departures raise it, delays lower it
  in proportion to the delay. `AirportRoutes.Accept` now takes the score and
  refuses proposals above the airport's reputation; an accepted route locks in a
  per-flight bonus of `3 × (score − 50)`. Reputation is rebuilt by replay — no
  persisted field. HUD shows score + band and disables offers the airport can't
  meet. 47/47 tests; macOS build runs. See `docs/decisions/0012-reputation.md`.
- **Airline route proposals.** `AirportRoutes` (simulation) offers one scheduled
  service at a time on a timer; it lapses if unaccepted. Accepting is a persisted
  `accept-route` command (replayed on load / offline catch-up); every completed
  flight then pays `Routes.IncomePerFlight` on top of turnaround revenue.
  Proposal content comes from the timeline, not the random source, so existing
  seed-based outcomes are unchanged. HUD shows the offer with an Accept button.
  42/42 tests; macOS build runs. See `docs/decisions/0011-airline-route-proposals.md`.
- Airport **location** + **day/night cycle**. `AirportLocation` (domain) carries
  id/name/region/UTC offset/latitude; ships with Kingscote (default), Port Lincoln
  and Coober Pedy. `DayCycle` derives local time from the sim clock — one
  simulated day per 20 real minutes from an 08:00 start — and drives the sun and
  ambient light and a HUD line (location, day, clock, phase). **Save schema → v2**
  (adds `locationId`); schema-1 saves migrate on load. 37/37 tests; macOS build
  runs. See `docs/decisions/0010-location-and-day-cycle.md`.
- Fair corridor hand-off: when the shared A1/A2 corridor is free and more than one
  fleet aircraft is queued, it goes to the one that has waited longest (fleet
  order breaks ties), instead of fleet order alone. Reuses the traffic monitor's
  wait timestamps — no new state. A forty-cycle soak asserts neither fleet
  aircraft is starved. 30/30 edit-mode tests; macOS build runs.
  See `docs/decisions/0009-fair-corridor-handoff.md`.
- Ground-traffic **fleet**: `AirportSimulation.GroundTraffic` is now a list.
  `GT-201` runs an arrival/stand/departure schedule (parking on whichever stand
  the primary flight is not using); `GT-202` repositions in and out through a
  run-up bay without a stand, starting 25s later. Every fleet aircraft reserves a
  single-file `TAXI-CORRIDOR` lock while on A1/A2, so at most one is on the shared
  taxiway at a time — they queue instead of meeting head-on. The primary flight
  keeps priority and is never blocked (`ReservationConflicts` stays zero).
  Deadlock-free by construction. Presentation renders one model per fleet aircraft
  and lists them in the HUD. 28/28 edit-mode tests; macOS build runs.
  See `docs/decisions/0008-ground-traffic-fleet-and-corridor-lock.md`.
- Earlier the same day: single second aircraft — shared segment reservations
  (`0006`), then an arrival/stand/departure schedule (`0007`).
- Second aircraft first introduced: shared taxi-segment reservations with the
  primary flight, priority-and-yield rule, per-tick reservation time so segments
  are correct during offline catch-up.
  See `docs/decisions/0006-second-aircraft-priority-and-yield.md`.
- Repo set up for shared work: added `.gitattributes` (Unity merge/binary rules),
  expanded `AGENTS.md` into the shared contract, added `CLAUDE.md` and Cursor
  rules, this changelog, and `LICENSES.md`. Removed a stale duplicate
  `AirsidePrototype 2.cs`. Pushed to a private GitHub `origin`.
- Segment clearance and traffic wait diagnostics: taxiing aircraft reserve only
  the segment they occupy and release it before moving on; a monitor explains any
  aircraft blocked on one resource for ten seconds or more
  (`docs/decisions/0005-segment-clearance-and-wait-diagnostics.md`).

## 2026-09-06

- Add named taxi routes and operations history.
- Add persistent saves and offline catch-up.
- Add operational turnaround and economy loop.
- Complete deterministic movement milestone.
- Open the Airside prototype by default.
- Add first playable Airside prototype.
- Target current Unity 6.3 LTS patch.
- Create Airside project foundation.
