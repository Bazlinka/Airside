# Decision 0011: airline route proposals

Date: 2026-09-06

## Decision

Airlines propose scheduled services to the airport (`AirportRoutes` in the
simulation layer). One proposal stands at a time: it is offered on a fixed
schedule and lapses after an offer window if the player does not accept it.
Accepting a proposal records an `AcceptedRoute` and adds its per-flight payment
to a running total; every completed flight then pays `Routes.IncomePerFlight` on
top of the turnaround revenue.

Accepting is a player command (`accept-route`), persisted like the priority-crew
command. It carries no payload — it means "the player accepted the proposal
standing at this second". Because proposal content is a pure function of the
simulated timeline, replaying to that second reconstructs the same proposal, so
the command applies identically on reload and during offline catch-up.

## Reason

This is the first decision that builds the airport up over time rather than
recovering a single flight — the start of the progression the game is about.
Keeping it to one standing offer with a clear payment keeps the first version
legible and testable.

Generating proposals from the timeline rather than the random source matters:
adding a call to the shared `IRandomSource` would have shifted every existing
seed-based outcome (stand assignment, disruptions, the fifty-cycle soak). The
timeline is already deterministic and already replayed exactly.

`AirportRoutes.Update` replays every offer window due by the given time, so a
large time step lands in the same state as second-by-second stepping — the
frame-rate invariant holds for offline catch-up.

## Consequences

- `AirportEconomy` gains `AddRouteIncome`; it is only called when income is
  positive, so games with no accepted routes are unchanged (existing economy
  tests still pass).
- `AirportSimulation` gains `Routes` and `AcceptPendingRoute`;
  `PersistentAirportSession` gains `AcceptRoute` and replays the command.
- The HUD shows the standing offer with an Accept button and the accepted-route
  count.
- Not yet modelled: the proposed flights actually flying (cadence still fixed at
  one primary flight), reputation gating which proposals appear, or declining a
  proposal explicitly. Those follow.
