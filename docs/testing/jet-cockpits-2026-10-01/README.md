## Authorised merge verification — 1 October 2026

Bailey requested merging after disclosure of the candidate limits. Combined with
main `f1044eea`, preserving the SF34/ATR42/DH8D rollout and all ten jet interiors.
Turboprop layout helpers now inherit the same `CockpitInterior` lifetime; native
cases cover all three turboprops' repeated entry, leave and direct destruction.
Both any-phase and arrivals-only packaged review flags are retained. Jet ADR was
renumbered to 0225 to avoid main's existing 0215 decisions.

- Full headless: **1,345 passed, 0 failed**. Main's separate audio fix resolves the
  original candidate failure; no audio thresholds changed by the jet work.
- Full native Unity: **1,765 passed, 0 failed, 2 inconclusive (1,767 total)**.
  Fresh result: `merge-native-tests.xml`. The existing `FleetMarket_SaysASharedLockOnce`
  and `Storm_IsAGroundStop` preconditions remain inconclusive.
- Native compilation initially exposed main's internal vegetation cache-reset hooks
  being inaccessible to the separate test assembly. `AssemblyInfo.cs` grants
  `Airside.Tests.EditMode` friend access, matching the existing headless harness;
  full native checks pass after this repair.
- Asset audit: **1,618 unique GUIDs, 347 packaged mirrors, 70 character materials**.
- Generated headless harness check passes. PR diff whitespace check passes.
- Clean combined Mac build passes at `6f1d6ddd`, dirty=false. Identity retained
  as `merge-mac-build-identity.txt`. PR #514 carries the authorised merge.
  Previously recorded all-ten native geometry stills remain applicable: jet layout/profile/shell geometry has not changed during merge.
- Packaged flight/night/weather/audio/performance acceptance remains open after the
  authorised merge. Prior failed captures below remain disclosed.

---

# Jet cockpit candidate validation — 1 October 2026

Branch: `feature/all-jet-cockpits`; baseline `f15aac12` (`origin/main` at creation).
Source checkout: `/private/tmp/airside-jet-cockpits`. Ten explicit jet fits; existing
SF34 geometry retained. Candidate, unmerged; simplified native geometry reviewed.
Packaged flight and wider runtime acceptance remain separate.

## Completed evidence

- Focused headless cockpit access/profile/family/topology suite: **27 passed, 0 failed**.
  Coverage includes every catalogue jet, type IDs, controls/display counts, first
  spool, cold/shutdown, local visibility and unsupported types.
- Unity's installed Roslyn compiler successfully compiled all current Presentation
  sources, all Editor sources and all EditMode test sources against cached Unity
  6.3.23f1 references from the existing same-baseline cockpit checkout. Compiler
  exits: 0/0/0. One unchanged `AirsideAdelaideSurroundings.cs` unreachable-code
  warning. This is a source compile check, **not** a native Unity import/test run.
- Focused native Unity suite: **35 passed, 0 failed**; result retained in
  `native-focused-tests.xml`. Ten interior cases check seat parenting,
  shell components, controls, display counts, removed colliders, kept wings,
  repeated entry and visibility restoration on leave/destroy. Native tests exposed
  an Editor-time destruction gap; `[ExecuteAlways]` ensures shell restoration and
  resource disposal during native review as well as normal runtime.
- `git diff --check` clean; native review shell script syntax passes.
- Unity asset audit now passes after native import: **1,594 unique GUIDs,
  347 packaged art mirrors, 70 committed character materials**. The initial empty
  folder metadata findings were checkout/import state, not new cockpit assets.
- Final full native Unity suite: **1,702 passed, 1 failed, 2 inconclusive (1,705 total)**.
  The one failure is the unchanged audio assertion described below. Every cockpit
  test passes; native imports and source compilation completed. Full result retained
  as `native-full-tests.xml`.

## Blocked / unresolved

- Full headless regression inherits an audio failure:
  `AircraftAudioMixTests.PropGovernorHoldsTheNoteWhileJetRevsRise`, expected pitch
  ratio >1.30, actual 1.20711827. The audio implementation/profiles/test are identical
  to baseline `origin/main`; primary checkout contains unrelated audio work and
  has not been modified. Final suite: **1,288 passed, 1 failed, 1,289 total**
  (the adapter separately logs two assumption/precondition skips).
