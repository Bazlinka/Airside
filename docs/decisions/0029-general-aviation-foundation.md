# Decision 0029: general-aviation economics foundation

Date: 2026-09-06

## Decision

Introduce `AirportGeneralAviation`: a deterministic count of GA movements per
simulated day (baseline 3, from charters/training/small aircraft), each paying
a landing fee (45). Income settles at every simulated midnight alongside the
existing daily report, added via a new `AirportEconomy.AddGeneralAviationIncome`
and tracked as its own `DailyReport.GeneralAviationIncome` field (folded into
`DailyReport.FlightIncome` so `NetCashChange == FlightIncome - DelayCost -
OperatingCost` continues to hold).

A single buildable expansion (`ExpandGeneralAviationApron`, 4000,
`expand-ga-apron` command) raises movements to the maximum (6/day). Movement
count is reconstructed by command replay, no new save field.

This slice is economics only. GA movements do not spawn a visible aircraft or
take a taxi/stand reservation — they are an abstracted daily count, like the
pre-existing weather/payroll running costs but as income. A physical GA
aircraft sharing taxiways and aprons would need the same soak-tested
deadlock-freedom guarantee documented for the commercial/fleet corridor
(`docs/decisions/0008-ground-traffic-fleet-and-corridor-lock.md`,
`0009-fair-corridor-handoff.md`); that verification needs the Unity editor and
was out of reach for the session that wrote this change.

## Reason

Project-plan step 20 calls for "baggage, then cargo and general aviation using
existing resource systems," with general aviation explicitly framed as the
simplest of the three ("gives early airports activity through small aircraft,
charters, training, medevac and business aviation... using the common
movement and resource systems while retaining different economics"). Landing
each GA movement as its own tracked, buildable income stream — reusing the
existing economy/command/replay machinery — is the safe first cut: real new
content, zero risk to the reservation/corridor invariants the codebase
already soak-tests.

## Consequences

- `AirportGeneralAviation` is a new plain-C# simulation class; same
  baseline/cap/buildable-expansion/no-save-field pattern as
  `AirportCapacity`/`AirportTerminal`.
- `AirportEconomy` gains `TotalGeneralAviationIncome` and
  `AddGeneralAviationIncome`; `DailyReport` gains a `GeneralAviationIncome`
  field (its constructor signature changed — the only call site is
  `AirportSimulation.SettleDaysUpTo`, updated in the same change).
- `PersistentAirportSession` gains `ExpandGeneralAviationApron()` / the
  `expand-ga-apron` command, following the `build-stand`/`expand-checkin`
  pattern.
- `DailyFinanceBrief` is deliberately left untouched (it does not yet project
  GA income), to avoid touching its existing, closely-asserted test
  expectations in the same change that adds GA.
- No HUD wiring yet, for the same visual-verification reason as decision 0028
  (terminal capacity — renumbered from 0023 after this branch was rebased
  onto `main`, since Passenger Services research took 0023 there first).
- Visible GA aircraft with their own taxi/stand reservation, cargo, and
  baggage remain future slices.
