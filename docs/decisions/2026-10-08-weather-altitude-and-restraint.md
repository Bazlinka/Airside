# Persistent weather at flight altitude and restrained cockpit motion

Status: Accepted. Date: 2026-10-08. Owner: Codex / Bazlinka. Task: #666.

Cloud coverage/morphology revised by [2026-10-09-cloud-continuity-and-variety](2026-10-09-cloud-continuity-and-variety.md). Other altitude/audio decisions remain.

## Decision

Keep the bounded presentation weather renderer, with sixteen local volumes and the
existing shaders/fallback atlas. Develop every third volume into a broad thunderstorm
tower and anvil, extending from roughly 1 km to 10 km. Lower and upper deck surfaces
share world noise: lower surfaces show from below, lit tops show from above. The
observer's sky clears above storm tops rather than the ordinary 1.6 km stratus top;
actual tower proximity controls immersion and slow gusts between those heights.
Liquid rain fades by 4.5 km in storms, before the cold upper cloud. All camera views
share altitude-aware fog/sky/rain; interior rain audio and wipers use that envelope.
Ground fog clears the observer around 90–300 m while its depth-integrated bank remains
visible on the ground. Weather presentation eases live/fallback/sample changes over
12 seconds. Operational weather and airport rules retain their existing inputs.

Shift cloud geometry and lightning with the flight origin. Evaluate fog/ceiling noise
in absolute coordinates and integrate wind travel, avoiding resets at origin changes
or a new wind heading. Distribute cloud reveal thresholds across the existing grid.

Gate deterministic lightning cadence on displayed weather. Reuse one bounded jagged
channel, illuminate the struck cloud and reduce the global sky flash, especially
above the storm. Schedule thunder from channel distance at 343 m/s with at most eight
independent delayed claps; later strikes cannot replace pending thunder. Existing
audio resources and safe unlit fallback are retained.

Remove the extra rapid camera shake and continuous trackpad rumble/joint tapping.
Reduce engine/runway visual buzz and use weather-driven slow, eased gusts, while
retaining touchdown, nose-wheel, gear and thrust/braking motion. The existing
Vibration switch still disables the camera motion and discrete haptics.

## Reason

A single shallow deck made thunderstorms clear at low altitude. The old origin shift
moved the camera without its clouds; the shader's wind multiplied by all elapsed time
also jumped textures when wind changed. Duplicate shake and synthetic clear-air
buffet made otherwise steady flight uncomfortable. Lightning followed authored
simulation storms even when the displayed live weather differed.

## Scope and compatibility

Presentation weather/motion, three existing shaders, a presentation-gated overload
of Lightning.StrikesAt and focused regressions. Extends ADRs 0193/0226; supersedes the
shallow storm envelope and continuous vibration choices in ADR 0214. No save/schema,
operation, route, economy, external data or art asset changes. Original code, zero cost.
This remains one rendered weather sample across the flight, not spatial meteorology
or a full fluid/atmospheric simulation. Storm height is representative, not live
measured cloud-top data.

## Evidence and open checks

90 focused headless tests pass (83 motion/envelope/haptics/lightning/coverage/weather
policy, 7 atmosphere); six existing static producer/shader contracts pass. Changed
C# is syntax-parsed with the available Roslyn SDK; this does not compile UnityEngine
code. Generated harness and presentation map checked/refreshed. No Unity shader
compile, native game run, rendered review, packaged build or GPU performance check.

Bailey's next Mac playtest: fog at ground level and after climbing above 300 m;
storm below/inside/above its tops; exterior and cockpit transitions; a long journey
crossing a floating-origin boundary; live weather on/off; quiet cruise, turbulent
clouds, landing/gear thumps and Vibration off. Check transparency sorting, tower
silhouettes, deck crossings, lightning/thunder alignment and frame time.
