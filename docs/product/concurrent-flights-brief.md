# Design brief: accepted routes add real flights

Status: **not started** — needs a design pass (ChatGPT) before implementation.
Owner: unassigned.

## Why this is a decision, not just a task

Today `AirportSimulation` runs exactly **one** commercial flight, looping
back-to-back (~7 flights per simulated day, no idle gap). Accepting airline routes
raises `Routes.ScheduledFlightsPerDay` but nothing acts on it — the airport does
not actually get busier. Making routes add flights means **more than one
commercial flight operating at once**, which is the core architectural step the
project plan has been building toward and deliberately deferring until the single
loop was stable. It is now stable (48 edit-mode tests, deadlock-free multi-aircraft
ground traffic, persistence, economy, reputation).

The single flight is currently entangled with: `ActiveTurnaround`,
`AssignedStand`, `ActiveTaxiRoute`, the economy settlement, reputation, the event
log, `_flightSettled`, `CompletedCycles`, and the `DepartureResetSeconds` reset.
The ground-traffic fleet (`GroundTrafficAircraft`) already solved shared-resource
contention with a priority rule and a single-file corridor lock, but it assumes
**one aircraft has absolute priority and everything yields to it** — that
assumption breaks with two co-equal commercial flights.

## What "done" looks like

- The number of commercial flights operating scales with accepted routes: 0 extra
  routes = today's single loop; more routes = 2, then 3 concurrent flights, up to
  a capacity limit (stands, runway).
- Each flight has its own arrival, stand, turnaround and departure, settles its
  own revenue/delay/reputation, and appears in the event history by id.
- Two flights never deadlock and never occupy the same segment or stand; when
  they contend, one waits and the wait is explained (reuse `TrafficWaitMonitor`).
- Deterministic, frame-rate independent, and reconstructed exactly on load /
  offline catch-up — same bar as everything else.
- Existing guarantees hold: fifty-cycle soak, economy figures, persistence tests
  (some will legitimately change and need re-baselining — call those out).

## Open design questions for the pass

1. **Promotion vs parallel model.** Make the primary flight the first element of a
   `List<Flight>` and generalise, or keep the primary loop and add a separate
   "scheduled arrivals" system beside it? The former is cleaner long-term but
   churns ~15 tests.
2. **Priority between commercial flights.** First-come-first-served on the runway
   and taxiway? A slot/schedule? How does the ground-traffic fleet's priority
   rule change when there is no single "the flight"?
3. **Capacity.** Two stands today. Does exceeding stand capacity block new route
   acceptance ("needs another stand"), queue arrivals in a hold, or both? This
   is the hook for the plan's first buildable upgrade (a third stand).
4. **Cadence.** How does `ScheduledFlightsPerDay` translate to actual spawn
   timing without making the airfield visually chaotic at the greybox scale?
5. **Turnaround / economy per flight.** `TurnaroundWorkflow` and the delay-cost
   settlement are per-flight already; confirm they compose when several run at
   once.

## Suggested first slice (once the design lands)

A **second** commercial flight only, gated on `ScheduledFlightsPerDay >= N`,
sharing the reservation system, with a fixed rule (e.g. the earlier-arriving
flight holds priority). Prove two flights coexist for fifty cycles without
deadlock or a reservation conflict, then generalise to N and add capacity.

## Hand-off

Paste this brief to ChatGPT with `AGENTS.md` and `GAME.md`, ask for a task packet
per question 1's chosen model, then bring the packet back to Cursor or Claude.
