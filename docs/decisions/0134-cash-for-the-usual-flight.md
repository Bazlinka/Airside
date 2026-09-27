# 0134 — Keep cash for the usual flight

Date: 27 September 2026. Author: Claude.

## Context

After the new aircraft (ADR 0131), the balance bot was refused about 2,600 flights it couldn't
afford, against 240 before, and its cash hit $1. It looked like a Domestic-stage squeeze. The
run data says otherwise:
- Hourly cash was never low in the Domestic stage.
- No rescue contract ever fired.
- The refusals came in bursts right after a widebody purchase.

The cause was the bot's cash reserve. It kept enough for each aircraft to fly 1,500 km once. A
widebody's usual flight is long-haul. The cheaper A330 let it buy a widebody with little to spare,
and it then retried long-haul flights it couldn't pay for. A player could fall into the same trap:
nothing warned that a purchase would leave too little to fly the aircraft.

## Decision

- **`FlightEconomics.TypicalLegKm(type)`:** the length of a type's usual flight, from its route
  band, capped by its range.

  | Band | Usual flight |
  |---|---|
  | Regional | 300 km |
  | Domestic | 1,000 km |
  | National | 2,000 km |
  | Tasman | 3,200 km |
  | Long-haul | 6,000 km |

- **Fleet market warning:** when buying would leave less than that flight costs, the card says so in
  amber: "Leaves $500. Its usual flight costs about $X."
- **CareerBot reserve:** each aircraft is costed on its usual flight, and the bot only buys when it can
  also fly the new aircraft once.

## Result

Report: `docs/testing/career-balance-2026-09-27/lineup/` (regenerated).

- Refusals are down from about 2,600 to 0. Minimum cash is the starting $2,119, with no rescue
  contracts.
- Pacing:

  | Style | International | Finale |
  |---|---|---|
  | Competent | 61.6 h | 142.8 h (target 110–150 h) |
  | Casual | 74.4 h | 174.7 h (was 172.6 h) |

## Verification

- `FleetMarket_WarnsWhenABuyLeavesTooLittleToFlyIt`.
- 869/869 headless tests pass; the type-check is clean.
