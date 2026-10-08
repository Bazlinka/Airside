# macOS notification registration and recoverable permission setup

Date: 8 October 2026
Status: Implemented; native Mac verification pending

## Decision and reason

Bailey reports Airside absent from macOS Notifications. The installed build and permission status are not yet confirmed; the remote Mac is offline, so this is not a verified root-cause diagnosis.

Before requesting permission, register the actual player app bundle with the public Launch Services `LSRegisterURL` API. Retain the existing bundle identifier and opt-in preference. The permission row requests/retries when not asked or failed, opens System Settings once allowed/blocked, and waits during an active request. Do not open System Settings before a first request and imply Airside should already be listed.

Distinguish unavailable Unity/non-Mac sessions, missing native plugin, and native request failure in Options help. Native registration, permission and exception errors log their details. No notification database reset, automatic permission grant, AppleScript or new app identity.

The build hook now fails if Unity omits the compiled notification executable from the packaged app. The local build script no longer changes Info.plist after Unity has signed the player: those edits invalidated the signed bundle. Git build identity is still stamped before Unity packages it and displayed in-game; Finder uses Unity’s application version.

## Affected systems and migration

Native bridge/compiler, Unity Mac build hook, local build script, notifications Options and runtime wrapper. No simulation/save migration, changed bundle identifier, preference reset or changes to important-event selection. Builds predating the notification bridge cannot gain it by opening System Settings; install a new built player.

## Verification

Bounded shell syntax and changed-C# syntax checks only. Objective-C++/CoreServices compilation, Unity packaging, signed-player loading, first permission prompt, settings list, banners and click activation require the next Mac build. No Mac build or broad tests run under standing policy.

References: https://developer.apple.com/documentation/coreservices/1442827-lsregisterurl and https://developer.apple.com/documentation/usernotifications/asking-permission-to-use-notifications
