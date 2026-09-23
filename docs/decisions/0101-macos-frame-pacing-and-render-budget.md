# 0101 — macOS frame pacing and render budget

Date: 22 September 2026.

## Decision

The Mac player paces its frames to the game rather than to the panel:

- **Focused:** capped at 60 fps. The cap uses a vsync divisor (`QualitySettings.vSyncCount`),
  not `targetFrameRate`, so frames stay on vblank and native fullscreen never tears. 60 Hz → 1,
  ProMotion/120 Hz → 2, 144 Hz → 2 (72 fps), 240 Hz → 4. Options → **Frame rate · Display max**
  restores every-vblank rendering for players who want it. Stored in PlayerPrefs
  (`airside.settings.uncappedfps`), not the save file, so no save migration.
- **Not focused:** about 30 fps (divisor for 30), whichever option is set. The project keeps
  `runInBackground`, so the live airport keeps running and saving. Automated soak runs are exempt
  because they never own focus.
- The display rate is re-read once a second and on every focus change, because the window
  can move between the built-in panel and an external display.

The render budget also drops work that bought nothing visible:

- **Landing lamps cast shadows only at night.** Each shadowed spot is another shadow-caster pass
  every frame. In daylight the sun's key shadow hides a lamp's shadow.
- **Static tint passes skip unchanged frames.** Airfield light renderers, and the terminal and
  hangar window glow by day, are only re-tinted when daylight moves by ≥ 0.002. The night
  flicker still animates every frame.
- **Per-aircraft light lookups are cached.** The lights pass ran `GetComponent<Light>`,
  `GetComponent<Renderer>` and a by-name `Transform.Find("White strobe")` for every lamp on
  every visible aircraft every frame. It also read `gameObject.name` (a fresh string) twice per
  glow pane. All of these are now resolved once.

**SSAO stays at full resolution** (`PC_Renderer` `Downsample: 0`). Half-res AO was tried as a
budget cut and made the field read flat on Retina overview/follow; Bailey asked for a
visually nice Mac build, so the soft AO is not worth the save. Frame pacing and the
CPU/light skips above remain the Mac optimisation path.

## Why

On an M1 Pro with a ProMotion panel, `vSyncCount = 1` rendered the full HDR, SSAO, MSAA and
SMAA stack at 120 fps. That doubled GPU work and heat for a real-time airport where nothing
moves fast enough to benefit. With `runInBackground` on, the game also kept rendering at full
rate while it sat behind the player's other windows. Simulation time is read from the wall
clock, so frame rate never changes outcomes (design invariant). Pacing is therefore purely a
power and thermal choice.

## Affected systems

Presentation only: `AirsideFramePacing` (new), `AirsideRuntimeQuality`, `AirsideSettings`,
`HudLayout.OptionsHeight` (one more Options row), `AirsidePrototype` light/glow passes and the
`PC_Renderer` asset. Domain, Simulation, saves and replay are unchanged.

## Verification owed

EditMode covers the divisor, focus/soak targets, tint gate, lamp shadow rule and Options fit.
Still owed: a packaged look on the ProMotion panel. Check the frame rate (60 focused, ~30
unfocused), that full-res AO reads clean at overview and follow, and that dusk lighting
transitions stay smooth. If Unity's Metal player does not honour a vsync divisor above 1,
the fallback is `vSyncCount = 0` plus `targetFrameRate`.
