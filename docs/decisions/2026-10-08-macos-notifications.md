# Important background macOS notifications

Date: 8 October 2026
Status: Implemented at Bailey’s request; native Mac verification pending

## Decision

Use a project-authored Objective-C++ bridge to Apple UserNotifications in the Airside app. Notifications are local, with the app’s own Notification Centre identity/icon. No AppleScript, shell message interpolation, remote service, new package or push credentials are needed at runtime.

Bailey selected important background events only: player arrivals waiting for a stand; late settlements; completed/expired contracts; career goals, operating tiers, challenges and milestones. Routine departures, arrivals that need no action, AI traffic, news and daily reports retain their existing in-game behaviour without Mac banners. Native notices only run while the app is running and unfocused. They do not run after quitting or wake a sleeping Mac.

Options → Notifications contains an opt-in switch, macOS permission status/settings shortcut and SEND TEST action. Default is off. Enabling requests Apple’s notification permission; denial does not break the game or toasts. Returning sessions restore the PlayerPrefs setting. Airside Sound controls requested notification sound; macOS banner/Focus settings still govern delivery. A deliberate test may display in the foreground; ordinary notices remain suppressed there. Clicking a native notice activates/unminimises Airside.

Background bursts are grouped into one banner at most every ten seconds. Duplicate event keys are suppressed for sixty seconds. The pending buffer holds eight details plus an overflow count; summaries show three details and the remaining count. Returning to the game or disabling clears undelivered background messages. Delivered Notification Centre history is left under macOS control.

## Background event handling

Career-event presentation now drains in the existing airline update hook instead of HUD drawing, so it works with a minimised window. Simulation already runs in background (`runInBackground: 1`). Existing event cursors, settlements, toasts, celebration cards, saves and simulation timing remain unchanged. This feature does not replay historical event logs after restoring a save. Delays are reported from completed flight settlements, not speculative live predictions.

## Build integration

`MacNotificationsBuild` compiles the original `scripts/native/airside-notifications.mm` into a universal arm64/x86_64 bundle using Apple’s macOS SDK before Unity packages/signs a Mac player. macOS 11+ and Xcode command-line tools are required. The generated bundle is imported for StandaloneOSX only, with Editor compatibility disabled; generated binaries/import metadata are git-ignored. Build failure is explicit if native compilation/import fails. The runtime safely falls back to in-game toasts for missing plugins, non-Mac platforms and Editor sessions.

Source scripts live under `scripts/`; no downloaded binary/framework/asset enters the repository. Player notification preference uses a new PlayerPrefs key, with no game-save schema migration.

## Evidence and remaining checks

Pure notification-selection/deduplication/burst/clear tests plus Options painter checks: 8/8 passed. Edited C# syntax parses pass; shell syntax passes. Native settings/Options coverage is updated but not run in Linux. The new source metadata GUIDs and generated harness list are present.

Not verified: Objective-C++/framework compilation, Unity prebuild plugin import/inclusion, the signed player’s P/Invoke loading, first permission prompt/denial/re-enable, macOS banners/sound/Focus, click activation and background/minimised delivery. No Mac build, native Unity tests, broad suites or rendered/player journeys run under the standing repository policy. Next Mac build must verify the bundled plugin and test banner before treating delivery as accepted.

Primary API/build references:

- https://developer.apple.com/documentation/usernotifications/unusernotificationcenter
- https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications
- https://docs.unity3d.com/Manual/PluginsForDesktop.html
