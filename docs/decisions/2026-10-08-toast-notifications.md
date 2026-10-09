# Departure-style toast notifications

Date: 8 October 2026
Status: Implemented at Bailey’s request; Unity appearance pending

## Decision

Match notifications to the Adelaide opening identity: dark compact cards, coloured status edge and icon tile, explicit status caption, wrapped message, separate repeat count and a subtle remaining-lifetime rule. Queued notices use a 68-point slot; the opening greeting keeps its compact variant without a fabricated timer. Long text scales between 12 and 10 points before wrapping into the two-line body.

Notices enter with a 0.24-second cubic fade/rise and leave with a smooth fade over their existing last 0.6 seconds. The runtime stops the stack before it crosses a registered HUD panel or the screen margin. Existing six-second lifetime, ten-entry history, three-entry visible cap, duplicate renewal and severity producers remain unchanged.

## Reason

Bailey asked to extend the opening redesign to toast notifications. The old single-line pills had only a severity dot, appended repeats directly to the message, and stacked without bounding later rows to available space.

## Scope and migration

ToastQueue, ToastPainter/HudShell, DrawToast in AirsidePrototype.Airline and FlightViewHud toast bounds. No new assets, simulation state, save schema or migration. Short notification animation has no effect on game timing. Registered modal panels can suppress toasts while their overlay is open; history/lifetime remain as before.

## Checks

Existing AircraftStatusTests, GlassCockpitPainterTests and HudShellTests: 22/22 pass with the final font-fit adjustment. Four edited files parse with no C# syntax errors. Native Unity compilation, wrapping/font metrics, panel-safe stacking in the player, Retina appearance and animation remain unverified; no broad suites/builds/player reviews were run.
