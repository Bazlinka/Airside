# Economy v2: real-dollar scale, real fees, tuned levers

Date: 2026-10-09
Status: approved by Bailey 9 Oct 2026 (direction); numbers below are proposals until the cost model's payback tests pass.

## Decision

1. **Scale.** All money in the game moves to real Australian dollars (economy plan option A). Aircraft prices, flight costs,
   fares, fees, funds and loans share one scale. The per-flight formulas in `FlightEconomics` are replaced by a pure
   `FlightCostModel` (no Unity types) built from block time, fuel, crew, maintenance reserve, airport and navigation
   charges, handling and en-route charges, plus per-aircraft standing costs. Revenue becomes seats x load factor x fare.
2. **Real where it changes a choice; tuned where it only sets pace.** Real inputs: Adelaide Airport per-passenger fees,
   Airservices weight-based charges, fuel burn and fuel price, fares and load factors. Tuned constants (named, in one
   place, documented): opening cash and loan, route subsidies on thin regional routes, aircraft values and leases where no
   free source exists, and the rate at which demand grows. Sources and confidence tiers: `docs/data/AIRLINE_OPERATING_COSTS.md`.
3. **Fuel.** A seeded, deterministic daily price walk with a baseline and a range. Hard difficulty widens the range; other
   difficulties stay cost multipliers. Baseline: a 2025-like price (about US$90-100 per barrel), not the 2026 spike.
4. **Loans and recovery.** One rolling bank loan with interest and a tier-based cap. Negative cash triggers the existing
   recovery contract and loan offer; there is no game over and no hard lock.
5. **Competitors** reuse this cost and revenue code (roadmap phase 2) and are not part of this change.

## Amendment, 9 Oct 2026 (same day): finance, not purchase

A prototype with the sourced fees showed that real prices and purchase-based growth cannot fit the 100-150 hour career, because the game runs
on the real clock. Hours of flying to earn back an aircraft's price: Saab 340 about 715, 737-800 about 2,500, A330-900 about 7,100, before
realistic overheads. Bailey left the choice to the team ("up to you"), and this is the chosen model:

- **Aircraft are leased, not bought.** Monthly lease is 0.9% of market value, with a three-month deposit and daily insurance (0.4% a year).
  The starter Saab 340 is owned. Growth is limited by margin, debt, gates, tier and reliability, as in a real airline, not by saving up a
  purchase price. The existing tier and reliability gates on `AircraftOffer` still decide when a type is offered.
- **Bank loan:** 8.5% a year, capped by tier (A$2M, 15M, 80M, 300M), with the existing recovery contract as the safety net.
- **Opening cash** A$1.5M, enough for an ATR 42 deposit (A$270k) but not a widebody deposit (A$3.8M).
- Tuned (tier D) constants are named in `FlightCostModel`; the tests pin ordering and viability, not the exact numbers.
- Code: `Domain/LeaseTerms.cs` (market values, deposit), `Simulation/FlightCostModel.cs`, `EconomyPacingTests`.

## Step 1 landed (9 Oct 2026): flight money, price list and save v24

- `FlightEconomics.DispatchCost` and `FlightPay` now come from `FlightCostModel` (an out-and-back is two legs). `AircraftOffer.Price` is the
  lease **deposit** (`LeaseTerms.Deposit`); "sell" still refunds `ResaleFraction` of it. Opening cash A$1.5M; Relaxed A$3.2M, Demanding A$1.1M.
  Adelaide base upgrades A$50k / 200k / 700k; outstation bases A$500k / 1.4M.
- **Maintenance is billed once.** The flight cost includes a maintenance reserve, and the game also bills explicit checks, which double-charged
  at first (the real `CareerBot` ran out of cash at 119 hours). Dispatch now excludes the reserve (`LegResult.DispatchCost`) and a check pays the
  reserve accrued over its interval (`FlightCostModel.CheckCost`).
- Save `CurrentVersion` 24. `AirlineSave.ScaleLegacyMoney` multiplies funds, the accepted contract's pay, the day's totals and lifetime-revenue
  tallies by `LegacySaveMoneyScale` (35) once, inside `Restore`. Known edge: a flight booked before the conversion and cancelled after it
  refunds at the new cost. Tests: `EconomyMigrationTests`.
- **Not yet done:** daily lease and insurance charges, the bank loan and its recovery path, HUD wording that says "deposit" and "lease", and a native
  check that seven-digit amounts fit every HUD control. Until the daily charges land there is no running cost for holding an aircraft, as before.

## Step 2 landed (10 Oct 2026): daily lease and insurance, save v25

- Every Adelaide day each player aircraft costs lease plus insurance (`FlightCostModel.StandingPerDay`); the founding Saab 340 is owned and pays
  insurance only. Charged by day index in `AirlineOperations.ChargeStandingCosts`, so an away catch-up pays each missed day once and stepping
  the clock in pieces gives the same result. Cash never goes below zero: a shortfall is reported ("only $X was available"); the bank loan, which
  would carry a shortfall, is **not built yet**.
