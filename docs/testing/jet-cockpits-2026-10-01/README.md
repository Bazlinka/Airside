# Jet cockpit candidate validation — 1 October 2026

Branch: `feature/all-jet-cockpits`; baseline `f15aac12` (`origin/main` at creation).
Source checkout: `/private/tmp/airside-jet-cockpits`. Ten explicit jet fits; existing
SF34 geometry retained. Candidate, unmerged; visual fidelity is not yet accepted.

## Completed evidence

- Focused headless cockpit access/profile/family suite: **16 passed, 0 failed**.
  Coverage includes every catalogue jet, type IDs, controls/display counts, first
  spool, cold/shutdown, local visibility and unsupported types.
- Unity's installed Roslyn compiler successfully compiled all current Presentation
  sources, all Editor sources and all EditMode test sources against cached Unity
  6.3.23f1 references from the existing same-baseline cockpit checkout. Compiler
  exits: 0/0/0. One unchanged `AirsideAdelaideSurroundings.cs` unreachable-code
  warning. This is a source compile check, **not** a native Unity import/test run.
- `JetCockpitInteriorTests` compiled; ten native type cases check seat parenting,
  shell components, controls, display counts, removed colliders, kept wings,
  repeated entry and visibility restoration on leave/destroy. **Not executed**.
- `git diff --check` clean; native review shell script syntax passes.
- Unity metadata supplied for all new source/test files. Global asset audit still
  reports baseline missing `Assets/Resources.meta` and three orphan Animation
  folder metas (empty directories do not survive a Git checkout). No new art file
  or packaged mirror was introduced by this change.

## Blocked / unresolved

- Full headless regression inherits an audio failure:
  `AircraftAudioMixTests.PropGovernorHoldsTheNoteWhileJetRevsRise`, expected pitch
  ratio >1.30, actual 1.20711827. The audio implementation/profiles/test are identical
  to baseline `origin/main`; primary checkout contains unrelated audio work and
  has not been modified. Final suite: **1,288 passed, 1 failed, 1,289 total**
  (the adapter separately logs two assumption/precondition skips).
- `bash scripts/test-unity.sh`: no completed native results. The sandbox invocation
  failed package-manager IPC; an authorised unsandboxed invocation repeatedly
  failed licensing (`Channel LicenseClient-bailey.fleming doesn't exist`,
  `Licensing initialization failed`, `com.unity.editor.headless was not found`).
  Stopped only this branch's stalled editor process. No licensing configuration
  was changed. Mac build and native renders were not attempted after this failure.
- No jet screenshots were generated or inspected. Closed-shell geometry and tests
  do not prove no visible gaps from every seat angle. Day/night/weather, readability,
  model fit, repeated gameplay entry/exit, audio and performance remain unverified.

## Resume on a licensed Mac

1. `bash scripts/test-domain.sh` (resolve/integrate the independently owned audio
   fix before merging; do not silently weaken its test).
2. `bash scripts/test-unity.sh`, including all ten `JetCockpitInteriorTests` cases.
3. `bash scripts/review-jet-cockpits.sh`; generates a fresh directory with 70 native
   stills: forward, left, right, panel, overhead, layout, bank for every jet. Inspect
   every angle for shell gaps, seat/window fit, legible displays and family identity.
   Fix actual findings; file existence alone is not a visual verdict.
4. `bash scripts/build-mac.sh`; run actual local departures and arrivals for each
   type. Review at day/dusk/night and rain/fog, startup through shutdown, entry/exit,
   target loss/rebuild, look/zoom/recenter/input, listener and frame-time behaviour.
5. Packaged review may select a particular real departure using
   `-airsideReviewCockpit -airsideReviewCockpitType B738` (substitute any jet ID).
   It waits for an eligible actual type; does not force-start engines or fake flights.
6. Record evidence, fix any gaps and merge only after branch review.
