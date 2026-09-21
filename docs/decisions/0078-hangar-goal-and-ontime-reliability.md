# 0078 — Hangar goal on the objective card, on-time reliability

Date: 21 September 2026. Follow-on to ADR 0077 and Bailey's "keep improving
game mechanics" — make the ATR climb readable, and tie ops quality to career
money without daily drip or per-passenger fiction.

## Decision

1. **Hangar goal on the objective card.** When there is no active contract,
   the persistent objective shows the next hangar step from
   `AircraftAcquisition` (usually the ATR 42): title "Save for an …", progress
   `$funds of $price · N of M rotations`, and an imperative that points at
   flying or accepting a contract. Active contracts still own the title and
   rotation progress. `TryBuyHint` still only fires when every gate clears.

2. **On-time pushback → reliability.** When a player aircraft leaves the stand
   for TaxiOut, lateness vs the booked `DepartAt` is recorded. At settlement:

   | Lateness | Reliability |
   |---|---|
   | ≤ 2 minutes | +1 (on time) |
   | 2–5 minutes | 0 |
   | 5–15 minutes | −1 |
   | > 15 minutes | −2 |

   Flat `FlightPay` still uses `ReliabilityMultiplier` only; contract bonuses
   still pay exactly what they advertised. AI flights are not scored.

3. **No daily/hourly auto-pay, no per-passenger pricing, no insolvency** in
   this cut. Pay remains dispatch-up-front / pay-on-return (ADR 0055).

## Reason

ADR 0077 made "start small → earn the ATR" the opening fantasy, but the
objective never showed hangar shortfalls — only a buy hint after every gate
cleared. Reliability barely moved outside contracts, so the existing pay
multiplier rarely mattered. Punctuality uses the booked push the player already
sees, without inventing passenger counts.

## Affected systems

`CareerProgress.NextAircraft`, `OperationsSummary`, `FlightEconomics`
punctuality helper, `FleetAircraft.PushbackLatenessSeconds`,
`AirlineOperations` pushback + settle, `AirlineCareerState` reliability apply,
`AirlineSave` v10.

## Migration impact

Save version **10**. v9 restores with `PushbackLatenessSeconds` unset (null) —
mid-trip flights from old saves skip punctuality for that one settlement.

## Guardrails

- Do not restore airport-manager daily wages or insolvency.
- Do not invent passenger load for pay.
- Contract advertised numbers stay exact.
- Keep MaxPlayerAircraft at 4.
