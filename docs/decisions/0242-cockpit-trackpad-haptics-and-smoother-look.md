# 0242 - Trackpad haptics, either-button look and eased view changes in aircraft views

Date: 2026-10-06. Owner: Claude (Bailey's request).

## Decision
- **Trackpad haptics.** `CockpitMotion` already models touchdown, nose slam, lift-off, gear thumps, spoilers,
  runway joints and turbulence for the camera shake. It now also reports a haptic strength per event and a
  continuous `Rumble01`. `CockpitHapticScheduler` (pure, unit tested) spaces them out, merges queued taps,
  echoes a hard landing and ticks a rumble. `MacTrackpadHaptics` plays them through the system's
  `NSHapticFeedbackManager`, reached via the Objective-C runtime every macOS process already links. No plugin or
  third-party code is added, so there is nothing to record in the asset/data register. Other platforms and any
  failure are silent no-ops. The existing **Vibration** toggle governs shake and trackpad together.
- **Look input.** Either mouse button drags the view (right-drag was awkward on a trackpad and nothing else is
  clickable inside an aircraft view). Per-frame pointer and scroll movement is bounded, and drag sensitivity
  follows the zoom and the camera-speed setting.
- **Transitions.** Entering a seat, switching seats and leaving an aircraft view glide over 0.9 s, including the
  field of view, instead of cutting.
- **Hints.** The dock hint, a once-per-run entry toast and the F1 help describe the aircraft-view controls.

## Reason
Looking round with a trackpad was awkward, drags could whip the view round, view changes snapped, and nothing
said how to move inside the aircraft.

## Affected systems
Presentation only: `CockpitMotion`, `CockpitHaptics`, `MacTrackpadHaptics`, `CockpitLookInput`,
`CockpitControlHints`, `AirsideCameraController` (+ `.Cockpit`), `AirsidePrototype.Cockpit`, `FlightViewHud`,
`ControlsHelp`. No simulation, save-format or persisted-setting changes.

## Migration impact
None. Left button no longer does nothing in an aircraft view; it looks.
