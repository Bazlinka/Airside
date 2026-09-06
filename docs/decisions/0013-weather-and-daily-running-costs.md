# Decision 0013: simulated weather and daily running costs

Date: 2026-09-06

## Decision

The airport has weather (`Weather`, a static helper in the simulation layer). It
changes every five simulated minutes and is a pure function of the timeline — a
hash of the time-block index, biased toward mild conditions (Clear/Cloudy ~60%,
Storm ~3%). No random source is touched.

At each simulated midnight the airport pays running costs:
`BaseDailyOperatingCost` (400) plus a weather surcharge (0 for Clear/Cloudy up to
160 for Storm), applied through `AirportEconomy.PayOperatingCosts`. A line goes to
the event log. `AirportSimulation` catches up any midnights crossed in a single
`Update`, so it is frame-rate independent and reconstructed on offline catch-up.

Weather does **not** change flight timing in this version — it is informational
plus the daily cost.

## Reason

The economy could only ever go up: turnaround revenue and route income with no
recurring drain, so cash was not a constraint and on-time performance had no
teeth beyond the one-off delay cost. A daily running cost makes the airport
something you have to keep solvent, which is what makes route income and
reputation matter.

Weather is the plan's phase-four "simulated weather" and the natural driver for
the variable part of that cost. Keeping it timeline-only (no RNG, no effect on
flight phase durations) means every existing seed-based and timing-based test is
unaffected — the change is purely additive to the economy.

## Consequences

- `AirportEconomy` gains `PayOperatingCosts` and `TotalOperatingCost`; cash can
  now go negative (existing `PurchasePriorityCrew` already guards against
  spending when broke — that now bites).
- The away summary reports operating cost alongside delay cost.
- HUD shows the current weather on the day line.
- Weather affecting approach/turnaround timing, weather visuals (overcast tint,
  rain), and a "storm closes the runway" disruption are future work — they need
  care around the timing invariant and the seed tests.
- A game-over / insolvency state is not modelled yet; that belongs with the
  economy-recovery-paths work in phase four.
