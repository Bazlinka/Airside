# Decision 0032: baggage handling foundation (soft mishandling cost)

Date: 2026-09-06

## Decision

Introduce `AirportBaggage`: a per-day handling capacity (baseline 12/day,
matching `AirportTerminal`'s baseline), distinct from the terminal's hard
schedule gate. Unlike the terminal, exceeding baggage capacity does not block
route acceptance — it produces a mishandling cost (60 per excess scheduled
flight/day) folded into the existing daily operating cost at every midnight
settlement, alongside weather and payroll. A buildable sortation expansion
(`ExpandBaggageSortation`, 4500, `expand-baggage-sortation` command) raises
capacity by 6/day up to a maximum of 18/day. No new `DailyReport` field —
the cost folds into the existing `operatingCost` number, same as weather and
payroll, so no constructor or call-site change was needed beyond the one line
that computes `cost`. No save-schema field; capacity replays from the command.

Because baggage's baseline (12/day) exactly matches the terminal's baseline
cap, and the terminal already caps `MaxScheduledFlightsPerDay` at 12 unless
separately expanded (decision 0028, renumbered from 0023 after this branch
was rebased onto `main`) — including after a third stand raises
the stand-based cap to 18 — `ScheduledFlightsPerDay` cannot exceed 12
anywhere in the existing test suite without a test deliberately calling both
`BuildThirdStand` and `ExpandCheckInHall` and then over-booking, which none
currently do. The mishandling cost is therefore provably zero under every
existing behaviour; a new test
(`FullyBookedScheduleWithoutTerminalExpansion_NeverExceedsBaggageCapacity`)
demonstrates it directly by building a third stand, accepting every route
offer reputation allows for ~16 simulated days, and asserting the cost stays
zero throughout.

## Reason

Project-plan step 20 lists baggage first ("Add baggage, then cargo and
general aviation using existing resource systems"); general aviation and
cargo were done first in this session (decisions 0024, 0025) specifically
because their per-day-income shape was provably safe, while baggage's
plan description ("major source of operational depth" via check-in,
screening, sortation, cart transport, loading, unloading, reclaim) points at
per-flight task-level timing — i.e. `TurnaroundWorkflow`, which
`StaffingTests` and the turnaround/economy tests assert exact durations
against. Touching that blind, with no way to re-run those tests, was too
risky.

This slice gets a real baggage system in without that risk: a capacity
number with its own soft consequence (cost, not a schedule block), safe by
the same "baseline provably inert" argument used for the terminal, general
aviation and cargo foundations. It is deliberately not the deep task-level
system the plan eventually wants — that remains future work for a session
that can run the full suite after changing `TurnaroundWorkflow`.

## Consequences

- `AirportBaggage` is a new plain-C# simulation class.
- `AirportSimulation.SettleDaysUpTo` adds `Baggage.MishandlingCostFor(Routes.ScheduledFlightsPerDay)`
  into the existing `cost` variable, before it's paid and recorded — no new
  `DailyReport` field, no other call site touched.
- `PersistentAirportSession` gains `ExpandBaggageSortation()` /
  `expand-baggage-sortation`, following the established pattern.
- **No HUD button this round** — same operations-panel height-budget reason
  as decisions 0025 (cargo) and 0026 (land).
- Per-flight baggage volume, check-in/screening/sortation/reclaim as distinct
  tasks, and any coupling to `TurnaroundWorkflow` timing remain future work.
