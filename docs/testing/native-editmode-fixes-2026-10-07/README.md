# Native EditMode failure repair packet

Outcome: existing saves resume without invented maintenance jobs or wake movements;
flight cameras keep their smooth entry and follow presentation origin shifts. Native fixtures
reflect the merged interface and cabin materials while retaining sightline, visibility,
optics, viewport, and click-target assertions.

Scope: AirlineSave, AdelaideGroundPolicy approach distance, camera transition/origin handling, and the fixtures
listed in GAME.md. ADRs 0242–0245 remain the intended camera, ground and interface
contracts. No product plan, fleet catalogue, runtime asset, economy, or save version
change. Simulation timing remains injected and deterministic. Real malformed records
must still be rejected; all-default Unity inline objects represent absent records.

Acceptance:
- Unity's three listed save/backfill/catch-up fixtures pass after JSON round trips.
- An all-default inline job or wake is absent; a partially populated invalid record
  fails restore; a Maintenance-state aircraft still requires a valid saved job.
- The full busy-day fixture uses the route policy's actual 100 m painted stand
  approach when separating fitted stand neighbours from transit traffic. It uses
  shared wheels-on-runway poses and actual helicopter pad tracks, reports no ground
  transit overlap, and completes more than 100 rotations. Ground clearance behavior
  stays unchanged. Baseline main completed 132 rotations with one falsely classified
  neighbour encounter ~95 m from the moving Q400's assigned stand.
- Both passenger window sightlines remain open for all 13 types, with one draw per
  cabin material (eight authored surface materials), and exterior visibility restored.
- Camera tracking checks run after the intended entry transition; an origin shift
  during the transition translates the camera and glide anchor together.
- Radar is tested explicitly when enabled. Overview has a full-width status capsule,
  right inspector and compact career card; flights remain available in Operations.

Verification: scripts/test-domain.sh is supplementary. The busy-day fixture is now
Unity-free and included in its generated list. Native compilation and the entire
EditMode suite remain pending with scripts/test-unity.sh on the Mac.
Desktop Commander reported that Mac offline during this repair; no native run is
claimed. Latest failure XML was requested to confirm the source-based diagnoses.

Local results (supplementary, 7 October Adelaide):
- `scripts/test-domain.sh`: **1,830 passed, 0 failed**, about four minutes. The
  adapter also printed the existing FleetMarket/Storm inconclusive cases. The
  generated harness list is current. This full run preceded adding the two
  missing-maintenance-job cases below; no runtime changes followed the full run.
- Final `AirlineSaveRestoreTests`: **10/10 passed**, including both null and Unity
  placeholder jobs on a Maintenance-state aircraft, and partial-record rejection.
- Busy-day plus initial save regressions: **9/9 passed**. Busy-day no-overlap and
  >100 rotation assertions passed in about four seconds (main baseline: 132).
- C# 9 parsing of all **12** changed source/fixture files: zero syntax errors.
  This does not verify Unity APIs, assembly references, or native execution.
- Unity asset audit: **1,793 GUIDs**, **386** runtime mirrors and **70** character
  materials passed; `git diff --check` passed.

Native follow-up: compile in Unity 6.3 LTS and run the entire EditMode suite,
retaining result XML. In particular confirm all 13 real window raycasts, both
exterior/cockpit optic and transition fixtures, the mid-entry/exit origin test,
CameraShellAnchor EditMode delivery, updated layout/radar cases, and the three
actual JsonUtility round trips. These have been repaired from the checked-in
source contracts; they are not marked natively green without that run.

Merge update, 7 October: Bailey explicitly authorised merging #552 without waiting
for further tests. Rebased onto main e31d56fe, preserving the intervening save fix
(#555), fleet/outstation views, arrivals and rendering changes. Empty-record
validation is now stricter than #555: only exactly default records are absent;
partial records still fail. The initial results above predate this integration;
no new native result is claimed. Native rerun is follow-up work, not a merge gate
for this explicitly authorised merge. The ADR now uses the current date-based
filename: `docs/decisions/2026-10-07-native-save-and-flight-view-contracts.md`.

After resolving the main integration, targeted `AirlineSaveRestoreTests`: **10/10 passed**. No new full or native suite run was required for this owner-authorised merge.
