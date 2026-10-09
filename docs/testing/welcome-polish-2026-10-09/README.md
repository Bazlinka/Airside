# Welcome and comfort fixes — 9 October 2026

Task #706; owner Codex / Bazlinka. Presentation and input only. Operational
weather/delays, live time, economics, reservations, aircraft and save schema are
unchanged. Pre-existing PC_RPAsset/packages-lock edits are excluded from the PR.

## Ten player-visible wins

1. Welcome, setup and title Options no longer render live fog/cloud/rain/lightning over the opening surface. Weather returns on entry to the airport.
2. Weather Layers off now also hides distance fog and rain, rather than leaving those paths running; operational weather remains active.
3. F1 opens the Flight Manual from the title, as advertised, and closes it again.
4. M toggles sound from the title menu with visible on/off status; typing M in setup remains normal text.
5. A mouse click can skip the entry glide even when no keyboard device is present.
6. Flight-code editing accepts the existing ASCII alphabet and allows a genuinely empty edit; deletion no longer resurrects the suggested code, and invalid codes still block Next.
7. Short welcome windows omit the large title block when needed so the complete action card stays above the footer; footer shortcuts retain usable width.
8. A readable backup recovered by Continue is now explicitly identified on the returning welcome card, with room for its warning.
9. Options descriptions and controls retain full row spacing; short windows scroll the rows while tabs and Back remain available. Ordinary windows retain device-pixel text.
10. Opening Options from follow starts its Views customisation on Follow instead of silently targeting Overview. The selector still allows either view.

## Checks and native evidence

- Focused headless: 25/25 setup, splash and Options checks passed, including code deletion, compact warning/action bounds and full row spacing.
- Generated harness check current; diff whitespace check passed.
- Unity 6.3.23 Mac build succeeded. Native welcome weather/current-follow Options regressions passed 2/2. Final committed-source app rebuild follows merge.
- Actual packaged UI inspected through computer-use screenshots at an 800×600 macOS window under forced Fog: returning welcome, F1 manual, F1 close, M off/on, title Options General/Views, Identity setup and a cleared code. Next disabled with the existing validation message; Escape returned to title. Sound preference restored; no career was started or overwritten.
- Forced Storm welcome inspected at 1280×800: no rain/cloud/fog/lightning overlay obscured the title or controls. A separate private-save Fog airport session rendered the weather over the airport again; its actual captured frame was inspected. The capture helper exported that PNG upside-down (capture orientation, not a claimed game-camera fault).

The UI screenshots above are observations in the task session, not exported PNG
fixtures. The initial review package carried a dirty stamp because the user's
existing local renderer/lock edits were deliberately preserved. Build success
is separate from visual/interaction checks; no full suite, long journey, FPS or
GPU-performance claim is made. Backup warning bounds are covered by painter tests;
a real personal save was not corrupted to test recovery.
