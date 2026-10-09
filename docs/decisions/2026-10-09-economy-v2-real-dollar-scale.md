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
- First code: `Simulation/FlightCostModel.cs` and `FlightCostModelTests`. It is **not yet wired** into `FlightEconomics`, planner, HUD or
  saves; the live game still uses game-dollar formulas until the v24 migration lands.

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
