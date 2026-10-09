# Second report-only bug hunt — 9 October 2026

Task #751. **Seven additional findings (16–22), no fixes.** Bailey interrupted the collection and requested immediate merge. The requested target of fifteen additional findings was not reached; eight remain uncollected. These are separate from the [first fifteen](../bug-hunt-2026-10-09/README.md).

## Evidence and limits

Native macOS player built once from clean commit `0cca70b3875372bfd03fbce87e9190319c8703f7` (dirty=false), using the existing 40× QA clock and isolated soak careers. Manual UI interaction with automatic snapshots; screenshots were opened and inspected. The compact and jet sessions each completed 36 observations in approximately 560 seconds, with zero captured runtime errors. Passing observations establish capture execution, not a gameplay pass.

The seven findings below combine native observations with source traces on that revision. Other contributors have subsequently changed main; reproduce on current main before fixing. Larger aircraft were added through the existing hidden QA fleet seed, which bypasses ownership/base progression. Finding 20's eligibility omission also applies to legitimate Domestic-tier A220 ownership; that normal career path was not separately played. Normal-speed behavior and performance were not verified. A later helicopter probe added no confirmed findings and was stopped by the publication request; a helicopter seeded into a Starter base is not evidence of a normal-career landing defect.

Only private-career facts are exported, not personal saves. Logs, plans and reports retain their original local QA paths for provenance; those paths are not portable replay instructions. Source references below are relative to `game/Airside/Assets/Airside/` at the tested commit. No source, tests, dependencies or configuration changed. No broad test suite was run for this docs-only publication.

## 16. Timed-contract profit includes a bonus the chosen schedule cannot earn

**Medium — misleading financial forecast.** Accept the Kingscote medical offer (one Saab rotation, three-hour deadline), then choose a departure later than its deadline. In the compact session, acceptance was around 14:30, due around 17:30; at 15:08 the planner offered departure 18:23 while advertising profit +$563, return $865 and Contract +$469. Booking was made for 18:26. The contract subsequently expired and reliability dropped to 95; the forecast bonus could never be earned on this schedule.

Expected: exclude the unavailable bonus or warn that this departure misses the commitment. Trace: `Presentation/FlightPlanner.cs`, `ExpectedRevenue`, adds an eligible active contract's payment/reward without a departure or completion time. `RouteMapWorkspace.FillDestination` calls it before calculating departure. This is distinct from the acceptance defect in finding 22: here the player deliberately chooses a late schedule after a feasible acceptance.

Evidence: [accepted offer](evidence/manual-compact-02.png), [impossible forecast](evidence/manual-compact-06.png), [career facts](evidence/private-career-facts.json).

## 17. Reviewing a booking drifts its departure; UPDATE silently postpones it

**Medium — unintended schedule change.** Book Kingscote for 18:26, open View plan around 16:20, then leave the time buttons untouched. At 16:38, the picker said 18:45 while the same panel still said Booked Kingscote at 18:26. Clicking UPDATE around 17:06 moved the booking to 19:12.

Expected: reviewing an existing booking preserves its absolute time until the player changes it. Trace: `Presentation/AirsidePrototype.Airline.cs`, `SetPlanningAircraft`, copies the remaining delay once; `RouteMapWorkspace.FillDestination` adds that constant delay to the advancing clock each frame; `ScheduleFromPlanner` writes the resulting later time. Opening alone does not change the actual booking; UPDATE commits the drift.

Evidence: [early review](evidence/manual-compact-14.png), [drifted picker](evidence/manual-compact-15.png), [updated booking](evidence/manual-compact-18.png).

## 18. Future bookings falsely report active fuelling with two minutes left

**Low — misleading status/countdown.** Book a flight several hours ahead. At 15:28, the aircraft booked for 18:26 already said Fuelling 0%, 2 min left. That status persisted through the future waiting period.

Expected: distinguish waiting for preparation from work currently happening, with a countdown that includes the wait. Trace: `Simulation/DeparturePrep.cs`, `For`, clamps negative elapsed time before the future prep start to zero, returning Fuel with its stage duration remaining. Actual service timing retains the future start; this is a status projection defect, not proof of stuck fuelling. It also occurs without reopening or updating the plan.

Evidence: [future booking status](evidence/manual-compact-08.png), [later unchanged status](evidence/manual-compact-15.png).

## 19. Planner blocks the cash-recovery contract's underwritten dispatch

