# 0052 — Runway fairness for long holds and stand suggestion beside a Dash 8-400

Date: 2026-09-15

## Decision

1. **Tower.** Arrivals still get the runway first, with one exception: a departure that has
   held short for at least `DepartureMaxHoldSeconds` (6 min) goes first, provided it has
   waited longer than the first arrival has circled.
2. **Stands.** A new `AirlineOperations.SuggestStand` picks the stand for an aircraft waiting
   for one:
   - It takes a free stand that fits.
   - It prefers a stand that does not put a Dash 8-400 beside another aircraft on a
     `TightBayPairs` neighbour (50D/50E).
   - After that it takes the shortest taxi in, then list order.
   - It never refuses the last free stand on clearance grounds.
   Other operators taxi straight to the suggested stand, replacing the old first-fit choice.
   The player's one-click stand button now offers the same stand, labelled "Best stand".

## Why

- **Tower:** landings always won, so a steady stream of arrivals could hold a departure
  indefinitely.
- **Stands:** first-fit could park a Dash 8-400 3.3–3.5 m from its neighbour on 50D/50E,
  under the 4.5 m code C clearance (ADR 0049). A hard ban could strand an aircraft with six
  aircraft on six bays, so this is a preference only.

## Invariants and migration

- The decision uses only existing state times and stand holders. Saves gain no fields and
  need no migration.
- Timelines stay deterministic.
- A departure is promoted only once it has waited longer than the first arrival, so one
  aircraft cannot starve the other queue.
- Existing saves resume under the new rules from their next tower decision or stand choice.
- With the default Adelaide fleet this rule does not stop a second Dash 8-400 being added;
  that remains Bailey's call.

## Tests

`TowerAndStandChoiceTests` (6).
