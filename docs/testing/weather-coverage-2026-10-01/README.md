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
review results will be recorded below when complete. Changed full domain is running.

The first focused headless invocation overlapped harness regeneration and failed
on provisional Unity-only includes. It was repeated after regeneration completed;
13/13 passed. The generated harness diff includes only the new helper and test file.

Repeat packaged coverage with `scripts/review-weather-coverage.sh`: fresh soak
sessions, remote focus (14000, 8000), low and high cameras, cloud/fog/storm and
overcast below/above the deck, plus the normal airport overview. Normal player
saves are not used. Inspect pixels and logs before declaring visual acceptance.

Pending: completed full suites, clean Mac build, packaged shader/geometry/readability
review, moving cockpit/pan validation, layer-toggle check and comparative performance.
