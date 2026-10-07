# Five-area bug sweep — 7 October 2026

Task issue: #576. Owner: Codex / Bazlinka. Five agents requested by Bailey, one each for Aircraft, Ground, Graphics, Performance and Buttons, used isolated worktrees from main `0087ded0`. Root reviewed their fixes and integrated current main `0dc6e629` into `codex/five-area-bug-sweep-20261007` before final acceptance.

## Final scope and acceptance

- Aircraft: preserve main #552's `ShiftFlightOrigin` camera/glide correction and stronger native cabin/view fixtures; add three native midpoint/exit origin-shift regressions.
- Ground: keep parked fixed-wing aircraft in taxi clearance, isolate helicopter liftoff from fixed-wing queues/poses, use a positive 1 m parked planning allowance while keeping 3 m for moving/queued traffic, and correct the short BAY-4 inbound apron section while preserving graph connectors/stop. Preserve main #579's merged exit-occupancy window and add regressions showing the exit conflict is rejected while distant taxi-in conflicts still permit landing. Add strict optional-save-record regressions around main #552's implementation.
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

- Required `scripts/test-domain.sh`: **1,905 passed / zero failed**, runner summary total 1,905, test duration 5 m 19 s. Includes the unchanged busy-day separation test and longer soak/capture checks. The log also emits skip notices for the pre-existing `FleetMarket_SaysASharedLockOnce` and `Storm_IsAGroundStop`; those notices are not included in the runner's passing total.
- Generated harness consistency and compile against Unity's NUnit 3.5 without implicit usings: passed as part of that required script. This is not a Unity editor compile.
- New exit regressions pass: the departure crossing the arrival's exit after vacating blocks clearance, clearance releases after the departure passes, and an obstructed distant taxi-in route still permits landing at a safe exit. Main #579's `StandingSpotClear` implementation and window remain unchanged.
- Ground geometry/guard focused run: **14 passed, zero failed**. Rejects 0.5 m parked gap / accepts 1.5 m; rejects 2.5 m queued gap / accepts 3.5 m. All four Q400 BAY-4 routes clear nearby largest parked neighbours, preserving their stop. The final full suite includes these cases.
- Asset audit: **1,811 unique GUIDs, 388 byte-identical art mirrors, 70 committed character materials**. No new authored assets in this task.
- C# 9 syntax: **16 changed files, zero syntax errors**. Presentation map regenerated and checked; `git diff --check` passed. Syntax parsing is not native type checking.

Historical evidence: original main `0087ded0` passed 1,827 headless tests. An older intermediate suite passed 1,850 with seven ground timing/soak/capture failures; those changes and the old airborne taxi-route guard were superseded. Integration at main `1fd4c9fc` passed 1,892 with one busy-day collision. Its trace showed the vacate check ended at 48,888 s while the already-cleared departure crossed the stationary arrival two seconds later; main #579 independently fixed that exit-standing gap. A temporary endpoint-horizon alternative was discarded in favor of main's accepted implementation. Earlier targeted integration passed 67/68; its obsolete BAY-4 fixture was corrected to use a deliberately conflicting route after the real route geometry was repaired. These earlier runs are not final acceptance. Final acceptance is the required green current-main run above.

## Native checklist and remaining limits

No Unity editor/player, builds, screenshots or native EditMode execution was authorized/run. Unverified in Unity: IMGUI click ownership, inspector fit at short heights, actual taxi/turn/saved-in-progress poses, camera-shell lifecycle/rendering, cockpit/passenger/exterior transitions and player frame performance. Run `scripts/test-unity.sh` and native compilation on Unity 6.3 LTS when authorized; inspect day/dusk/night and the documented area checklists. Native camera/batching/sightline tests stay separate from headless evidence.

This task does not certify that every historically reported native failure is resolved. The original single-camera shell fixture was repaired independently in #552 and retained; its native outcome remains unverified. Review the draft and obtain native acceptance before merge.