- Initial Unity run was blocked by licensing IPC. Diagnosis found the sandbox-started
  licensing helper still held the global mutex, preventing replacement helpers.
  Terminating only that task-created stale helper restored normal native import,
  compilation, tests and graphics rendering. No licence configuration was changed.
- Mac player builds passed at clean `dc2041f3` and `57d14e09`; build identity
  retained as `mac-build-identity.txt`. Packaged cockpit captures remain unverified:
  B738 departure (6-minute soak), B738 any-phase (150-second timeout), and B38M
  any-phase (10-minute soak) did not enter an eligible local jet or produce PNGs.
  These runs establish player startup, not cockpit rendering or flight acceptance.
  Day/night/weather/audio/performance gates remain open. No overview image has
  been accepted as cockpit evidence.

## Native geometry review

Final source produced **100 native stills**, ten views for each of ten jets, under
`work/jet-cockpit-review/run-XejOOq`. Original runtime aircraft factory, per-type
interior builder, fitted left pilot seat, retained exterior wings/engines and actual
Unity rendering were used. Ten-angle proof sheets are retained under `native/`;
four selected full-resolution views are retained as well.

Reviewed types: **B738, B38M, A320, A21N, E190, A223, A359, A339, B789, B78X**.
For every type: forward, left, panel, right, overhead, layout, bank, footwell,
left-down and right-down. Verdict: no unintended floor/sidewall/roof/front-footwell
opening visible in the reviewed angles. Seats/controls and distinct display layouts
are present. This is a simplified first pass, not a surveyed manufacturer's deck.

Findings fixed from the actual renders:

- Replace overlapping shell primitives with a continuous welded shell. Pure topology
  test verifies every internal edge has two triangles with consistent orientation,
  and the single open boundary is exactly the deliberate window band.
- Separate the flat main ceiling from the raked forward wedge so overhead controls
  stay below the lining. Rails and pillars match the exact opening edges.
- Capture overhead within the actual head-look limits and footwell at the actual
  45-degree pitch limit. The widebody overhead is now visibly reviewable from the seat.
- Update instrument bank for the banked fixture. Engine/ECAM/EICAS screens use real
  startup-state bars; system/OIS screens use a restrained static layout instead of
  a compass on every screen. Local telemetry and spool are labelled honestly.

These are geometry fixtures with synthetic labelled review readouts and a simple
runway background. They do not establish genuine gameplay journeys or audio.

## Checks before merging

1. `bash scripts/test-domain.sh` (resolve/integrate the independently owned audio
   fix before merging; do not silently weaken its test).
2. Full native suite has run; cockpit cases pass. Rerun after integrating the
   separate audio fix or changing source.
3. `bash scripts/review-jet-cockpits.sh`; generates a fresh directory with 100 native
   stills, ten angles per jet. Final native review above is complete; if changed, inspect
   every angle for shell gaps, seat/window fit, legible displays and family identity.
   Fix actual findings; file existence alone is not a visual verdict.
4. `bash scripts/build-mac.sh`; run actual local departures and arrivals for each
   type. Review at day/dusk/night and rain/fog, startup through shutdown, entry/exit,
   target loss/rebuild, look/zoom/recenter/input, listener and frame-time behaviour.
5. Packaged review may select a particular real departure using
   `-airsideReviewCockpit -airsideReviewCockpitType B738` (substitute any jet ID).
   It waits for an eligible actual type; does not force-start engines or fake flights.
6. Record evidence, fix any gaps and merge only after branch review.

Packaged Mac build passed at clean commit `dc2041f3`. The first B738 departure capture completed its six-minute soak without an eligible departure, so no cockpit images were produced. The review helper now offers `-airsideReviewCockpitAnyPhase` to enter an actual eligible running arrival; the default still waits for departure startup. This flag does not modify aircraft or engine state.

The requested initial implementation is committed and pushed; the branch remains
unmerged. All ten native decks were inspected. The broader packaged acceptance
above is required for later merge review and has not been claimed complete.
