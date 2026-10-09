# Economy realism plan — making money matter

**Status:** proposal for Bailey's sign-off (design change: needs an ADR and a save-version bump before any code).
**Date:** 8 October 2026. **Scope:** `FlightEconomics`, `Maintenance`, `AircraftAcquisition`, `PlayerBase`, daily running costs, contracts, difficulty.

## 1. What is wrong today

The economy is one number per decision, and the numbers are in two different units.

- **Aircraft, bases and checks look like real prices in thousands of dollars** (Saab 340 $1,600, ATR 42 $5,200, 737-800 $24,500, 787-10 $82,000, jet-gate base $6,000). Those match used-market values in $ thousands.
- **A flight costs and pays in plain dollars** (`DispatchCost` = 70 + km x 1.12 for a turboprop; `FlightPay` = 140 + 2 x km x weight x band). A 112 km Saab hop costs about $195 and pays about $364.
- Result: an aircraft is worth ~1/1000 of its real price against a flight that costs ~1/10 of its real cost. Rough payback today, before band multipliers: **Saab on ADL-KGC about 9 flights; ATR about 31; 737-800 on KGC about 117 (but 28 on MEL).** Real airlines take years. Cash is never a constraint after the first hour, so the fleet ladder is a timer, not a decision.
- There are no standing costs except `BaseDailyOperatingCost` (400/day, +weather). Parked aircraft are free. There is no fuel, crew, landing fee, lease, insurance, or depreciation. `ADR 0125` already tuned costs *down* once to keep the 100-150 hour career moving, so any rebalance must keep that pacing target explicit.

## 2. Inspiration (design patterns, from memory; not verified quotes)

| Game | Pattern worth borrowing |
|---|---|
| **Airport CEO** | Ongoing costs dominate: staff wages, facility upkeep, utilities; a loan you can drown in. Growth is limited by *monthly* margin, not purchase price. |
| **OpenTTD** | Every vehicle has yearly running costs even when idle; income is per cargo/distance/speed; loans with an interest rate and a cap; inflation. A vehicle that earns less than it costs is visibly a loss. |
| **Airline Manager / Airlines Manager Tycoon** | Fuel price swings, per-route profit panels, leases vs buying, maintenance as a scheduled cash event, yield tuning per route. |
| **Two Point / RollerCoaster Tycoon** | Cash crunches as the core loop: a bad month forces trade-offs; staff and upkeep make every new building a commitment. |
| **Real airlines (BITRE/IATA-style cost shares)** | Fuel ~25-30%, crew and maintenance ~10-15% each, ownership/lease ~10-15%, airport and en-route charges ~10-15%. Thin margins (single-digit %). |

Lesson: the fun is a **monthly P&L that can go red**, not a bigger price tag.

## 3. Proposed model

Keep the existing deterministic, clock-injected structure. Replace the two linear formulas with a cost build-up, and add standing costs.

**Per-flight variable cost** = block hours x (fuel + crew + maintenance reserve) + airport fees + handling + en-route charge, then x `CostMultiplier` (existing difficulty hook).

| Component | Driver | Indicative size (A$, to be sourced before implementing) |
|---|---|---|
| Fuel | block hours x type burn x fuel price | Saab ~$800/h, 737-800 ~$3,000/h, widebody ~$6,500/h |
| Crew | block hours x crew count | $400/h turboprop, $1,100/h narrowbody, $2,400/h widebody |
| Maintenance reserve | flight hours | $300/h turboprop, $900/h narrowbody, $2,000/h widebody |
| Landing + terminal fee | weight class, per landing | $250 Saab, $500 Q400, $1,800 737, $4,500 widebody |
| Handling / turnaround | per turn, by stand class | roughly half the landing fee |
| En-route / nav | km | small, ~$0.5-1.5/km |

**Standing cost per aircraft per day** (the part that makes idle aircraft hurt): ownership = ~0.05% of the type's price per day (depreciation, finance, insurance, about 18%/yr), plus base rent and ground-crew wages scaling with base level. Parked or in check, it still accrues.

**Revenue**: seats x load factor x fare(km), replacing the flat `2 x km x weight` pay. Load factor depends on time of day, route demand and reliability (reliability already scales pay via `ReliabilityMultiplier`). Contracts keep their advertised fixed pay.

**Fuel price**: slow deterministic random walk by day (seeded, no frame dependence), shown in the planner so the player can see a route's margin move.

**Checks and refits**: keep `CostFraction` but on the re-denominated price; outsourced multiplier 1.4 stays.

**Loans** (new, optional, phase 3): one rolling loan with interest, a cap by tier, and the existing recovery contract as the safety net so the player cannot hard-lock.

## 4. Denomination decision (needs Bailey)

- **A. Real dollars** — multiply aircraft/base/refit prices x1000 for display and make flight costs real ($4k hop, $1.6M Saab). Immersive, but then margins must be thin and the early game needs a bigger opening float to stay playable.
- **B. Game dollars (recommended)** — keep today's aircraft prices and scale flight costs and pay up so payback is meaningful: target **Saab payback ~120 flights, ATR ~250, 737-800 ~500 flights**, margin ~15-20% on a good route and negative on a bad one, plus a standing cost that makes an idle aircraft cost ~1-2% of its price per month.

Option B is a smaller migration (flight money only) and keeps the 100-150 hour pacing; option A reads more "real".

## 5. Implementation phases

1. **Data first** — a sourced `docs/data/AIRLINE_OPERATING_COSTS.md` (burn rates, fees, crew, yield) with licences/sources recorded as `AGENTS.md` requires.
2. **Pure cost model** — new `FlightCostModel` (no Unity), tests for payback targets per type and band, planner forecasts (`RouteForecast`) switched to it.
3. **Standing costs** — daily ownership/base costs in the midnight tick (`AirportEconomy`), shown in the P&L; catch-up stays deterministic.
4. **Save compatibility** — new persisted fields get an explicit version and migration; old saves keep their cash and get a one-time rebalance notice.
5. **Presentation** — per-route profit panel, fuel price indicator, monthly P&L, red-cash warnings and the recovery contract flow.
6. **Balance pass** — run the career bot (`CareerBot`) for the 100-150 hour target before shipping.

## 6. Risks

- Progression pacing (ADR 0120) and the Provisional-to-International gates are tuned on today's numbers; every threshold in `AircraftAcquisition`, `PlayerBase` and `ContractMarket` must be re-checked together.
- Many tests assert specific dollar values; expect broad test updates.
- A recovery path must remain so a bad run cannot soft-lock a save.

## 7. Questions for Bailey

1. A (real dollars) or B (game dollars)?
2. How punishing: should an idle aircraft be able to bankrupt you, or only slow you down?
3. Include loans and fuel-price swings in the first pass, or standing costs and per-flight build-up only?
4. Keep difficulty modes as pure multipliers, or give Hard real fuel volatility?
