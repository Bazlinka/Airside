# Decision 0001: deterministic simulation boundary

Date: 2026-09-06

## Decision

The domain and simulation assemblies contain airport rules without Unity scene dependencies. Unity presentation code reads the resulting state and smoothly interpolates aircraft movement. Simulation time is whole seconds from an injected clock. Random choices use the project's seeded generator rather than Unity or platform randomness.

Runway, taxiway, apron and stand occupancy use stable identifiers and an atomic reservation table. The current single-aircraft runner exercises the same reservation boundary intended for later traffic.

## Reason

This keeps live play, offline catch-up and automated tests consistent. It also lets future Mac and iPhone code exchange versioned state without treating rendered positions as game truth.

## Consequences

- Visual frame rate cannot advance simulation rules directly.
- New random behaviour must receive an `IRandomSource`.
- Persistent identifiers cannot be based on scene instance IDs.
- Multi-aircraft traffic must use the reservation table before entering shared infrastructure.
