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
are retained. Weather ADR was first moved to 0225 (0216 occupied and 0224 reserved in
main's handoff), then to 0226 after the jet merge below. The first integrated native attempt failed compilation because new
placement tests call internal `ClearCache` hooks across assemblies. Added
`Presentation/AssemblyInfo.cs` with the same EditMode friend assembly declaration
already used in Simulation. Runtime methods remain internal; no scenery behavior
or test expectations changed by this fix. Integrated full domain: **1337/1337**.
Integrated full native: **1744 passed / zero failures / two existing inconclusives /
1746 total**. Metadata audit: 1614 GUIDs, 347 mirrors, 70 materials. Weather diff
whitespace checks pass against main; incoming Unity metas retain their existing
blank-value trailing spaces. Integration rebuild/captures follow.

Clean scenery-integrated build **94346c2f**, `dirty=false`, passed. Three inspected
player images/logs are under `scenery-integration/`: distant fog, airport overcast
above, and the same distant view with weather layers disabled. Disabling hides
clouds/shadows/fog layers; the terrain artifacts remain. No managed exception,
shader error or logged stall appeared in these completed captures.

Main then advanced to **b9ec2250** (jet cockpits PR #514). Weather rendering files
are unchanged by that update. Both branches added the same friend-assembly
declaration; retain main's version/meta. Weather ADR moves to **0226**, because
main's jet cockpit ADR uses 0225. Focused native integration checks and a clean
combined Mac build are complete; previous full counts above apply to
94346c2f, not the newer cockpit integration.

Final combined source **409b9349**: focused native **67/67**, focused headless
**37/37**, zero failures. Asset audit: 1620 GUIDs / 347 mirrors / 70 materials.
Diff check against main passes. Clean Mac build (`dirty=false`) passed; identity
and focused native XML are committed beside this record. Two final-build images
and logs under `final-integration/` were inspected: remote storm and airport
overcast above. Distant rain/clouds remain; overview/runways remain readable.
No shader error, managed exception or logged stall in the two completed captures.
The final evidence commit only changes documentation/images, not compiled code.
Full-suite results apply to the scenery integration described above; final jet
integration was checked with the focused suite and clean player build. Manual
continuous camera movement and comparative performance remain unverified.
