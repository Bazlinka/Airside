# Decision 0006: second aircraft, priority and yield

Date: 2026-09-06

## Decision

A second aircraft (`GroundTrafficAircraft`, id `GT-201`) shuttles back and forth
along the two shared taxi segments (A1, A2) while the primary flight runs its
cycle. It reserves those segments through the same `ReservationTable`, so the two
aircraft genuinely contend for the taxiway.

The primary flight has absolute priority. On every simulated second the second
aircraft first releases any segment the flight needs this tick, the flight then
takes its reservations, and the second aircraft moves into whatever space is
left. A blocked move is held and, after ten seconds, explained by the existing
`TrafficWaitMonitor` (keyed by `GT-201`).

Reservation requirements are now computed against the per-tick simulation time
(`_lastUpdatedAt`), not the outer clock, so a taxiing aircraft reserves the
correct segment during offline catch-up as well as live play.

## Reason

Phase five needs simultaneous traffic on the reservation system before broader
content. A full N-aircraft scheduler is a large change; a single opposing
aircraft with a fixed priority rule exercises the shared-segment contention and
the wait diagnostics now, stays deterministic, and does not disturb the proven
single-flight cycle (fifty-cycle soak, economy, persistence).

Absolute priority for the primary flight keeps `ReservationConflicts` at zero:
the flight is never blocked, so the existing conflict-free guarantee still holds.
The interesting behaviour — one aircraft waiting for another — happens on the
second aircraft and is visible through the monitor.

## Consequences

- A future real scheduler will replace the fixed priority rule with per-aircraft
  intent and negotiation, but must keep the progressive segment release and the
  diagnostic event.
- `ReservationConflicts` still means "the primary flight was blocked" and must
  stay zero in the soak test. Second-aircraft waits are counted only in the
  traffic wait monitor.
- The presentation layer renders `GroundTraffic.Position` as a second aircraft on
  the taxiway.
- Save compatibility is unchanged: the second aircraft's motion is a pure
  function of elapsed simulated seconds and the reservation table, so it is
  reconstructed exactly on load with no new persisted fields.
