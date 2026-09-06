# Decision 0030: cargo economics foundation

Date: 2026-09-06

## Decision

Introduce `AirportCargo`, following `AirportGeneralAviation`'s exact template
(decision 0024): a deterministic count of freighter contracts per day
(baseline 2, income 90 each) settles at every simulated midnight as its own
`DailyReport.CargoIncome` field, folded into `DailyReport.FlightIncome` so the
`NetCashChange == FlightIncome - DelayCost - OperatingCost` invariant keeps
holding. A single buildable expansion (`ExpandCargoWarehouse`, 5000,
`expand-cargo-warehouse` command) adds 2 contracts/day up to a maximum of 6.
No save-schema field — contract count replays from the command.

Cargo movements are economics only, same scoping call as general aviation:
no freighter aircraft, apron or taxi/stand reservation yet.

## Reason

Project-plan step 20 ("baggage, then cargo and general aviation using
existing resource systems") and the plan's cargo section ("a full progression
branch with freighters, aprons, warehouses, handling capacity, contracts and
night operations... using the common movement and resource systems while
retaining different economics") name cargo as GA's sibling system. Since GA's
economics-first slice (decision 0024) proved out safely, repeating the exact
same shape for cargo is the lowest-risk way to add real, distinct content
without touching the reservation/corridor invariants this session cannot
soak-test in Unity.

## Consequences

- `AirportCargo` is a new plain-C# simulation class; identical pattern to
  `AirportGeneralAviation`.
- `AirportEconomy` gains `TotalCargoIncome` / `AddCargoIncome`; `DailyReport`'s
  constructor gains a `cargoIncome` parameter (one call site, updated in the
  same change: `AirportSimulation.SettleDaysUpTo`).
- `PersistentAirportSession` gains `ExpandCargoWarehouse()` /
  `expand-cargo-warehouse`, following the established buildable-expansion
  pattern.
- **No HUD button this round.** The operations panel's left box already grew
  520 → 616 to fit the terminal/GA rows (decisions 0028, 0029, renumbered
  from 0023/0024 after this branch was rebased onto `main`, since Passenger
  Services research took 0023 there first); by
  calculation, a fifth and sixth stacked row would push it to roughly 708–730
  virtual units against the `Screen.height / 720f` canvas the whole HUD scales
  against — at or past the ceiling of a 720-tall window, before accounting
  for any margin. Stacking more rows onto a panel already flagged as
  unverified compounds the risk rather than just repeating a proven pattern.
  The command (`ExpandCargoWarehouse` / `expand-cargo-warehouse`) is real and
  callable; it just has no button yet. See the layout note added to
  `GAME.md`.
- Physical freighter aircraft, warehouses as visible buildings, and night
  operations remain future slices.
