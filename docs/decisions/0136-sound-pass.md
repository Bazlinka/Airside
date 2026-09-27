# 0136 — Sound pass: level-matched engines, engine voices, an apron soundscape

Date: 27 September 2026. Author: Claude, at Bailey's request ("improve sound pls").

## Context

The recorded engine beds (ADR 0087) did not match each other. The jet bed ran at about −8 dBFS RMS
with a loud click at the loop point (a seam step of 0.886). The Dash 8-300 twin bed sat near
−27.6 dBFS, so turboprops were almost silent next to jets. The coast bed was one short wave on a
loop, and you could hear it repeat. Every aircraft used the same pitch and loudness whatever its
size. Between flights the airport itself made no sound.

## Decision

- **Repaired beds (`scripts/audio/process_beds.py`).** The script processes the four WAVs in
  place.
  - Engines are normalised to −20 dBFS RMS, with a 0.6 s equal-power crossfade at the loop.
  - The coast is rebuilt as a 16 s bed of irregularly spaced waves at −24 dBFS.
  - The measured seams now match the typical sample step. For the jet this is 0.0056, against a
    typical step of 0.0648.
  - The asset register (AUD-002/007/008/009) records the new hashes and the processing.
- **Engine voices (`Presentation/EngineVoice`, pure).**
  - Aircraft are grouped by size: turboprop, regional jet, narrowbody or widebody.
  - Each group has a base pitch, a loudness and a hearing range. For example, an E190 is higher
    and lighter than a 737, and an A350 is deeper, louder and carries further.
  - A steady ±3% detune from each aircraft's id stops two identical jets in a queue from phasing.
  - Distance muffles the sound through a low-pass filter, so a far jet is just a rumble. Rolloff is
    logarithmic, and doppler is set to 0.35.
- **Apron soundscape (`AirsidePrototype.Soundscape`).**
  - A quiet 12 s procedural apron bed plays: ground-power hum, a low rumble and distant reversing
    beeps. It drops to 35% during curfew.
  - A terminal PA chime sounds every 4 to 8 minutes during opening hours.
  - The coast drops to 0.016, down from 0.035.
- **HUD sounds.**
  - A soft whoosh plays when a workspace opens.
  - The ambience ducks to half for 1.8 s under a tier or contract fanfare.
  - Arpeggios gain an 80 ms tail fade, so they end without a click.
- **Listening copies (`scripts/audio/render-sounds`).** This small console project renders every
  procedural sound to WAV in `docs/testing/audio-2026-09-27/`, so it can be reviewed without Unity.

## Verification

- `EngineVoiceTests` checks:
  - the class order for pitch, gain and range;
  - that detune is deterministic and bounded;
  - the low-pass mapping.
- `HudSoundsTests` covers the whoosh, the PA chime, and the apron bed looping without a seam.
- The headless suite passes, and the type-check is clean.
- Mac checks:
  - Level balance between a Saab, a 737 and an A350 at the gate and on takeoff.
  - Distant aircraft sound muffled.
  - The PA chime is not annoying over an hour.
  - The apron bed sits under everything else.
