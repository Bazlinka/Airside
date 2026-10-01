# Weather coverage verification — 1 October 2026

Branch `fix/regional-weather-coverage`, based on main `46ab53d3`.
Task packet/acceptance: `docs/plans/weather-coverage.md`.

Completed: 13/13 focused headless coverage/atmosphere tests; 30/30 focused native
weather/coverage/atmosphere/cockpit camera tests. The latter also verify all three
previously merged turboprop factories and lower-shell sightlines after the licensing
service recovered. Metadata audit passed: 1593 GUIDs, 347 mirrors, 70 character materials.

Baseline domain: 1280 passed / 1 existing audio failure / 1281 total. The sole failure
is `PropGovernorHoldsTheNoteWhileJetRevsRise`, ratio 1.20711827 versus >1.3, matching
main and prior PR CI. No audio code is changed here. Full native Unity: **1695 passed / 1 identical existing audio failure / 2 existing
inconclusives / 1698 total**. Weather/coverage tests pass. Mac build and actual-player
review results will be recorded below when complete. Changed full domain: **1288 passed / 1 identical existing audio failure / 1289 total**.

The first focused headless invocation overlapped harness regeneration and failed
on provisional Unity-only includes. It was repeated after regeneration completed;
13/13 passed. The generated harness diff includes only the new helper and test file.

Repeat packaged coverage with `scripts/review-weather-coverage.sh`: fresh soak
sessions, remote focus (14000, 8000), low and high cameras, cloud/fog/storm and
overcast below/above the deck, plus the normal airport overview. Normal player
saves are not used. Inspect pixels and logs before declaring visual acceptance.

## Completed source build and player review

Clean Mac build **4f1385c5**, `dirty=false`, passed. Six native player images/logs are
under `pre-integration/`: remote cloudy, fog, storm, overcast below/above, and normal
airport above the deck. Pixels were inspected. Remote low camera position is
(10917.48, 665.32, 8543.53), outside the former fog rectangle and cloud wrapping
area. Cloud bodies remain present; fog is visible across low terrain/water; storm
rain/clouds remain present there. Both overcast sides show cloud bodies and the
airport overview retains readable runways/stands. No shader error, managed
exception or logged frame stall in the completed six-shot batch.

The first fog launch exited before its image, without a managed exception; resume
succeeded. A separate moving Q400 cockpit capture was terminated with SIGTERM
before images/eligible-entry evidence. Cause was not established; that attempt
proves no moving cockpit result. Static images and native/math continuity tests do
not establish manual panning, layer-toggle usability, all flight phases, weather/time
combinations or comparative performance. Those remain manual QA limits.

The remote terrain photo edge/resolution artifacts visible in these images belong
to the existing landscape; weather coverage does not change the terrain assets.
The images predate latest-main scenery integration. A clean integration rebuild
and representative remote/airport captures will be recorded separately.

## Latest-main integration

Main advanced to `f1044eea` with new scenery and a correction to the audio ratio
expectation. Weather source merged without conflicts; both handoffs/changelogs
are retained. Weather ADR is now 0225 (0216 is occupied and 0224 reserved in main's
handoff). The first integrated native attempt failed compilation because new
placement tests call internal `ClearCache` hooks across assemblies. Added
`Presentation/AssemblyInfo.cs` with the same EditMode friend assembly declaration
already used in Simulation. Runtime methods remain internal; no scenery behavior
or test expectations changed by this fix. Integrated full domain: **1337/1337**.
Integrated full native: **1744 passed / zero failures / two existing inconclusives /
1746 total**. Metadata audit: 1614 GUIDs, 347 mirrors, 70 materials. Weather diff
whitespace checks pass against main; incoming Unity metas retain their existing
blank-value trailing spaces. Integration rebuild/captures follow.
