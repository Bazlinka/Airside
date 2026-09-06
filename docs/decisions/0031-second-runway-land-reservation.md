# Decision 0031: second-runway land reservation

Date: 2026-09-06

## Decision

Introduce `AirportLand`: a one-off purchase (`ReserveSecondRunwayLand`,
10000, `reserve-second-runway-land` command) that reserves the adjacent plot
for a future second runway. The reservation is the entire feature at this
stage — `SecondRunwayLandReserved` is not yet read anywhere else in the
simulation. No save-schema field; reconstructed by command replay.

## Reason

Project-plan step 21 calls for land alongside incidents, maintenance,
surface access, finance and analytics, "one at a time." The plan's building
loop names land banking explicitly as a real trade-off in its own right
("buying adjacent land may protect space for a second runway while delaying a
nearer-term terminal improvement") — the reservation itself, not anything it
unlocks yet, is the described player decision. That makes it a genuinely
complete, self-contained slice rather than a half-built stub: a future
second-runway construction system will consume this flag, but its absence
does not make today's purchase incomplete, the same way buying land in
reality does not require building on it immediately.

This was chosen over the other systems in step 21 (incidents, maintenance)
because those need a periodic cost or event injected into the daily
settlement loop or turnaround timing, which several existing tests assert
exact values against over multi-day and soak-length runs
(`DailyReportTests`, `StaffingTests`, the concurrent-flights and capacity
soaks) that this session has no way to re-run after the change. A pure
reservation flag with no coupling to any other system carries none of that
risk.

## Consequences

- `AirportLand` is a new plain-C# simulation class alongside `AirportCapacity`.
- `AirportSimulation.ReserveSecondRunwayLand()` spends and sets the flag,
  refusing if already reserved or insolvent — same shape as `BuildThirdStand`.
- `PersistentAirportSession` gains the `reserve-second-runway-land` command.
- **No HUD button this round** — same operations-panel height-budget reason
  as decision 0030 (cargo). The command is real and callable; it just has no
  button yet.
- Maintenance/incidents (the other step-21 candidates) remain explicitly
  deferred to a session that can run the full EditMode suite after adding a
  cost that recurs across the timeline — see the "Do this next" note this
  change adds to `GAME.md`.
- Second-runway construction, surface access and analytics dashboards remain
  future slices.
