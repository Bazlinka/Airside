# 0142 — Arrivals from further out

Date: 27 September 2026. Author: Claude, at Bailey's request ("plane animations should be coming in
from further away").

## Context

Arrivals appeared 18 km out, on a dead-straight final, at no more than 3,000 ft. Several limits
made even that hard to see:

- the camera's far clip was 10 km;
- the aircraft LOD group culled any aircraft under 2% of the screen height, so a 737 more than
  about 2 km from an overview camera vanished;
- fog left a clear day 90% faded at 9.5 km.

## Decision

- **`Presentation/ArrivalApproach`** holds the approach geometry as pure numbers.
  - **Show distance.** An arrival is drawn from 32 km, up from 18 km.
  - **Height.** It follows the 3° glideslope, capped at 6,000 ft, up from 3,000 ft. At 32 km the
    path is under the cap, so the aircraft is already descending when it appears.
  - **Joining the final.** Beyond 12 km the arrival curves in from the side its origin city lies on,
    by up to 6.5 km. The side comes from the great-circle bearing, Adelaide to origin, against the
    runway's true heading. An origin straight behind the final gives a straight-in approach. The
    curve grows with the square of the distance beyond 12 km, so it meets the final with zero
    slope and no kink.
  - `AirsidePrototype.ArrivalFinal` uses this path, fixing the side when the arrival is first drawn.
- **Far clip** is 30 km (`AirsideBareField.CameraFarClip`).
- **LOD.** The far LOD cutoff drops from 2% to 0.4% of the screen height, so a jet on a 10 km
  final is still drawn.
- **Distant light (`AirsidePrototype.DistantLights`).** Beyond 6 km every aircraft carries a soft
  camera-facing glow, using the ADR 0124 halo material.
  - It is kept about 7 px across whatever the distance.
  - It is bright white with landing lights on, and dimmer and warmer without.
  - It is outside the LOD group, so a speck of an aircraft is still seen.
- **Fog.** Visibility-based fog (ADR 0143) keeps the far final visible on a clear day.

## Verification

- `ArrivalApproachTests` covers:
  - the show distance and far clip;
  - the height cap, and that the aircraft is still descending at the show point;
  - the joining curve being continuous and kink-free;
  - the side logic: Kingscote straight in, Sydney fully to one side, Perth and Melbourne on
    opposite sides, Darwin on the left of 05;
  - true bearings;
  - the distant light only far out.
- The headless suite passes and the type-check is clean.
- Mac checks:
  - arrivals visible far out on the final and curving in from their city's side;
  - no pop where the curve meets the straight;
  - frame rate with the 30 km far clip;
  - depth fighting on apron paint, since the near clip is unchanged;
  - distant lights at dusk.
