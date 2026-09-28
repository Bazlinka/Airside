# 0162 — Airfield lights as light points, and a night brightness option

Date: 28 September 2026. Author: Claude, at Bailey's request (a play test: "improving visibility for
the player at night time … correct light fixtures with realistic visibility … visibility on the
runway").

## Context

The ERSA-accurate fixture set (ADR 0124) is modelled at true size:
- 0.34 m lenses;
- halo cards a few metres across.

From the default overview, about 2.4 km out, a lens is well under one pixel. The runway edge lines
shimmered or vanished at night, which is the opposite of a real field, where runway lights are the
most visible thing for kilometres. Ambient and exposure had already been raised twice (ADR
0063/0064); raising them for everyone a third time would wash out the look.

Runway guard lights were also steady, although real ones alternate their pair.

## Decision

- **Light points.** Every lens also emits a camera-facing point (`Airside/AirfieldLightPoint`, one
  merged mesh and material per lens group). The vertex shader:
  - opens four vertices at the lens centre into a quad that never shrinks below a minimum on-screen
    size: approach 3.4 px, guard 3.2, runway edge 2.8, taxi 2.3, stand 2;
  - dims it with distance, to half at 2.2 km, never below 30 %;
  - raises the scene's exp² fog transmission to the power 0.35, so lights reach about 1.7× as far
    as surfaces (runway visual range exceeds meteorological visibility);
  - pulls the quad towards the camera by its own size, so the ground doesn't clip it at grazing
    angles.

  Close up the point is a 0.8 m glare, so the modelled lens and its halo still read. Points are HDR,
  so the night bloom picks them up.
- **Day response.** Edge, taxi and stand points come up through dusk only. Thresholds, runway ends,
  PAPI, approach and guard lights keep a daytime point, as high-intensity lights do.
- **Guard lights flash.** They alternate left and right at 48 flashes a minute (ICAO Annex 14:
  30–60).
- **Night brightness option.** Options → Night brightness: Natural / Brighter / Brightest. It lifts
  night exposure (+0.35 / +0.7 EV) and ambient light (×1.3 / ×1.65), easing in below 0.45 daylight
  and leaving the day untouched. The default is Brighter, following this play test. Natural is the
  previous night.

## Evidence

- `AirfieldFixtureTests`:
  - `LightPoints_StayVisibleFarOutAndThroughHaze`
  - `GuardLights_AlternateTheirPair`
- `NightVisibilityTests`.
- Headless harness green. The shader is added to Always Included Shaders.

## Not verified

**Not seen in Unity.** The shader has not been compiled. The pixel sizes and strengths are first
values to tune from a night play test.
