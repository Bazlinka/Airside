# 0155 — Graphics test switches

Date: 28 September 2026. Author: Claude, at Bailey's request ("fix the graphics slowdown", then
"go ahead and add the toggles").

## Context

The last graphics-on measurement (GAME.md, 2026-09-26, PR #409) had the normal Adelaide package at a
steady **60 fps**, p95 16.8 ms, once the aircraft glazing was batched. The "≈ 29 fps, p95 over 33 ms"
repeated by ADR 0152 and ADR 0154 came from the 2026-09-25 runs *before* that fix and is stale. No
graphics-on run has been recorded since. Several heavier effects have been added in between:

- ADR 0143 weather layers: an 18 km overcast sheet, a 24-segment horizon band and three low mist sheets
  of 6.5 × 5 km, all alpha-blended, several of them full-screen when active (fog, rain, dawn).
- ADR 0148/0151 propeller and fan blur discs: a transparent quad per propeller or fan.
- ADR 0142 distant-aircraft glows: a camera-facing transparent card per aircraft beyond about 6 km.
- Real-time lights on every aircraft (nav, wingtip strobe, beacon, landing and taxi spots), up to
  about seven per aircraft; landing lamps cast shadows at night (ADR 0101).

Past blind trials (shadows off, renderers hidden, lower quality, GPU Resident Drawer off) were reverted
without a result, and this session has no Mac or GPU to profile on. Guessing again would repeat that.

## Decision

Add one switch per effect, all **on by default** so nothing changes until a player turns one off:

| Option (Options menu, "Graphics tests") | Setting | Off means |
|---|---|---|
| Weather layers | `WeatherLayers` | overcast sheet, horizon band and mist hidden |
| Propeller blur | `PropellerBlur` | no blur discs; blades stay visible and turn |
| Distant aircraft glow | `DistantGlows` | no far-off glow cards |
| Aircraft lights | `AircraftLights` | no real-time aircraft lights; the lamp lenses still glow |

They are saved like the other options. For a soak A/B without touching saved options:

```
Airside -airsideSoak -airsideSoakMinutes 5 -airsideGraphicsOff weather,propblur,glows,lights
```

(`all` switches all four off.) Options set by the flag are not written back to PlayerPrefs.

## How to use it

Run a soak with everything on, then with `all` off. If the frame rate doesn't move, none of these is
the cause. If it does, bisect with one effect at a time and fix that effect properly. The switches are
a diagnostic, not the fix; once the cause is found, the fix should make the default look fast.

## Evidence

The changed files parse as C# 9 (Roslyn). Settings defaults and the flag parser have EditMode tests in
`AirsideSettingsTests`; `OptionsMenu_FitsTheFrameRateRow` covers the taller menu (`OptionsHeight`
552 → 760). **Not compiled or run in Unity here.** `scripts/test-unity.sh` and a Mac run are needed.
