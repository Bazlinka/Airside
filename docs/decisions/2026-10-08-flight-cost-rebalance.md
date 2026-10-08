# Flight costs rebalanced upward

Date: 2026-10-08

## Decision

Per-flight dispatch cost rises: a flat 120 base (minimum 180) plus per-km 1.45 (turboprops), 1.65 x weight x running factor (jets) and 1.0 (helicopter), up from a 70 base (minimum 90) and 1.12 / 1.28. Flight pay, aircraft prices, starting funds and checks are unchanged. Bailey chose "game dollars" and "don't over-complicate" (plan: `docs/plans/economy_realism_plan.md`, option B).

## Reason

A Saab repaid its price in about 9 flights; cash never constrained the fleet ladder. A starter hop (KGC) now keeps about 80 of 364 pay instead of 169, and jets earn 20-30% on good legs, so purchases take many more flights.

## Affected systems

`FlightEconomics.DispatchCost` and everything built on it (planner forecasts, refunds, relocation, recovery contract). Pay-based contract terms are unchanged.

## Migration impact

None to saves: dispatch cost is computed, not persisted. Standing costs, fuel prices and loans are not in this change. Balance is unverified against the 100-150 hour pacing target (ADR 0120); run the career bot before further changes.