- Save `CurrentVersion` 25 adds the last day paid. Older saves are not billed retroactively; the first day seen after loading is the start.
- Fleet cards show "$X deposit" and "lease $Y/day"; the operations line shows leases per day.
- Evidence: 8 `StandingCostTests`; casual `CareerBot` (seed 2, 100 h) Regional 11.2 h, Domestic 33.0 h, lowest cash A$44k (before: A$59k).
- **Open:** a competent `CareerBot` (seed 1, 150 h) stalled on this branch after the ground-crew merge (3 aircraft, 10 rotations, "waiting for
  a stand: every regional bay is in use"). The same bot ran fine on step 1 before that merge, and the comparison run on current `main` did not
  complete, so the cause is unattributed. Treat the competent-bot result as unverified until it is rerun.

### Real `CareerBot` comparison (Standard difficulty, 150 open hours, seed 1; old economy = `main` before this change)

| | Regional | Domestic | International | Fleet at 150 h | Cash at 150 h |
|---|---:|---:|---:|---:|---:|
| Old economy | 9.9 h | 30.4 h | 87.0 h | 6 | 29k |
| Economy v2 step 1 | 10.9 h | 30.2 h | 74.3 h | 5 | 632k |

A Casual player (seed 2, 100 h) reaches Regional at 11.2 h and Domestic at 33.0 h, lowest cash A$59k. Neither economy reaches the 18-aircraft
finale with this bot (6/18 before, 5/18 after), so that gap predates this change. Pacing is therefore at parity with ADR 0120's targets, not
better. The economy-only sim above was optimistic: it ignored dispatch cost timing, contracts, checks and demand, which is why the live bot
reaches 5 aircraft where the sim reaches 20.

### Pacing check (economy-only simulation, 9 Oct 2026)

`EconomyPacingTests` plays a competent, ambitious lessee through 150 open hours using only `FlightCostModel`, with stand-ins for bases
(fleet caps 3 / 8 / 14 / 20 by tier) and route saturation (each extra aircraft of a type earns 80% of the last). Baseline fuel:

| Open hour | Fleet | Loan | Net per open hour | Mix |
|---:|---:|---:|---:|---|
| 8 | 3 | 0 | A$1.3k | Saab + 2 ATR 42 |
| 35 | 8 | A$0.2M | A$2.3k | + Dash 8 |
| 70 | 14 | A$10M | A$14.9k | + A321neo, A220, A320 |
| 110 | 20 | A$38M | A$54k | + two 787-10, two A350, 787-9, A330-900 |

Cash never goes negative. At half revenue the fleet reaches 15 by hour 150, so money matters but tiers and bases still pace growth. Doubled fuel
price slows it without breaking it. First draft constants made money irrelevant (margins 26-53%), so overhead rose to 30% of revenue and crew
on-costs to 1.35x. Real margins are still lower than the game's; the CareerBot run must set the final level. Limits: this is not CareerBot, it
ignores contracts, reliability, curfew timing and base purchase costs, and every input is a tier D design value.

## Reason

Today's flat per-flight formulas make cash a timer, not a decision (ADR `2026-10-08-flight-cost-rebalance`). Real fee
structures create real trade-offs: Adelaide charges regional arrivals per passenger ($6.13) but Airservices charges by
weight ($12.78 per tonne), so a full small aircraft and a half-empty large one cost very differently.

## Affected systems

`FlightEconomics`, `AircraftAcquisition` prices, `PlayerBase` and refit costs, `Maintenance`, `AirportEconomy` daily
costs, `ContractMarket` pay, planner forecasts and refunds, `AwayCatchUp`, `Difficulty`, `CareerBot`, HUD and planner
display (profit per route, fuel price, monthly P&L), and most money-asserting tests.

## Migration impact

- Save `CurrentVersion` 23 -> 24. New fields: loan balance, fuel-price state, standing-cost accrual date.
- **Existing funds scale by 1,000** on load (a one-time migration keyed on version < 24), so relative wealth is preserved:
  the old Saab price of 1,600 becomes $1.6M, in line with the new scale. Owned aircraft and contracts are unchanged.
  Contract pay and refunds already in flight are converted with the same factor. Players see a one-time rebalance notice.
- The migration must be idempotent (never apply twice) and covered by a v23 fixture test.
- Dispatch cost and fares are computed, not persisted, so they need no migration.

## Acceptance

- Payback targets per type are written down and tested: Saab, ATR, 737-800 and one widebody.
- `CareerBot` confirms the 100-150 hour career (ADR 0120) still holds, and no run soft-locks.
- Simulation outcomes do not depend on frame rate; catch-up reproduces the same fuel-price walk and costs.
- Every sourced number in code cites its row in `AIRLINE_OPERATING_COSTS.md`; tier D values are labelled design constants.

## Open before code

Aircraft values and leases, fares, and crew on-costs remain tier C/D in the data file. The cost model can be written with
named placeholders, but none ships in a build until its source or design-constant label is recorded.
