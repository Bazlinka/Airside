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
- Native Unity licensing recovered after launching the installed Unity Hub normally.
  Full EditMode: 1688 passed, one failure (the same baseline audio assertion), two
  inconclusive cases. No licensing or compilation blocker remains.
- Five additional native tests exercise the actual presentation journey at 0.25 s
  intervals for KGC and CPD, both directions, and actor visibility restoration.
  Initially exposed kilometre-scale inbound altitude jumps at the 32 km approach
  handoff. Cruise height now eases onto the same capped local final over the 40 km
  map-track blend; final five cases passed in 5.785 s. XML: `journey-results.xml`.
- Airport-only GSE, boarding, aerobridges, birds and coastal boats are hidden while
  the distant origin is active; original active states restore on return/exit.
  Their visual updates pause; simulation continues. The local arrival owner seeds
  its pose from the watched view rather than snapping to a fresh tower estimate.
- Coarse-mesh runway support widened to 1.5 km flat / 3 km blend: a 200 m
  pointwise flatten did not guarantee that the surrounding 1 km triangle vertices
  stayed below the strip. New regression samples every runway's containing cells.
  All 13 focused headless flight-world tests pass (303 ms); harness derivation
  check is current. Packaged review driver source compilation: zero errors, nine
  existing warnings; native compilation now verified, player verification pending.
- Latest native focused world/journey run: 18/18 passed, 1.956 s; current review
  driver imported/compiled successfully. `world-results.xml` contains the results.
- First Mac build succeeded, identity `40caaf9d-dirty`, log reports `Build Finished,
  Result: Success`. It excludes the later journey review driver and terrain-cell
  fix. Current clean source must be rebuilt before running the review driver.
  Graphics-on acceptance remains open. Native path continuity does not establish
  cockpit appearance, frame rate or seamless view ownership.
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

## Packaged review, first attempt

Clean Mac build `158b4ac5` succeeded and included the journey-review CLI. Graphics-on
KGC captures show the SF34 at stand, taxiing and outbound. This run is not accepted:
the driver logged/completed against the first player aircraft (VH-PAX), while the
cockpit followed VH-ZRC. It exited before a round trip without a COMPLETE marker;
the available log gives no exit cause. Bind scheduling, cockpit selection, phase
capture and trace to the same supported player registration. Native focused checks
still pass after that correction. Rebuild before retrying in a distinct output
folder; first-attempt records remain in `work/sa-flight-kgc/`.

## Graphics-on KGC review, revision 3

Clean player `d3a951f5`: captured player VH-PAX taxi, takeoff, outbound cruise,
origin changes, Kingscote approach, rollout and destination stand. Resident terrain
never exceeded 49 tiles in the trace; elevations loaded. Runway is visible at its
sourced height. Departure-bank commands from the local climb-out incorrectly
persisted into regional cruise; suppress those commands on the geographic track.
Native focused paths/import still pass after that correction.

Do not accept this as a complete rendered round trip: after real 252.8 s the next
journey trace was 428.7 s, already back at Adelaide. The heartbeat reports a
172,182.1 ms frame. No inbound, home landing or taxi-in captures exist. The old
script returned success on a COMPLETE marker alone; now require the essential
rendered phase files too. Accelerated QA now caps each frame's simulated advance
so a blocked frame cannot skip the return. Rate-1 measurements keep wall time and
record stalls. An external watchdog samples the owning player if its log stops for
20 s. Display power logs show no display-off or sleep event during this run.
Before the stall, stationary regional minutes recorded 120 fps / 8.3 ms p95;
these accelerated values are not live-time acceptance or a baseline comparison.

Bailey confirmed the first two review exits were manual closures. They are not
runtime crash evidence. Exact baseline f15aac12 now has a clean Mac build in
`/private/tmp/airside-sa-baseline`, using an independent APFS-cloned Library cache.
Baseline and normal-rate performance windows are still outstanding.

## Authorised main integration

On 1 October Bailey explicitly requested merging the implemented branch to main.
The current clean `ac7de3c9` Mac build passed. Kingscote r4 captured outbound
approach, rollout, destination, return cruise and restoration of the Adelaide
render origin. The player exited before the return landing, without a completed
round trip. The earlier 172-second stall remains unresolved. No packaged Coober
Pedy completion or accepted comparative performance result is claimed. Previous
self-imposed full player gates remain follow-up work under this explicit merge
authorisation. Current main's cockpit/weather work is preserved in the integration.
