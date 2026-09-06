# Decision 0002: turnaround dependencies and delay accounting

Date: 2026-09-06

## Decision

An aircraft turnaround is a dependency-driven workflow owned by the simulation. Passengers disembark, baggage unloads, fuel is added and the cabin is cleaned before baggage loading and boarding can finish. The initial schedule allows 45 seconds at stand. A seeded cabin-cleaning disruption extends the workflow and produces a named delay.

The airport starts with $25,000. A completed flight earns $1,200, while delay costs $40 per second. During an active turnaround the player may spend $300 once to assign a priority crew, which reduces remaining task durations.

Unity reads task state to display service vehicles and the operations panel. It does not decide task completion, delay length or financial results.

## Reason

This turns the first aircraft loop into a small management game: the player can see a developing problem, understand its cause and decide whether recovering the schedule is worth the cost. Keeping the workflow in the deterministic simulation also prepares it for offline progress and CloudKit synchronization.

## Consequences

- Every disruption needs a player-readable explanation.
- Money changes must pass through the economy model.
- Future upgrades may change task capacity or duration, but cannot depend on frame rate.
- Offline catch-up must replay or summarize the same turnaround decisions and results.
