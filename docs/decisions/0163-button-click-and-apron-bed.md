# 0163 — Button click and the apron bed

Date: 28 September 2026. Author: Cursor, after Bailey said he does not like the beep, and that the
sound needs to be improved.

## Context

Every button played Kenney `ui_select_005`, a 0.38 s interface tone, including the Sound option
on the title screen and in the pause menu. The apron bed (ADR 0136) also mixed in a 1150 Hz
reversing beep, twice per 12 s loop.

## Decision

- Buttons play `HudSounds.UiClick`: a few dozen milliseconds of filtered noise and a low thump,
  at 0.7 on the UI source. The Kenney clip stays in the asset register and on disk, and is no
  longer loaded.
- `HudSounds.ApronBed` is the rumble and the ground-power hum only. The reversing tone is removed.
  Curfew still drops the bed to 35%.

Engines, the coast, the PA chime, the panel whoosh and the celebration stings are unchanged.

## Evidence

`HudSoundsTests` covers the tick (energy in the first 40 ms) and the apron loop. A listen on the
Mac build is the check for whether the click sits under the airport.
