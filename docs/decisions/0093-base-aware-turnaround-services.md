# 0093 — Base-aware turnaround services

Date: 21 September 2026.

## Decision

Player departure preparation remains one deterministic simulation chain, extended to:

1. Fuel
2. Catering
3. Baggage
4. Boarding
5. Ready for pushback

No staff roster, second currency or independent service scheduler is introduced in this slice.

## Base effect

The persisted PlayerBaseLevel changes service throughput for the whole chain:

- Starter: 100% of authored baseline time.
- Expanded Regional: 90%.
- Jet Gate: 80%.
- International: 70%.

Terminal-gate aircraft retain the existing 1.5x aircraft-size factor before the base multiplier.

The simulation's scheduled prep start, next-event calculation, pushback readiness and departure-ready
time all use the same base-aware total. Fleet, Operations and the selected-aircraft card use the same
stage calculation.

## Persistence

No schema change. PrepStartedAt remains the saved time anchor; PlayerBaseLevel is already persisted
by save v12.

## Invariants

- No new player action is required to manually click each service.
- A better base speeds operations but never skips a service stage.
- AI turnaround behaviour is unchanged.
- Pushback remains impossible until all four player stages are complete.