**High — recovery path unusable through ordinary planning.** In the private career, cancel a booking to restore $2,800, refit the founding Saab for $400, start its $560 check, and buy another Saab for $1,600, leaving $240. Accept the generated Kingscote recovery offer `REC-0-SF34`, select the free new Saab VH-PAA, and plan Kingscote. The planner disabled PLAN: dispatch costs $302, funds $240.

Expected: allow the recovery contract's designed dispatch credit. Trace: `Simulation/AirlineCareerState.TryChargeRecoveryDispatch` permits REC dispatch below zero; `AirlineOperations.Runway.ScheduleDeparture` uses it. `Presentation/RouteMapWorkspace.FillDestination` blocks on cash before that command can execute. Existing `FullCareerTests.RecoveryContract_CanDispatchWithEmptyCashAndRepaysOnSettlement` describes the intended simulation path; it was inspected, not rerun for this report.

Evidence: [recovery offer](evidence/manual-compact-32.png), [blocked funded recovery](evidence/manual-compact-36.png), [private facts](evidence/private-career-facts.json).

## 20. International routes appear available before the required airline tier

**Medium — contradictory eligibility.** With the QA-seeded A220 VH-TS2, Provisional career and $16,275, select Auckland. The planner listed it under AVAILABLE, said “Tasman route. You can fly it”, and enabled PLAN. Clicking refused: Auckland requires the International tier.

Expected: show the career-tier gate before offering a booking. Trace: `Presentation/FlightPlanner.DestinationsFor` and `RouteMapWorkspace.FillDestination` check aircraft range/band but omit airline tier; `Simulation/AirlineOperations.Runway.ScheduleDeparture` checks `RouteAccess.RequiredTier`. `AircraftAcquisition` unlocks A220 ownership at Domestic, so ordinary ownership can precede International eligibility. The native refusal was observed directly; the automatic screenshot retained the offered state after the short toast disappeared.

Evidence: [false availability](evidence/manual-jets-14.png), [session report](evidence/manual-jets-gameplay-report.json). QA-seeded ownership limit applies as stated above.

## 21. Returning from a distant flight leaves the airport hidden

**High — airport overview unavailable.** Watch the A220 Adelaide–Melbourne flight from its passenger/cockpit views, then click Overview around 18:20. Open and close Fleet, and click Overview again. The airport remained absent behind fog/sky at 18:31, 18:35 and the retained 18:40 frame. Logs repeatedly recorded `cockpit False`, `origin 248000,-600000`, `airport False`.

Expected: return to Adelaide's visible airport and world origin. Trace: `Presentation/AirsideCameraController.FollowTarget` exposes the retained target after `ReturnToOverview` clears `_following`. `AirsidePrototype.FlightWorld.UpdateFlightWorld` uses that target without checking whether follow is active, restoring the distant origin after `ExitCockpit` resets it. Following the parked Saab later restored the airport. Ownership was QA-seeded; the stale-target path is shared with normal flight views.

Evidence: [persistent missing airport](evidence/manual-jets-26.png), [player log](evidence/manual-jets-player.log), [later restored airport](evidence/manual-jets-34.png).

## 22. Timed-contract acceptance ignores maintenance unavailability

**High — impossible commitment accepted.** Start the only owned Saab VH-PAX's check around 19:43; the native toast advertised completion at 23:01. The Kingscote three-hour medical offer remained actionable at 19:47. Confirm it around 19:51: the active contract was accepted, due around 22:51, before the aircraft's check could finish, even before adding flying time.

Expected: refuse an offer the eligible fleet cannot finish before its deadline. Trace: `Simulation/AirlineOperations.CanStillFinish` counts eligible aircraft and takes `SecondsUntilHome`; its AtStand branch returns zero without considering `MaintenanceJob` or `CheckUntil`. `AcceptContract` and `ContractsWorkspace.FillOffers` rely on that feasibility check. The original native check-end toast and accepted card were inspected during play; the retained periodic frame shows CONFIRM, and the private save facts corroborate acceptance and maintenance state.

Evidence: [maintenance aircraft](evidence/manual-jets-32.png), [impossible offer confirmation](evidence/manual-jets-33.png), [private acceptance/maintenance facts](evidence/private-career-facts.json).

## Reproduction handoff

Use the existing isolated agent gameplay workflow in [its guide](../agent-gameplay/README.md), with a clean current stamped build and private saves. The retained plans only schedule observations; repeat the described native UI actions to reproduce the findings. Do not use the personal career to recreate the cash/maintenance cases. Prioritize recovery blocking (19), overview restoration (21), and impossible contract acceptance (22). The remaining eight findings from the requested second batch have not been collected.
