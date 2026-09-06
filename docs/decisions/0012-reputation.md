# Decision 0012: airport reputation

Date: 2026-09-06

## Decision

The airport carries a reputation score, 0–100, starting at 50 (`AirportReputation`
in the simulation layer). Every completed departure moves it: an on-time flight
adds 2, a delayed flight subtracts 2 plus a quarter of the delay in seconds
(capped at 8). It exposes a band label (At risk / Provisional / Established /
Trusted) and an income bonus.

Reputation gates and prices airline route proposals. Each proposal states a
`ReputationRequired`; a proposal cannot be accepted below it. When a route is
accepted, its per-flight payment locks in a bonus of `3 × (score − 50)` — so a
well-run airport is offered the same routes for more money.

## Reason

Reputation is the feedback loop that makes on-time operations matter beyond the
immediate delay cost, and the gate that makes route proposals a progression
rather than a list. It is the last core piece of "first playable airport" before
the accepted routes actually start adding flights.

Reputation is derived entirely from replayed departures, so — like the economy —
it is reconstructed exactly on load and during offline catch-up with no persisted
field. The reputation bonus is applied at acceptance time (not proposal-generation
time) so proposal content stays a pure function of the timeline and the
large-vs-small time-step parity of the route schedule is unaffected.

## Consequences

- `AirportSimulation` gains `Reputation`; it records each departure alongside the
  economy settlement.
- `AirportRoutes.Accept` now takes the current score and an optional bonus, and
  refuses proposals above the airport's reputation.
- The HUD shows the score and band, the required reputation on an offer, and
  disables the Accept button when the airport does not qualify.
- Next: accepted routes add real scheduled flights; declining a proposal; and
  reputation effects on demand and disruptions.
