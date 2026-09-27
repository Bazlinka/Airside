# 0143 — Weather that drifts, one sky, fog that matches it, better clouds

Date: 27 September 2026. Author: Claude, at Bailey's request ("improve cloud logic as well and fog
making things more immersive"). Bailey chose drifting weather.

## Context

- **Weather jumped.** Weather was an independent hash for each hour, so clear could follow a
  storm, fog could turn up at 3 pm, and every change snapped instantly.
- **The sky ignored the weather.** It was one flat colour whatever the weather.
- **Two fog paths competed.** One set a soft day fog, the other a weather fog. They used different
  colours and densities, and the fog colour never matched the sky.
- **Fog hid distant arrivals.** On a clear day fog left things 90% faded at 9.5 km, which hid the
  distant arrivals of ADR 0142.
- **Flat clouds.** Clouds were 16 cards billboarded fully to the camera, so from above they read as
  flat cut-outs. They slid at a fixed speed, popped when they wrapped round, and cast their
  shadows straight down whatever the sun.

## Decision

- **Drifting weather (`Weather.At`, Simulation, still pure and deterministic).**
  - Each hour is a Markov step along Clear ↔ Cloudy ↔ Overcast ↔ Rain ↔ Storm.
  - Most hours stay put: 40–72% depending on the state. Most changes go to a neighbour, and 5% of
    hours jump anywhere.
  - Fog forms only between 04:00 and 09:00 local, from a clear or cloudy sky, and lifts after.
  - The chain restarts from a hashed state 48 hours before each local-day anchor, so any hour costs
    at most 72 steps and is the same whichever hour is asked first. Days are cached.
  - Over 20,000 hours the mix is about 37% clear, 28% cloudy, 17% overcast, 11% rain, 4% fog and
    2.5% storm, close to the old shares.
  - Storm ground stops still read `Weather.At`.
- **Blended look (`Weather.LookAt`).** Each new hour's look eases in from the last over 15 minutes
  of game time. `WeatherLook` gains `Lerp`, `VisibilityMetres` and `FogDensity`:
  - visibility runs from about 900 m in fog, through 2.6 km in a storm and 9 km in rain, to 60 km
    on a clear day;
  - density is √3 / visibility, so exponential-squared fog is 5% at the visibility distance.

  Live weather still overrides.
- **One sky (`Presentation/AtmosphereLook`, pure).**
  - One colour drives the camera background, the horizon dome and the fog. It is clear blue,
    grey-blue under cover, rain grey, storm slate, pale fog, or deep night. Dusk colour is
    smothered by cloud.
  - `ApplyDayCycle` sets the only fog. The second, weather-only fog branch is removed.
  - Fog density comes from visibility, eased to 20% for a camera above 1,500 m. From the overview,
    the field still shows through fog while the ground looks misty, and a clear day's 20 km final is
    not fogged away.
- **Layers (`AirsidePrototype.Atmosphere`).** All are soft procedural noise on unlit transparent
  quads.
  - **Overcast sheet.** An 18 km sheet at 1,100 m. It follows the camera across, fades in with cloud
    cover and fades out as the camera climbs toward it, so it never hides the field from the
    overview.
  - **Horizon band.** A ring of cloud 14 km out round the camera, thicker under cover and tinted
    from the sky.
  - **Low mist.** Three sheets at 12, 26 and 42 m over the field. They appear in fog, in rain and
    on clear dawns, and are fainter for a high camera.
- **Clouds.**
  - Cards turn about the vertical and tip only 35% toward a high camera.
  - Rain and storm clouds are darker bodies (`CloudShade`), and every third storm cluster towers.
  - Drift follows the real wind speed.
  - Cards fade out near the wrap edges and back in on the far side.
  - Shadows are cast along the sun direction.

## Verification

- `WeatherTests` covers:
  - neighbour-mostly drift, with under 8% of changes being jumps;
  - the overall mix;
  - fog only in the morning window;
  - the same answer whichever hour is asked first;
  - the look easing without a snap and identical at any step;
  - fog density against visibility.
- `AtmosphereLookTests` covers:
  - the sky carrying the weather (overcast greyer, storm dark, fog pale, night dark);
  - fog matching the sky;
  - density following visibility and easing for a high camera, with the field visible from the
    overview in fog and a 20 km clear final not fogged;
  - the layers following the weather, with no overcast sheet over the overview camera and dawn mist
    but no dusk mist;
  - continuity between weathers.
- Storm tests move from hour 34 to hour 249, a one-hour storm on the new chain.
- The swatch render is in `docs/testing/weather-2026-09-27/`. The headless suite passes and the
  type-check is clean.
- Mac checks:
  - clouds from the overview and from a low camera;
  - the overcast sheet appearing only below it;
  - the horizon band;
  - morning fog with mist and the field still readable;
  - storm darkness;
  - no cloud pop at wrap;
  - shadows following the sun;
  - frame rate.
