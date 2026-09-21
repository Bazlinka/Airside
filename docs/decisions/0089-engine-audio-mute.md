# 0089 — Engine audio mute and takeoff-loop cacophony

Date: 21 September 2026. Bailey, on the #366 play build: engine sound is
terribly broken, and mute does nothing.

## Decision

Mute sets `AudioListener.volume` / `pause` immediately (M and Options),
not only the current aircraft's `AudioSource.volume`. Recorded takeoff
beds no longer loop on parked aircraft, are fully 3D (no 25% 2D bleed),
keep native pitch, and do not auto-play on spawn.

## Reason

The CC0 beds are takeoff recordings. Pitching them to ~0.47 for idle,
playing every airframe at once, and mixing 25% 2D made a wall of broken
motors. Mute only zeroed sources `UpdateEngineAudio` happened to visit,
so follow-camera and leftover sources kept going.

## Affected systems

`AirsidePrototype` engine/master mute. Engine WAV import metas
(`preloadAudioData`, mono). No save change.

## Migration

None.
