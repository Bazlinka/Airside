# Working ground teams — 9 October 2026

Issue #774 / PR #776. Owner: Codex / Bazlinka.

## Behaviour and limits
Larger airport-provided teams use the shipped characters/hand tools. Bags are partitioned once between distinct carriers; their physical trip budget plus setup, belt transit and clearing establish the minimum player baggage stage. All prep/start/vehicle/countdown/boarding queries use the same duration. Fuel/catering preserve their original stage budgets with setup/clear windows inside them. Upgrades cannot accelerate physical baggage handling beyond this minimum. No hiring, economy, reservation, random-source or save-schema change.

Baggage volume is an explicit deterministic game allowance (65% of typical seats), not a real manifest. Airport provision is per aircraft, with no shared staffing contention. Other airlines receive larger visible crews within their existing ambient service windows; their departure readiness is unchanged. Walking is local to the stand. This slice does not add airport-wide pedestrian routes, terminal work, helicopter teams or wing walkers following a moving tug.

## Focused checks
- 62 checks across RampCrew, ServiceChoreography, TurnaroundCrewWork, CareerExpansion and GroundServiceRun passed.
- 98 related boarding/countdown/flight board/hold/workspace/feasibility/status checks passed.
- After native frame corrections, 26 worker/workload/choreography checks passed; the same 26 passed again after merging current sky/economy main.
- Every catalogue fixed-wing type partitions exactly one bag allowance, finishes before clearing and reproduces its load when time is resampled. Existing all-fleet ground footprint check passed. Early-booking simulation regression remains on stand at the former ready time, and releases at the new completion time.
- Existing far-ahead booking test expected Fuel while current code correctly reports Idle; its stale expectation was corrected. No broad test-domain/test-unity suites were run.

## Native service review
Unity 6.3.23f1 imported/compiled the task and rendered nine scenes using the actual shipped crew rigs, clips and equipment plus ServiceChoreography. Final revision **bbe40abd9256ce3275665f17a13beb6c4dbf2410**: Saab approach/work/clearing, A320 work/clearing, ATR fuel/catering/planeside bags and A320 raised catering platform. All nine PNGs opened and inspected. Three/four workers are readable at the regional/jet hold; bag and nozzle tools appear; departing hold attendants now walk clear and safety cones sit beside their workers. These are isolated native review scenes, not a whole-airport overview or continuous motion recording. Existing vehicle transparency and approximate review door/equipment geometry remain visible and were not changed.

Selected evidence:
- [Saab baggage team](ground-crew-2026-10-09/SF34_Baggage_t38.png)
- [Saab team clearing](ground-crew-2026-10-09/SF34_Baggage_t141.png)
- [A320 baggage team](ground-crew-2026-10-09/A320_Baggage_t44.png)
- [ATR fuel hose and safety cone](ground-crew-2026-10-09/ATR42_Fuel_t45.png)

## Packaged execution
Clean stamped **bbe40abd** Mac build succeeded (`Build Finished, Result: Success.`); executable contains x86_64 and arm64. Unity's initial Bee compilation stall was recovered by the build script's bounded cache-graph retry. Build identity is retained alongside the images. Unity-generated package/pipeline/material review rewrites were restored only in this isolated worktree.

`python3 scripts/agent-gameplay.py --profile full --features booking,save,views --timeout 90 --journey-timeout 600`: nine booking/save/cancellation/view actions passed with zero reported runtime errors. Both booked and cancelled saves round-tripped. The 40× ADL–KGC round trip passed in 187.89 seconds, observing departure, outbound, arrival, return, landing/taxi-in and the restored overview. Journey log scan found no runtime errors. Packaged follow, TaxiOut and completed-overview frames were opened and inspected; the final airport frame shows ambient crews at stands. Run: `work/agent-gameplay/20261009-225325-28acc3b8`. [Summary](ground-crew-2026-10-09/gameplay-summary.json), [feature report](ground-crew-2026-10-09/features-gameplay-report.json), [completed overview](ground-crew-2026-10-09/completed-overview.png).

This build is the clean ground-team revision before merging subsequent sky/unused FlightCostModel main changes; no claim that the subsequently merged whole main was packaged or visually reviewed. Crew source is identical in the merge. Personal saves and running game were not touched. Performance, long soak, full airport overview/night crew visibility and live-time motion pacing remain unverified.
