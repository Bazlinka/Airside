## Where to resume

**Working ground teams (9 Oct, Codex, #774 / PR #776):** airport-provided larger service teams;
shared bag allowance and physical carrying trips determine minimum player baggage
preparation, including equipment setup and clearing. Reuses shipped characters/tools.
Fuel/catering reserve setup/clear windows within existing budgets. Ambient airlines
receive more visible workers; their scheduling is unchanged. No hiring/economy/save-schema
changes. 160 focused/related checks pass, final native service frames inspected;
clean universal bbe40abd build, nine booking/save/view actions and 40× ADL–KGC
round trip pass, zero runtime errors. Whole subsequently merged main not rebuilt.
Decision: `2026-10-09-airport-ground-teams`; evidence: `docs/testing/ground-crew-2026-10-09.md`.


**Landing gear improvements (9 Oct, Codex, #757):** actual top-attachment pivots, one retained
gear timeline, clear door/leg sequencing, centred nose steering during fold, enclosed
stowed wheel envelopes and widebody bogie beams carried with their axles/wheels.
Missing widebody leaves fitted to existing bay/fuselage skin; ATR sponsons stay on the body.
47 headless and 75 native focused checks pass; all 15 airframes inspected up/down/mid,
with driven extension probes. Clean universal build/A350 view checks and 40× ADL–KGC
round trip pass; no runtime errors. Return gear close-up/performance unverified.
Simulation, paths, datums and saves retained.
Evidence/limits: `docs/testing/landing-gear-2026-10-09/README.md`.
Fleet intake/control refinements remain verified as recorded in `docs/testing/fleet-refinements-2026-10-09/README.md`.

**City and town lights (9 Oct, Codex, #760):** warm varied windows on existing
Adelaide/regional building facades; urban street sources/pools and mapped lamp
extensions; distant lights only on mapped built-up land. Existing airport lighting,
layouts, simulation and saves retained. Household occupancy and unsurveyed lamp
spacing are inferred; regional detail remains limited to shipped OSM snapshots.
Final clean universal Mac build and nine native steps pass, zero runtime errors;
day/dusk/night and horizon frames inspected. Focused 40× Whyalla arrival/origin
probe has no runtime errors; regional close windows/GPU cost remain unverified; `docs/testing/city-town-lights-2026-10-09.md`.

**Directional twilight sky (9 Oct, Codex, #758 / #773):** cool zenith/horizon gradients,
sun-facing amber/rose dawn and dusk, blue-hour evenings; broad orange fog reduced.
Existing celestial clock, weather/altitude, stars, night readability and saves retained.
Refinement narrows amber, softens dawn, adds an opposing rose band and cools blue hour.
Clean universal bb4a17ea build and 31 native steps pass, zero runtime errors; before/after
cycle, follow, opposite horizon and tower-weather frames inspected. Seasonal extremes/GPU
performance unverified; combined subsequent gear main not rebuilt.
Evidence: `docs/testing/sky-refinement-2026-10-09/README.md`.
Decision: `2026-10-09-directional-twilight-sky`.

**Tower cab and rain motion (9 Oct, Codex, #756):** original interior ceiling, window
framing and low controller consoles now enclose the existing tower eye. Rain retains
normal fleet-relative speeds and full elapsed presentation time; streaks reflect a fixed
exposure and floating-origin recentering preserves observer motion. Simulation, saves,
external assets and personal game unchanged. Native baseline reproduced the open roof
and a synthetic 220 m/s observer was limited to 90 m/s. Fixed clean universal Mac build and identical 17-step scenario pass, zero runtime
errors; day/night cab and rain frames inspected. Four tower and 17 runner regressions
pass. Synthetic 220 m/s rain probe reaches 216.7 m/s (previously 90); real flight/cloud
crossing and control hit-testing unverified.
Decision/evidence: `docs/testing/tower-rain-2026-10-09/README.md`.

**Next:** ground-team implementation complete. Bailey can use the isolated stamped ground-team
bbe40abd build for personal playtesting; it predates the subsequent sky/economy merge.
The separate sky bb4a17ea build/evidence retains the limits recorded above.
Other active state and prior verification limits are preserved in
`docs/history/game-handoff-before-tower-rain-2026-10-09.md`.
Standing policy: agents choose necessary focused runtime checks; broad suites/soaks on request.
Personal saves/running game and generated pipeline/package edits must be preserved.

*One block, replaced at the end of each session. Updated 2026-10-09.*

