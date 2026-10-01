# South Australia flight-world candidate — 1 October 2026

Base: `origin/main` f15aac12. Isolated checkout: `/private/tmp/airside-sa-world`.
The original Developer checkout's audio changes are untouched.

- Baseline domain: 1277 passed, one failure, 1278 total. Existing failure:
  `AircraftAudioMixTests.PropGovernorHoldsTheNoteWhileJetRevsRise`, expected >1.3,
  actual 1.20711827. This branch does not alter aircraft audio.
- Changed domain: 1289 passed, same one failure, 1290 total. All 12 added tests pass.
  Log tail in `domain-result.txt`. Two existing NUnit precondition skips also print;
  the runner reports zero skipped in its summary.
- Full Domain/Simulation/Presentation source compiled with .NET against the installed
  Unity engine/package DLLs: zero errors, nine existing warnings. This uses current
  source for all three layers; cached Domain/Simulation DLLs were stale and are not
  evidence for this branch. Compilation summary in `presentation-compile.txt`.
- Native Unity EditMode: no results; licensing initialization failed after 74.85 s,
  `LicenseClient-bailey.fleming` missing and `com.unity.editor.headless` not found.
  Reconnection retries did not succeed; the blocked run was interrupted. No native
  build, shader compilation, packaged screenshots, full journey or FPS measurement.
- Asset audit reports existing missing `Assets/Resources.meta` and orphan Animation
  World/Aircraft/Vehicles metadata. No new GUID or runtime mirror errors reported.
  These baseline metadata issues are not repaired as part of this feature.
- Regional DEM is 701×701 int16 samples, 982,842 bytes including header; all 225 COG
  tile probes completed, only HTTP 404 accepted as missing sea. Bounds/data parser
  tests pass; runtime copy must remain byte-identical. Negative elevations clamp
  to zero; inland below-sea-level land is not a bathymetry/shoreline dataset.

Candidate only. Required acceptance is in `docs/plans/south-australia-flight-world.md`.
In particular, dynamic airport actors and weather/origin changes still need actual
player inspection. Do not claim seamless journeys or accepted performance based on
source compilation or the fixed terrain budget. Do not merge until the native and
player gates have been completed.
