# 0060 — Departure climb-out actually turns after the SID establishes

Date: 2026-09-20

## Decision

Once `DepartureTurn.Blend` reaches 1 (the SID turn is established — the nose has yawed onto
the destination track and the sideways kick from `LateralMetres` has reached its full value),
any further along-track distance the departure covers is now flown on that established
heading instead of continuing straight down the original runway line. New pure function
`DepartureTurn.EstablishedTrackMetres(runway, home, destination, extraAlongMetres)` returns
the (forward, sideways) decomposition of that further distance via cos/sin of the established
yaw, in the same signed runway-local frame `LateralMetres` already uses.
`AirsidePrototype.ApplyDepartureTurn` calls it for the distance covered past
`TurnEstablishedProgress`, added on top of the already-established `lateral` offset.

Two long-standing latent doc bugs, found while working this out and fixed alongside it: the
`YawDegrees` and `Forward` comments claimed a right turn is +Z in the runway-05 frame; it is
actually −Z once Unity's rotation handedness is applied to a departing-05 forward of world
+X — the same sign `LateralMetres` already used, just not stated correctly next to
`YawDegrees`. `Forward` itself (dead code — nothing called it) had the wrong sign baked into
its actual return value, not just its comment; corrected to match.

## Reason

Bailey: "when the plane takes off... it is more realistic and actually follows a correct
flight path and doesn't crab along like it's drifting." Tracing `ApplyDepartureTurn` end to
end: `LateralMetres` and `YawDegrees` both freeze at their established value once
`Blend` hits 1 (correct — the turn itself is done), but the aircraft's underlying position
(`AirsideFlightPath.Departed`) keeps growing along the *original* runway heading, x only, for
the entire rest of the `Departed` phase — which is most of a visible departure, since the
turn establishes well before the flight leaves visual range. The frozen sideways offset was
the aircraft's *only* turn; everything after that was the nose held at the new heading while
the aircraft actually kept flying dead straight down the extended runway centreline. That
mismatch between where the nose points and where the aircraft actually goes is exactly a crab
— not a momentary artifact during the turn, but the steady state for most of the climb-out.

## Getting the sign right without being able to see it

This is Presentation code with no Unity editor available to actually watch a departure turn
in this session, and a sign error here would be a highly visible, embarrassing regression (the
aircraft turning the wrong way). Rather than trust hand derivation alone, the fix was pulled
out into a pure, unit-tested Simulation function instead of staying inline in
`AirsidePrototype.cs`:

- Derived independently from the codebase's own `RunwayWeather.TrueFromUnityYaw`/
  `UnityYawFromTrue` (world +X ↔ true 050°, Unity yaw 0 ↔ 320° true) rather than from general
  Unity lore, to confirm departing-05's world-forward is +X and to fix Unity's rotation
  handedness against a fact this same codebase already depends on elsewhere.
- `EstablishedTrackMetres_ContinuesTurningTheSameWayLateralMetresAlreadyDid` asserts the
  concrete direction (Melbourne right of 05 → further travel keeps going −Z; Perth left of 05
  → +Z), so a sign flip would fail a specific, named assertion, not just "some test somewhere."
- `EstablishedTrackMetres_StaysBoundedForANearReversalTurn` and
  `_PreservesTheStepDistance` guard the numerical shape of the fix: cos/sin decomposition was
  chosen over a simpler "keep x's pace, add a `tan(yaw)` sideways term" specifically because
  `tan` blows up approaching a 90° turn — a real possibility for a regional taking whichever
  of 12/30 the wind favours rather than the end that favours its destination (jets on 05/23
  always get the dest-aligned tie-break; regionals do not). cos/sin never exceeds the step
  distance for any angle, so a big turn foreshortens/reverses the forward component smoothly
  instead of the position exploding or flipping discontinuously.

## Consequences

- Presentation-only. No simulation timing, save data, or existing test's expected outcome
  changes — `ApplyDepartureTurn` is purely a rendering detail; phase transitions and flight
  duration are governed by `AirlineOperations`/`FleetState` timers, unrelated to this Vector3.
- `scripts/test-domain.sh`: 499/499 (494 baseline + 5 new `DepartureTurnTests`).
  `AirsidePrototype.cs`'s consuming change is Unity-only and unverified beyond inspection —
  `scripts/test-unity.sh` and watching an actual departure (ideally one with a large turn, e.g.
  Perth off 23, or any regional on 12/30) are still needed before trusting this the way
  `DepartureTurn.cs`'s own tests are.
- There is a small, accepted smoothness kink at the exact moment the turn establishes: before
  that point the sideways offset grows via a smoothstep-squared ease-in; after it, via this
  linear cos/sin projection of along-track distance. The two curves are continuous in
  *position* (verified: at zero extra distance the added term is exactly zero) but not
  perfectly matched in *rate* at that instant. Fixing that fully would mean modelling the
  whole climb-out as a proper arc, a larger change than this pass's scope — flagged, not
  hidden, and a much smaller residual issue than the bug it replaces.

## Tests

`DepartureTurnTests` (+5): `EstablishedTrackMetres_IsZeroWithNoFurtherDistance`,
`EstablishedTrackMetres_ContinuesTurningTheSameWayLateralMetresAlreadyDid`,
`EstablishedTrackMetres_StaysBoundedForANearReversalTurn`,
`EstablishedTrackMetres_PreservesTheStepDistance`, `Forward_MatchesLateralMetresSignNotANaiveSin`.
`scripts/test-domain.sh`: 499/499.
