# 0033 — Real-metre runway circuit, no taxi

**Date:** 2026-09-10
**Status:** Accepted
**Decision owner:** Bailey (product), Cursor (implementation)

## Decision

On the bare Adelaide field the commercial loop is:

1. Arrive from the west as a distant speck (~4.2 km out, ~155 m AGL).
2. Descend on a ~3° slope, flare, and land 300 m past the west threshold.
3. Roll out and almost stop on the centreline (~1 050 m of braking).
4. Take off from that point, rotate after ~900 m, climb out past the east end.
5. Keep climbing until the model is off the 3 400 m ground; the slot then
   respawns a new arrival on long final.

Taxi-in, stand, pushback and taxi-out are skipped (`AirportCircuit.SkipGroundTaxi`).
Those phases still exist on the enum for save compatibility; they last one
second and hold the aircraft on the rollout end. Turnaround no longer gates
departure. Tyre-smoke / skid VFX are not spawned.

Flight geometry uses the real 3 100 × 45 m runway, not the 1:20 miniature
`AirportLayout` numbers.

## Reason

The previous path used ~155 m of a 3 100 m runway and then taxied to an
invisible stand. Bailey asked for legitimate scale and a smooth land / takeoff
loop with the next aircraft arriving after the last one leaves the field.

## Affected systems

- Simulation: `AirportCircuit`, `AircraftOperation` durations, `CommercialFlight`
  reservations on skipped taxi phases, `AirportSimulation` leave-phase gates
- Presentation: `AirsideFlightPath`, `AirsidePrototype.PositionFor`, follow
  camera distances, touchdown smoke disabled
- Tests: `BareFieldTests`, `PresentationLayoutTests`, `AircraftOperationTests`

Save schema unchanged. Economy still settles on `Departed`.

## Migration impact

None. Presentation and phase timing only. Existing saves load; the next cycle
uses the circuit durations.
