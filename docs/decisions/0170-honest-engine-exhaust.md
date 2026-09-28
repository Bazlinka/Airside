# 0170 — Engine exhaust as haze, with a start puff

Date: 28 September 2026. Author: Claude, at Bailey's request ("visible engine exhaust" from the
list of next steps).

## Context

The exhaust behind each engine (ADR 0151) was tinted orange and got brighter and more orange with
power. A turboprop at takeoff looked like it had an afterburner. Real turbine exhaust has no
visible colour: running, it's a faint heat haze. The one moment it is plainly visible is a start,
when the turbine lights off and throws a short puff of grey-white smoke.

A true heat-distortion shader needs URP's opaque texture. That is off in both pipeline assets and
would add a full-screen copy every frame, which the game can't afford without a measurement on
the Mac (ADR 0155). It is not done here.

## Decision

- **Colour.** `AirsidePropellerDynamics.ExhaustTint(power, lightOff)`: running, a neutral
  warm-grey haze at 2.5–7.5 % opacity, thickening slightly with power; at light-off, blending to a
  34 % grey-white puff.
- **Size.** `ExhaustScale`: a puff is wide and short; power stretches the plume aft.
- **Build-time colours.** The kit and fallback plume colours are the same neutral grey, not orange.

## Evidence

- `PropellerDynamicsTests.Exhaust_IsAHazeNotAGlow_AndAStartThrowsAPuff`, run against Unity shims.
- Headless harness 961/961.

## Not verified

**Not seen in Unity.** The puff comes from how close the engine fraction is to light-off, so an
engine running down through the same point may also puff. That was true of the old bloom too.
