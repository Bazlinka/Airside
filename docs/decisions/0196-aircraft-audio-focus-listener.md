# 0196 — Aircraft heard from the camera focus point

Date: 30 September 2026. Author: Cursor, after Bailey reported he could not hear the
ADR 0192 aircraft audio. Approved by Bailey ("hear from the point the camera is
looking at on the ground").

## Context

ADR 0192 put the `AudioListener` on the camera and stops each aircraft's voices beyond
its class hearing range (turboprop 1,000 m, regional jet 1,300 m, narrowbody 1,700 m,
widebody 2,200 m). The default overview camera orbits at **2,400 m**, so from the
view the game opens in every aircraft was culled and the player heard only the 2D
wind/coast/apron beds. The audio was only audible in follow camera or close zoom.

## Decision

- Aircraft are heard from a dedicated **focus listener**: the camera's ground orbit
  point (`AirsideCameraController.FocusPoint`), lifted 6–60 m (a quarter of the orbit
  distance), facing the camera's yaw so left/right panning matches the screen. The
  camera's own `AudioListener` is disabled.
- Zoom distance fades aircraft voices instead of culling them: full level up to 300 m,
  `(300 / distance)^0.58` beyond (~30% at the 2.4 km overview, ~5% at the 45 km limit).
- ADR 0192 ranges, rolloff, low-pass and hidden/cold/distant stop rules are unchanged;
  they now measure from the focus point.
- The packaged audio review harness keeps its own fixed listener at unity zoom gain.

## Affected systems

`AirsidePrototype.AircraftAudio`, `AirsidePrototype.Lights` (camera setup),
`AircraftSoundEmitter.Apply` (new zoom-gain argument), `AircraftAudioMix`.

## Migration impact

Presentation only. No simulation, command or save change.
