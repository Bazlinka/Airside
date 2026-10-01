# Turboprop cockpit rollout verification — 1 October 2026

Candidate branch: `feature/turboprop-cockpits`, based on main `f15aac12`.
Scope: SF34, ATR42, DH8D only. No simulation/save/audio implementation changes.

## Completed checks

- Headless availability/card cases: **8 passed, 0 failed**. Includes first spool,
  cold shutdown, off-field rejection, all three turboprops, disabled jets and card fit.
- Full `scripts/test-domain.sh`: **1280 passed, 1 failed, 1281 total**.
  Sole failure: existing main `AircraftAudioMixTests.PropGovernorHoldsTheNoteWhileJetRevsRise`;
  expected ratio > 1.3, actual 1.20711827. This audio test/code is outside cockpit scope;
  the same failure was already recorded in main and cockpit-shell PR CI.
- **Offline C# compilation passed** for complete `Airside.Presentation`,
  `Airside.Tests.EditMode` and `Assembly-CSharp-Editor` assemblies using Unity
  6000.3.23f1's Roslyn compiler and cached Unity-reference response files. New
  base/glass source files were explicitly included. Only warning: existing
  unreachable surroundings code. Outputs went to ignored `work/turboprop-compile/`;
  they were not copied into Unity's runtime/build cache.
- Unity metadata/packaged art audit: passed, 1591 unique GUIDs, 347 mirrors,
  70 committed character materials.
- `git diff --check`: passed.

Offline compilation is a code/reference check; it does not prove Unity import,
IL postprocessing, completed native tests, rendering or a playable build.

## Environment blocker and unverified checks

Native focused EditMode was attempted twice, first with a private temporary directory,
then with the normal macOS temporary directory. Both stalled before test execution:
`Channel LicenseClient-bailey.fleming doesn't exist`, `Licensing initialization failed`,
and repeated licensing client reconnection failure. No new test XML was produced.
The licensing-client log reports another instance holding its global mutex; no
shared licensing client or other contributor process was stopped. Only this task's
blocked editor processes were terminated.

No native test pass, new Mac build or screenshot is claimed. The 20 intended native
cockpit cases cover eligibility plus camera restoration, all-type factory fit and
exterior restoration, rejection without allocation, and lower-shell occlusion versus
open windows in level/banked headings. Actual geometry, shadows, display readability,
kit alignment, head-look clipping, live movement and runtime audio still need review.
Do not merge the candidate until native checks and rendered review are complete.
Existing broader journey/weather/performance gates in the original cockpit plan
remain open; a still or offline compile does not close them.

## Continue when Unity licensing is healthy

From this branch's checkout:

```sh
scripts/test-unity.sh
/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD/game/Airside" \
  -executeMethod CockpitAppearanceReview.Run \
  -cockpitReviewType all -cockpitReviewOutput "$PWD/work/turboprop-review" \
  -logFile "$PWD/work/turboprop-review.log"
```

The review uses native graphics (do not add `-nographics`) and the real aircraft
builder. Inspect all 30 outputs: forward, sides, panel, overhead, layout, bank,
footwell, and both lower-side views for each type. Commit representative evidence
and correct any visual defects. Editor kit stills do not establish packaged behavior.

Build a clean tip with `scripts/build-mac.sh`. For actual packaged arrivals, use
existing fresh-soak flags plus `-airsideReviewCockpit -airsideReviewCockpitType DH8D
-airsideReviewCockpitArrivals`. Repeat SF34; enter ATR from an eligible bought/scheduled
ATR in a separate test career. The current seed does not provide an ATR arrival,
so the filter alone will wait rather than create one. The flags select eligible
existing aircraft; they do not bypass cold/off-field rules or mutate a normal save.

Store new evidence here with registration/type, phase, build identity, logs and
honest coverage limits. Confirm startup, taxi/holds, departure/local-loss, arrival,
landing/taxi-in/shutdown and repeated entry/exit, including rebuilt views.
