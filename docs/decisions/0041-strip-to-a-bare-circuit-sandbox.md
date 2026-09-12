# 0041 — Strip Airside to a bare circuit sandbox

Date: 2026-09-12. Requested by Bailey: remove the objectives — flights, jobs,
tracking — and the HUD panels with them. Keep "only plane stuff like it is,
runway, plane elements and that's it". Keep pause, follow and 1×/2×/4× time,
add a working pause menu, and stop the game leaving multiple instances behind.

## Decision and reason

Airside had grown a full management sim — economy, routes, reputation,
staffing, research, stand capacity, daily reports, turnaround workflows, an
event log, an ATC phraseology engine and a ground-traffic fleet — sitting under
a bare field where none of it was visible. Two parallel HUDs (UI Toolkit and
uGUI) plus an IMGUI fallback each rendered the same fourteen panels of numbers
about a world that showed one aeroplane and a runway.

Delete that layer rather than hide it. `AirsideFocusMode` already kept the
world unspawned; the flags were suppressing chrome for systems that should not
exist. Half-measures leave ten thousand lines of dead weight and a save format
describing a game that is no longer being made.

`AirportSimulation` is rebuilt as a circuit driver: one aircraft, approach →
landing → takeoff → departed, recycling after the fly-out window. The runway
reservation, the taxi network and `CommercialFlight`'s route geometry are kept
because the visible pavement and the aircraft's ground path are drawn from
them — not because anything competes for them. `ReservationConflicts` is kept
as an invariant check: a lone airframe must never block itself, so it must stay
zero.

Persistence goes entirely. With nothing to track there is nothing to save, and
autosave, save-on-quit, the "Saved" chip, the opening briefing and the
away-summary overlay all go with it. Every launch starts fresh on approach.

The HUD becomes one IMGUI path — chosen over UI Toolkit because it needs no
`PanelSettings`, `UIDocument` or UXML assets to exist and work in a player —
carrying exactly five controls (pause, follow, 1×, 2×, 4×) and a centred pause
menu (Resume, Restart circuit, Quit). `HudLayout` is rewritten from a
four-panel stack to that control bar plus the menu, so the fits-on-screen
contract stays testable without an editor.

Speed moves from a 1×/4× toggle to three discrete rates. Selecting a rate also
clears a pause: asking for 2× while paused means go. `SimulationFrozen`
(`_paused || _menuOpen`) is the single source of truth for "time is not
moving", so aircraft, audio, touchdown smoke and apron life freeze together
instead of each testing `_paused` separately.

## Multiple instances

Three causes, three fixes. `forceSingleInstance` was `0` in player settings, so
the built app would launch alongside itself — now `1`. Quit had no path at all:
the pause menu's Quit calls `Application.Quit`, and `EditorApplication.
isPlaying = false` under the editor. The `RuntimeInitializeOnLoadMethod`
bootstrap only checked for an existing component, so a scene reload could build
a second world; a static `_active` handle now disables and destroys any
duplicate on arrival. Disabling matters because `Destroy` is deferred to the end
of the frame, and an undisabled duplicate's `Update`/`OnGUI` would run once
against a world it never built.

## Affected systems

Deleted: `AirportEconomy`, `AirportRoutes`, `AirportReputation`,
`AirportStaffing`, `AirportResearch`, `AirportCapacity`, `AirportDailyReports`,
`TurnaroundWorkflow`, `DailyFinanceBrief`, `OperationalEventLog`,
`TrafficWaitMonitor`, `AerodromeAtc`, `GroundTrafficAircraft`, the whole
`Persistence` assembly, `AirsideCanvasHud`, `AirsideToolkitHud`.

Rewritten: `AirportSimulation`, `HudLayout`, and `AirsidePrototype`'s
lifecycle, input and HUD. Slimmed: `CommercialFlight` (no turnaround,
settlement or delay state), `AircraftOperation` (no at-stand progress
binding), `AirsideFocusMode`, `Weather` (no operating cost).

Unchanged on purpose: every aircraft visual — glTF loading, gear, props,
wheels, control surfaces, lights, cabin doors, engine heat, skid marks,
touchdown smoke — the Adelaide ground, pavement and perimeter, the terrain
field, the camera controller, and the day/weather presentation. Dormant
environment and prop animation code that only touches `Transform`s was left in
place behind its existing focus-mode flags; it is unspawned, not broken, and
removing it is a separate cleanup.

## Migration impact

Existing `airside-save-v1.json` files are ignored and never read or written
again; the file is left on disk rather than deleted. Nothing else persists.

## Acceptance and evidence

`scripts/test-domain.sh` **89 passed, 0 failed** on .NET 8 (down from 243 — the
154 removed tests covered the deleted subsystems). The surviving suite keeps the
circuit, taxi-segment release, runway-holding-position, departure-reset,
determinism-under-large-and-small-timesteps, reservation-atomicity, pavement,
terrain and bare-field coverage.

`HudLayout`'s new arithmetic was additionally checked against a `Rect`/`Mathf`
shim at 1280×720, 1440×900, 3456×2168, 800×500, 400×780 and 320×240: the bar
and menu stay inside the viewport, controls stay inside the bar in order, none
collapses to zero width and none overlaps. Those cases are committed into
`PresentationLayoutTests` so Unity runs them too.

**Not verified here:** `AirsidePrototype` and the rest of Presentation need
UnityEngine and cannot be compiled on this Linux VM, and no UnityEngine shim is
committed. The strip was done by removing whole methods and then sweeping for
surviving references — every removed member has zero remaining call sites and
every member the new code calls is defined — but a real Unity compile,
`scripts/test-unity.sh`, a Mac build and a packaged-player look at the control
bar, pause menu, 2× rate and single-instance quit are all still required before
this is trusted.
