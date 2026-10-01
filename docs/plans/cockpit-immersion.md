# Cockpit immersion candidate — 1 October 2026

Player outcome: smoother wheel/trackpad zoom (35–85 degree FOV), seat view shortcuts,
optional subtle engine/rolling/cloud vibration, destination, distance from Adelaide
and rendered vertical speed. Cloud-deck fog rises smoothly between 900 and 1100 m,
remains dense to 1350 m, and clears by 1600 m; precipitation fades above its top.
This is a stylised presentation envelope around the existing 1100 m stratus sheet,
not a measured weather volume. Existing type-specific engine, wheel/reverse and
contact voices remain the sound sources; interior filtering applies to the selected
aircraft only. No new audio asset or claim of recorded cockpit accuracy.

Scope: cockpit camera/HUD, aircraft audio selection, existing sky/rain presentation,
pure cloud envelope and deterministic tests. Simulation, schedules, route poses,
reservations, random draws, time pacing, economy and save schema remain unchanged.
No manual flight controls or translated eye movement through the fitted interior.

Acceptance: 1 forward/reset zoom, 2 left, 3 panel, 4 right, 5 overhead; right-drag
and scroll work with mouse/trackpad and obey menu/HUD ownership. Repeated entry/exit
restores external camera. Vibration checkbox immediately disables angular motion.
Readouts follow actual rendered aircraft including floating-origin transitions.
Ascend/descend through cloudy/rain/storm deck without a hard fog/rain cut; clear
weather has no deck fog. Native Unity compilation, all-type window/zoom review,
flight/landing listening and performance checks remain required before merging.

Validation: pure cloud envelope tests and full headless suite; asset metadata and
harness audit. This Linux worker has no Unity editor or Mac player; native checks
and perceptual acceptance are explicitly outstanding.

## Authorised merge and follow-up

Bailey requested commit/merge and continued work. PR #517 merged on 2026-10-01,
with its outstanding native/packaged acceptance explicitly retained.

Next player outcome: cockpit sound no longer includes the non-spatial apron bed
or terminal PA; own-aircraft voices have no Doppler pitch shift. Original filtered
noise supplies a quiet airflow/ventilation bed, rising with rendered ground speed
(a presentation proxy, not measured airspeed or recorded model-specific audio).
Arrows look, +/− zoom and Home faces forward. Presets and drag ease into position;
mouse pitch respects invert-orbit. The vibration checkbox persists across launches.

Scope: existing soundscape, emitter Doppler selection, transient cockpit airflow
source/clip, pure airflow generator/tests, cockpit camera and one PlayerPrefs key.
Existing exterior sound, aircraft recordings/synthesis, weather, simulation,
reservation/route/time/economy and save schema remain unchanged.

Acceptance: no apron bed/terminal chime during cockpit flight, including a chime
already playing when entering; exterior ambience resumes on exit without catch-up
chimes. Airflow is quiet at taxi and builds at flight speed, fades smoothly with
speed and silences immediately on mute. Rebind/exit/destruction release its clip
and source; no accumulating voices across entry cycles. Own engine/reverse/wheel
voices retain pitch on floating-origin travel; exterior Doppler returns on exit.
Keyboard navigation obeys menu ownership and clamps to the existing seat limits.
Check settings relaunch; native EditMode audio regression, all-type cockpit inputs,
listening and frame-time acceptance remain required.
