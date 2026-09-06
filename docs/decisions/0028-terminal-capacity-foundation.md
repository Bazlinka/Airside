# Decision 0028: modular terminal foundation (check-in capacity)

Date: 2026-09-06

## Decision

Introduce `AirportTerminal`, a passenger-processing capacity model independent
of stand geometry: check-in desks (baseline 2) each cap 6 scheduled
flights/day, so the baseline terminal caps out at 12/day — identical to the
existing two-stand schedule cap (`AirportRoutes.FlightsPerStandPerDayCap ×
BaselineStandCount`). `AirportSimulation.MaxScheduledFlightsPerDay` becomes
`min(stand-based cap, terminal capacity)`, and `AcceptPendingRoute` refuses
when accepting the standing proposal would exceed terminal capacity, in
addition to the existing stand-schedule check.

A single buildable expansion (`ExpandCheckInHall`, 6000, `expand-checkin`
command) raises capacity to 18/day — matching the three-stand cap after
`BuildThirdStand`. Desk count is reconstructed by command replay, no new save
field.

Because the baseline exactly matches the pre-existing stand-based cap, no
current behaviour changes until a third stand is built: the terminal only
becomes the binding constraint once stand capacity outgrows it, which is the
intended shape (buying a stand and expanding the terminal are now two
separate decisions with two separate payoffs).

## Reason

Project-plan step 19 ("Add modular terminal and passenger-flow foundations")
and the plan's own module description ("modules expose capacity, connections,
construction stages and operating requirements") call for terminal capacity to
be a system on its own, not folded into stand count. This is the minimal
foundation: a capacity number the player can expand, ahead of a full
passenger-cohort/baggage system.

## Consequences

- `AirportTerminal` is a new plain-C# simulation class alongside
  `AirportCapacity`; same pattern (baseline, cap, buildable expansion, no save
  field).
- `AirportSimulation.MaxScheduledFlightsPerDay` and `AcceptPendingRoute` now
  consult both stand and terminal capacity.
- `PersistentAirportSession` gains `ExpandCheckInHall()` / the
  `expand-checkin` command, following the `build-stand` pattern exactly.
- No HUD button is wired yet — this lands in the domain/simulation layer only.
  Presentation (a button and status line, mirroring the stand-3 button) is
  left for a session that can visually verify the OnGUI layout in the Unity
  editor; this change did not have editor access to do that safely.
- Full modular terminal buildings, passenger agents/cohorts and baggage remain
  future slices (project-plan steps 19's remainder and step 20).
