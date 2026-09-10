# 0030 — Aircraft motion read from the presentation clock, aircraft-only focus

**Date:** 2026-09-10
**Status:** Accepted
**Decision owner:** Cursor (under Bailey aircraft-logic pass)

## Decision

1. **Presentation evaluates phase progress at the fractional clock.**
   `AircraftOperation.PhaseProgress(now)` is `(now - PhaseStartedAt) / duration` —
   a pure function of time. Presentation therefore evaluates it at the fractional
   presentation clock (`AirsideAircraftMotion.PhaseProgress`) instead of reading
   the whole-second simulation clock. There is no interpolation buffer, no
   per-aircraft sample history and no lag: the position is exact at every frame.
   The result must stay clamped to `[0, 1]`, because that clamp is what keeps a
   departure held for traffic parked at the hold-short bar rather than sliding
   onto the runway ahead of its clearance.

   An interpolate-between-two-past-samples design was tried first and rejected:
   `StateAt` reports progress 0 at a phase boundary, so blending across the
   boundary froze the whole first second of every phase.

2. **Two deliberate fall-throughs to the simulated second.** `AtStand` progress
   is bound to turnaround work rather than the phase clock, and a flight waiting
   on a reservation has its phase clock pushed forward once per stalled second.
   Reading either at a fractional instant would creep forward and snap back, so
   both read `operation.PhaseProgress(_clock.Now)`.

3. **Speed profiles, not smoothstep, on position.** `Mathf.SmoothStep` has zero
   velocity at both endpoints, which reads as the aircraft stopping dead at
   rotation, at the flare and at the threshold. Air and ground paths are
   parameterised by arc length through `DistanceFraction(t, startSpeed, endSpeed)`.
   Phase geometry is tuned so speed is continuous across every seam when measured
   in **metres per real second** — never distance per unit of progress, since
   Takeoff lasts 15 s and the fly-out 6 s, so equal per-progress steps are a 2.5x
   lurch.

4. **One holding position, shared.** `AirportTaxiNetwork.RunwayHoldingPositionZ`
   (6.5 m) is the single source for the painted hold-short bar and the reservation
   boundary. `RunwayHoldingProgress(route)` converts it to route progress. The
   number must not be forked into presentation, or a departure gets cleared
   through an arrival that has not vacated.

5. **An arrival holds the runway until it is past that line.** `CommercialFlight`
   keeps `AirportSimulation.Runway` for the first ~23% of `TaxiIn`. Releasing it
   the instant the rollout ended let a departure be cleared and start its roll
   while the arrival was still on the centreline. `Atc.NotifyRunwayVacated` stays
   at the Landing→TaxiIn transition — the phrase is advisory, the reservation is
   the hard gate.

6. **A line-up turn onto the centreline.** Taxi-out ends at the hold-short bar
   pointing up the A1 chord and takeoff used to begin pointing down the runway: a
   143° heading snap in one frame. A constant-radius arc over the first 40% of the
   takeoff phase joins the centreline instead.

7. **Aircraft-only focus mode.** `AirsideFocusMode.AircraftOnly` parks ground
   vehicles, stand equipment and people. It is one switch with three derived
   readers, kept as a switch rather than deleted code so the ground fleet can come
   back once the aircraft loop reads correctly.

8. **The horizon dome is a backdrop, not geometry.** It renders on the Background
   queue with `_ZWrite 0`. As an opaque depth-writing sphere it occluded aircraft
   beyond it, so lengthening the approach would have made "planes appear out of
   nowhere" worse rather than better.

## Reason

At 1x and 4x nothing on the field read as an aircraft. Measured before the pass:
1475 of 1499 taxi frames and 885 of 899 takeoff frames did not move at all, then
jumped 1.86 m and 20.32 m respectively; the landing rollout was slower than a
taxi; departures teleported to a fixed point and froze; and two aircraft could
occupy the runway at once.

## Affected systems

- Simulation: `AirportTaxiNetwork`, `CommercialFlight` runway reservation window
- Presentation: `AirsideFlightPath`, `TaxiVisualPath`, `AirsideAircraftMotion`,
  `AirsideFocusMode`, `AirsidePrototype`, `AirsideCameraController`
- EditMode `PresentationLayoutTests`, `AirportSimulationTests`

## Migration impact

No persisted schema change, so no save migration. The runway is held marginally
longer, which is a stricter reservation and cannot introduce a conflict; a
40-cycle two-flight soak asserts at most one aircraft on the runway per second
with zero reservation conflicts. Frame rate still must not change simulation
outcomes: the fractional read is presentation-only and the simulation continues
to tick whole seconds.

## Follow-up (do not stop)

- **Not yet done, and it is the largest remaining item:** the airfield still has
  one A1/A2 chord and a single runway exit. A departure holding short and an
  arrival vacating still pass close on A1, and the existing design releases the
  corridor on purpose to avoid deadlocking them. The real answer is a parallel
  taxiway, multiple runway exits and a longer runway on a larger map — a
  simulation topology change, not a presentation tweak, so it needs its own
  decision record and its own reservation model.
- Nothing in this pass has been through the Unity editor; there is none on the
  machine it was written on. Markings, hold-short bars, edge lights and the new
  430 m approach need Mac Play eyes at overview and follow, day/dusk/night.
- Restore ground vehicles and people behind `AirsideFocusMode` once the aircraft
  loop reads correctly.
