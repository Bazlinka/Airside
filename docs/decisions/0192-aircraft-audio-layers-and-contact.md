# 0192 — Aircraft engines build with power, and landings have contact and rollout

Date: 30 September 2026. Author: Codex, at Bailey's request for an audio pass on
all aircraft, including increasing revs and realistic landing noise.

## Decision and reason

The previous three recorded beds were played as one loop per aircraft. A narrow
pitch/volume change could not represent idle, takeoff load or reverse. Approach
and landing had a forced 55% power floor, and max(left,right) made the second
engine start inaudible. Touchdown used a short synthetic chirp on a shared,
partially 2D source, so overlapping landings could move each other's sound and
far-away contacts could bleed into the overview.

Give each of the 13 flying catalogue types its own representative profile and
derived idle, loaded and reverse beds. Crossfade continuously with existing
shaft power / smoothed jet N1 and the two engine-start fractions. Governed props
principally change spectrum/load; jet pitch follows N1 over a wider range. Pitch
and gain changes ease in real time. No new throttle or engine state is simulated.

Each aircraft owns five spatial sources: idle, power, reverse, rolling tyres and
contact. A registered CC0 tyre recording supplies staggered wheel spin-up and a
small project-authored oleo thump. Rolling noise uses actual ground-leg tyre
speed and stops after the type's rotate point. Reverse is permitted only after
that type's visual touchdown and stows before taxi-in. A contact edge detector
consumes events while muted and suppresses a late-restored landing. No
synthetic rotate whoosh remains; the engine and tyre layers carry the departure.

Sources use distance low-pass filtering, a bounded rolloff curve, modest
Doppler and priorities favouring loaded engines/contact. Hidden/cold/distant
voices stop. Aircraft range scales by class; all remain 3D with no 2D bleed.
Authored and live sky aircraft use the same engine presentation.
The static Bell 412 is parked with engines off and stays silent.

## Sources and limitations

The old AUD-007/008/009 beds stay byte-identical and remain the fallback. They
are family recordings, not exact recordings of 13 different engine variants.
`docs/data/AIRCRAFT_AUDIO_PROFILES.json` contains sound-design tuning, not
measured aircraft specifications. Reverse adds project-authored filtered
turbulence to the family recording; it is not labelled as a recorded reverser.
The original tyre preview, decoded source and complete derivative hash manifest
live under `docs/data/audio/`; AUD-010/011/012 record licence, cost and fallback.

`scripts/audio/generate_aircraft_audio.py` generates the 41 mono PCM clips and
profile C# reproducibly. The local byte check uses NumPy 2.0.2. Existing CI covers the pure mix/contact tests. Loop seams, levels
and peaks are checked before import. No unlicensed simulator sound is used.

## Affected systems and migration

Aircraft audio presentation and its fleet/sky hooks, Resources AudioClips,
asset register, Flight Manual credits, headless file list and audio verification.
No save version or migration. Schedules, runway/taxi/stand reservations, timing,
physics, visuals and other ambience do not change.

## Verification

Deterministic mix/contact tests cover all types, both starts, governor/jet rev
differences, type-specific contact/rotate, airborne reverse rejection, repeated
landings, late restore and frame-rate-independent smoothing. Unity tests load
every new clip and check spawn/mute/hide/distance silence and source spatiality.

The built player has an explicit developer-only audio review flag that bypasses
career/world creation and never writes a player save. It captures the actual
Unity listener DSP for startup, idle, taxi, takeoff, climb, approach, touchdown,
reverse, rollout, shutdown and mute. Numerical audit checks levels and silence;
Bailey's listening review remains the assessment of perceived realism.
Final evidence is in `docs/testing/audio-2026-09-30/README.md`.
