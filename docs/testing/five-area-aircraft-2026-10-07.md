# Aircraft bug fix — 7 October 2026

## Task packet

Player outcome: cockpit, passenger and exterior camera glides retain their position
when the regional flight world rebases. Returning from a distant flight starts the
outgoing glide at the aircraft's absolute position rather than an unrelated spot
near Adelaide.

Scope: `AirsideCameraController.Cockpit.cs`,
`AirsidePrototype.FlightWorld.cs`, `CockpitCameraTests.cs`,
`PassengerFlightViewTests.cs`. No cabin geometry or visual assets changed.

Relevant decisions: ADR 0215 (8 km regional render origin), ADR 0227
(passenger/exterior cameras), ADR 0239 (fitted cabin), ADR 0242
(0.9-second aircraft-view glides). Preserve simulation determinism, reservations,
flight timing, save compatibility, camera optics restoration, exterior visibility
and intentional transition duration. This corrects coordinate bookkeeping and
existing acceptance fixtures; it makes no new design decision.

Acceptance: origin translation moves the camera and both entry/exit source anchors
by the same delta, including origin reset after leaving an aircraft view. Native
regressions exercise a half-completed cockpit/exterior glide and the first outgoing
overview frame. Settled tracking, clip-plane restoration, motion isolation and
actual mesh-collider window sightlines remain asserted.

## Findings and changes

1. A real camera defect: `UpdateFlightWorld` previously translated only the current
   camera position while `ApplyCockpitPose` blended from an unchanged world-space
   `_blendFromPos`. At half progress an 8 km origin step leaves half the step in
   the displayed camera, making a several-kilometre jump. The exit origin reset
   skipped even the camera translation because `InCockpit` had already become
   false. `ShiftOrigin` now updates camera position plus both glide source anchors;
   the flight-world owner calls it for origin changes including exit.
2. The reported thirteen passenger test failures were previously gated by an
   obsolete six-renderer limit. The current cabin creates eight independently
   authored materials after upholstery/headrest/shell refinements. Replace the old
   magic budget with the intended invariant: nonempty geometry, exactly one batch
   per distinct material, and exactly one material per renderer. This fails if
   repeated seat/window fittings stop batching. Collider/raycast, clipping,
   exterior wing retention and visibility restoration assertions are retained.
   No claim is made that the unseen native run's later raycasts now pass.
3. Three historical cockpit/exterior tests assumed the view cut instantly to its
   target. ADR 0242 deliberately introduced entry glides. Tracking fixtures now
   explicitly complete that glide before asserting rigid target tracking; new
   tests separately assert that entry starts at the previous pose and rebases
   correctly halfway through. The exterior motion fixture starts at its 48-degree
   resting optics to isolate motion/recenter invariance from the entry FOV glide.

## Verification and limits

Executed on the isolated aircraft branch with the local .NET 8 SDK:

```sh
source /workspace/airside-tools/activate.sh
dotnet test scripts/dotnet-harness/Harness.csproj --filter 'FullyQualifiedName~CockpitFeelTests|FullyQualifiedName~PassengerCabinProfileTests|FullyQualifiedName~CockpitMotionTests|FullyQualifiedName~CockpitAvailabilityTests|FullyQualifiedName~FlightWorldTests' --logger 'console;verbosity=minimal'
```

Result: **114 passed, zero failed/skipped**. Existing CS0649 warnings concern the
unrelated glTF test DTOs. This checks adjacent pure profile, feel, availability and
flight-world rules; the changed Unity-dependent camera/interior fixtures are
excluded from the headless harness. `scripts/update-harness.py --check` confirmed
the generated harness is up to date; `git diff --check` passed.

No Unity execution, editor, player, builds or renders were run under the recorded
owner restriction. Native C# type checking and the new/updated native tests remain
unverified. When authorized, run the `CockpitCameraTests` and
`PassengerFlightViewTests` native fixtures, then play cockpit → passenger → exterior
near an origin boundary and regional view → overview. Inspect both window sides
for all thirteen types and confirm no jump, clipping or optics/visibility leak.

Root owns the consolidated `GAME.md`/`CHANGELOG.md` update and combined checks.
