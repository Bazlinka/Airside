# Graphics bug fix — 7 October 2026

## Task packet

Player-visible outcome: transferring camera-centred sky or rain scenery to another camera must keep it at the new camera's position; an older camera cannot pull it back during its later frame update.

Scope: `CameraShellAnchor.cs`, its new focused EditMode tests and this evidence file. Existing aircraft cameras/interiors, HUD, environment assets/materials and other camera-controller behavior remain unchanged. Simulation state, injected clocks, reservations, save schema and migration behavior remain unchanged.

Acceptance criteria:

- A shell belongs to its latest camera, in either camera update order.
- Removing a transferred or destroyed shell preserves the other registered shells.
- Late placement uses the camera's final position and view direction, including absolute-height offsets.
- Destroying the camera clears its ownership registrations; another camera can claim the surviving shell.

No new art or external assets. No design/schema decision is required: this enforces the existing placement contract.

## Findings and change

`Place(camera B, target)` previously registered the target on B without unregistering it on A. If A's `LateUpdate` ran after B's, A overwrote B's placement. Added exclusive ownership per target, removal by swapping the final entry into the removed slot, destroyed-target pruning and owner cleanup on camera destruction. The steady frame path still places every live entry with the existing offset resolver and execution order.

The recorded native `PresentationBugSweepTests.CameraShellAnchor_FollowsCameraMovedAfterPlacement` failure has no retained failure message/XML in this workspace. Its existing single-camera logic is already present in the base implementation. This change addresses a separately reproduced ownership gap; it does **not** establish that the recorded native failure is resolved. That original test remains unchanged.

## Evidence

- Supplementary .NET 8/C# 9 source execution: compiled the actual production `CameraShellAnchor.cs` against a minimal scratch Unity object/transform shim in `work/graphics-anchor-check/`. Five checks passed: handoff and retained index, reverse update order, destroyed-target pruning, final position/direction placement, and destroyed-owner cleanup/reacquisition. This is not a native Unity test and does not validate Unity callbacks or rendering.
- Ran the same handoff check against `git show HEAD:.../CameraShellAnchor.cs` (the pre-fix source). It failed: `Got 1,422,3; expected 60,700,30`, showing the older camera reclaimed the shell. The fixed source passed all five checks.
- Added four Unity EditMode regressions in `CameraShellAnchorTests.cs`: handoff in both update orders; final position/view direction; destruction of the owning camera; destroyed-shell pruning plus re-registration of the retained shell. They invoke the private frame callback using reflection, matching other EditMode camera tests.
- `python3 scripts/audit-unity-assets.py`: passed (1,794 unique GUIDs; 386 mirrored runtime art files; 70 committed character materials).
- `git diff --check`: passed.
- The standard dotnet harness excludes Unity-dependent `CameraShellAnchor` and its tests; no headless suite result is claimed as coverage of these regressions.

## Native follow-up and limitations

Unity execution is prohibited by the retained owner instruction. No Unity compile, EditMode run, screenshots or runtime visual acceptance is claimed. Root consolidates `GAME.md`/`CHANGELOG.md` and integrated validation; this branch does not push or edit those shared files.

When Unity execution is authorized, run the four new `CameraShellAnchorTests`, the original `PresentationBugSweepTests.CameraShellAnchor_*` tests and native compilation. In Play, pan/orbit at night and in rain, then transfer a shell between two cameras and verify the old camera cannot recenter it. Inspect camera destruction/scene replacement and confirm sky/rain remain aligned during final camera motion.
