# 0169 — Wet-runway light reflections

Date: 28 September 2026. Author: Claude, following the night-lighting work (ADR 0167).

## Context

On a wet night a real runway mirrors its lights: every edge, centreline and approach light lays a
long bright streak across the pavement towards you. It's the most recognisable thing about an
airfield in the rain. The game darkened and glossed the pavement when wet (VFX-004). The lit Lit
material's reflection probe picked up the apron floods, but not the hundreds of small runway and
taxiway lights.

## Decision

- **Streaks from the light-point mesh.** Each lens group's light-point mesh (ADR 0167) is drawn a
  second time with the same `Airside/AirfieldLightPoint` shader in reflection mode (`_Reflect`). The
  quad lies on the ground under the fixture and runs from it towards the camera, 9× the point's
  size. It is brightest under the light and fades and narrows towards the viewer.
- **Pavement never hides it.** It is pulled 0.35 m towards the camera.
- **Shared behaviour.** It carries the same distance dimming, haze and guard-light flashing as the
  point.
- **When it shows.** Strength is `ReflectionStrength(wetness, night)`: nothing below 0.1 wetness,
  full from about 0.6, and only at dusk and night. The clear-weather residual damp on paved slabs
  (0.14) therefore never mirrors. It is 45 % of the light's own strength.
- **Cost.** One extra draw per lens group, and only while wet at night.

## Evidence

- `AirfieldFixtureTests.WetRunway_ReflectsTheLightsOnlyWhenItIsWetAndDark`.
- Headless harness.

## Not verified

**Not seen in Unity.** The shader change has not been compiled. Streak length, width and gain are
first values to tune in rain at night.
