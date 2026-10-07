# Five-area bug sweep — 7 October 2026

Task issue: #576. Owner: Codex / Bazlinka. Five agents requested by Bailey, one each for Aircraft, Ground, Graphics, Performance and Buttons, used isolated worktrees from main `0087ded0`. Root reviewed their fixes and integrated current main `1fd4c9fc` into `codex/five-area-bug-sweep-20261007` before final acceptance.

## Final scope and acceptance

- Aircraft: preserve main #552's `ShiftFlightOrigin` camera/glide correction and stronger native cabin/view fixtures; add three native midpoint/exit origin-shift regressions.
- Ground: keep parked fixed-wing aircraft in taxi clearance, isolate helicopter liftoff from fixed-wing queues/poses, use a positive 1 m parked planning allowance while keeping 3 m for moving/queued traffic, and correct the short BAY-4 inbound apron section while preserving graph connectors/stop. Add strict optional-save-record regressions around main #552's implementation.
- Graphics: one camera owns each sky/rain shell; transfer/destruction cannot let another camera reclaim it.
- Performance: cache both filtered runway crossing views and avoid boxed catalogue enumeration. 10,000 warm queries allocate 560,000 bytes before and zero after in the .NET 8 measurement; this is not a player frame-time measurement.
- Buttons: reserve the inspector's actual fixed header/footer space and give the Flight Manual exclusive input while open. Preserve current fleet/outstation controls and main's native optional-radar/layout fixtures.

Preserve main's approved arrivals-never-hold-on-final rule: runway/exit-only landing clearance, final metering and decision-point go-arounds. No pre-landing taxi-route check survives in the final code. Domain/simulation remain Unity independent; injected clock, seeded randomness, reservations, exactly-once commands, save v22/migrations and authored assets stay intact. No product plan changes.

## Area packets and reproducible evidence

- [Aircraft](five-area-aircraft-2026-10-07.md): original agent investigation and adjacent rules evidence. Runtime/native fixture fixes subsequently merged independently in #552; final branch keeps the main implementation and adds regression coverage.
- [Ground: current policy](five-area-ground-static-clearance-2026-10-07.md): authoritative geometry/margin packet, including the [audit source](five-area-ground-static-audit-2026-10-07.cs.txt) and [audit output](five-area-ground-static-audit-2026-10-07.log).
- [Ground: original branch](five-area-ground-2026-10-07.md): explicitly historical investigation; its old-flight-policy full-route airborne guard is superseded and not retained. The early intermediate generic guard caused a mutual hold and overnight queues; rejected soak/capture runs are not counted as passing acceptance.
- [Graphics](five-area-graphics-2026-10-07.md): actual pre-fix source reproduced camera handoff failure against a small scratch transform shim; fixed source passed five supplementary lifecycle checks. Native tests remain unrun.
- [Performance](five-area-performance-2026-10-07.md): before/after allocation measurement and filter/catalogue equivalence tests.
- [Buttons](five-area-buttons-2026-10-07.md): compact panel failures reproduced before layout fix; corrected fixtures and source-checked modal branch. Native clicks remain unverified.

Current-main static audit: 428 compatible routes / 14,552 worst-compatible parked-stand pairs, all accepted with zero outside-own-approach gap violations; minimum swept gap 2.782613 m. It uses main's 100 m `AdelaideGroundPolicy.StandApproachMetres`, replacing the older branch's 90 m audit. These are simulation geometry checks, not native aircraft/rendering acceptance.

## Current-main verification

- Focused integration: 67 of 68 passed, including the unchanged 30-day soak, approved arrival-priority tests, optional save restore, query cache, HUD and review capture checks. The one failure was an obsolete new fixture expecting the now-corrected BAY-4 route to intersect parked BAY-2; it was replaced with a deliberately conflicting route to verify the guard independently of that geometric fix.
- Final geometry/guard unit run: **14 passed, zero failed**. Rejects 0.5 m parked gap / accepts 1.5 m; rejects 2.5 m queued gap / accepts 3.5 m. All four Q400 BAY-4 routes clear the three nearest largest parked neighbours with the larger dynamic allowance and retain the stop. Actual parked obstruction blocks/releases as its aircraft leaves; opening-bank progress and rotor queue exclusion remain checked.
- `scripts/test-domain.sh`: current integration **1,892 passed / one failed (1,893 total, 5 m 8 s)**. The sole remaining case is `GroundSeparationTests.BusyDay_NoAircraftDriveThroughEachOther`, awaiting-exit Q400 versus taxiing 737 MAX at t=48890. Final correction/verification pending. This script also checks generated harness consistency and compiles eligible tests against Unity's NUnit 3.5 without implicit usings; that compile step has passed.
- Asset audit: **1,805 unique GUIDs, 388 byte-identical art mirrors, 70 committed character materials**. No new authored assets in this task.
- C# 9 syntax check and `git diff --check`: passed; syntax parsing is not native type checking or a Unity compile.

Historical baseline: main `0087ded0`, 1,827 headless passed. Older intermediate suite: 1,850 passed / seven ground timing/soak/capture failures; the failed changes and old arrival-policy guard were corrected/superseded before current-main verification. Final acceptance uses only the final source and current-main results above.

## Native checklist and remaining limits

No Unity editor/player, builds, screenshots or native EditMode execution was authorized/run. Unverified in Unity: IMGUI click ownership, inspector fit at short heights, actual taxi/turn/saved-in-progress poses, camera-shell lifecycle/rendering, cockpit/passenger/exterior transitions and player frame performance. Run `scripts/test-unity.sh` and native compilation on Unity 6.3 LTS when authorized; inspect day/dusk/night and the documented area checklists. Native camera/batching/sightline tests stay separate from headless evidence.

This task does not certify that every historically reported native failure is resolved. The original single-camera shell fixture was repaired independently in #552 and retained; its native outcome remains unverified. Review the draft and obtain native acceptance before merge.
