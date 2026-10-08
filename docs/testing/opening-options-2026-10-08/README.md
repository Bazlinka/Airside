# Opening and Options — 8 October 2026

Task #601; main base `8f735ae3`; branch `codex/opening-options-20261008`.
Save-recovery PR #600 remains separate.

## Changes

Wider title card, clearer fresh/returning wording, 2.8-second entry (previously
4.2 seconds), earlier skip hint and an Opening animation preference. Options is
split into General, Camera, Display and World, keeping all fifteen existing
controls and adding opening animation plus the existing cockpit-motion control.
Setting descriptions explain immediate versus next-launch effects.
Title Back/Escape closes Options directly; Escape on the title itself stays there rather than exposing an in-game Resume menu; the running game's Back returns to
its menu. Existing persistence and live-setting application paths are preserved.

## Verification

- Required full headless suite: **1,989 passed / 3 existing failures**, 1,992 total; Unity NUnit 3.5 compile check passed. Exact failures: `headless-results.txt`.
- Initial full native Unity: **2,518 passed / 5 failed / 2 inconclusive / 1 skipped**, 2,526 total. Three are the unchanged ground-separation/night-sky failures reproduced on main. Two were obsolete assertions requiring the old 846-point menu and exactly 4.2-second intro; both were updated to check the new behaviour. Initial and final focused evidence: `native-results.json`.
- Final affected native rerun after the title-Escape fix and assertion updates: **152 passed / 0 failed**, covering opening/menu/settings/setup/painter/layout/frame-pacing classes, including all nine new cases.
- Final affected headless painters: **20 passed / 0 failed** (`headless-focused.txt`). The entire native suite was not repeated after these focused corrections; it is not reported as fully green.
- Metadata audit passes: 1,886 unique GUIDs, 407 identical runtime art mirrors, 70 character materials. `git diff --check` passes.

New coverage: button/text bounds at 800×600, 1024×640 and 1440×900; title/in-game
Back labels; native all-section control coverage; title/in-game close routing;
opening preference default/read behaviour with exact preference restoration.
Existing title/setup painter cases cover all five supported window sizes.

## Native visual review

`OpeningOptionsReview.Capture` renders the actual shared HudPainter in a Unity
Editor window at the runtime HUD scale. It uses an explicitly synthetic saved
airline and current option defaults, without starting the game or writing saves.
All twelve fresh/returning title and Options views at 1024×640 and 1440×900 were captured and inspected. JPEG evidence is in `native/`; original PNGs remain in ignored `work/opening-options-native-fixed/`.
The first ReadScreenPixel attempt clipped the capture and missed image textures; those images were rejected. Capturing the actual IMGUI content rectangle with macOS screencapture produces complete views and both runtime art textures load successfully. The helper uses sample defaults and draws the background without disabling its sample buttons; the actual modal runtime keeps those buttons inert. This is native UI evidence, not a packaged gameplay test.

Reproduce with a graphical editor:

```
Unity -projectPath game/Airside -executeMethod OpeningOptionsReview.Capture -openingReviewOutput work/opening-options-native
```

Packaged pointer/keyboard interaction, actual player save/continue and the animated
handoff remain separate runtime checks. No whole-game performance claim.
