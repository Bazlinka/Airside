# Second report-only bug hunt — 9 October 2026

Tasks #751 and #753. **Fifteen additional findings (16–30), no fixes.** The first seven were published in PR #752; the eight remaining findings were collected after Bailey resumed the hunt. These are separate from the [first fifteen](../bug-hunt-2026-10-09/README.md). Findings 23–30 include two medium-severity data/navigation defects and six low-severity presentation/copy issues.

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

## Evidence for the remaining eight

Findings 23–30 were investigated on clean native commit `4c24425c07deab1d27a8169b353978d86c348966` (dirty=false), stamped build 2026-10-09 06:45 UTC. Two additional private Bell sessions used the existing 40× clock; a bounded weather probe ran at ordinary clock speed. The Bell is seeded into a Starter base to inspect type-specific UI, conversion and graphics, not to establish purchase/base progression correctness. Normal-speed career behavior, full playthrough and performance remain unverified.

The scratch copy of the compiled app has a unique bundle identity and an ad hoc signature solely to route native controls through macOS LaunchServices. Game code/art/build identity are unchanged; personal saves and concurrent tools were preserved. Screenshots were opened and inspected. The manual sessions logged all 36 observation actions and exited, but did not emit an aggregate completion/report file; no overall QA pass is claimed from those runs. The final 126-second rotor probe emitted a passed execution report with zero captured runtime errors. Individual native observations plus the stated source traces support the findings; that runner pass does not endorse gameplay correctness.

Plans only describe automated observations: manual clicks/refits/scrolls are given in each finding, so replaying a snapshot-only plan does not reproduce them unaided. [Evidence manifest](evidence/remaining-manifest.json) maps the selected frames to original observations. No personal save is published.

## 23. Bell freighter receives a 15-tonne jet payload and the wrong revenue denominator

**Medium — aircraft/economy data.** In a private career with the Bell seeded, use its ordinary Fleet refit action ($2,400), then open Kingscote planning. The load is **2.4/15 t freight**, with expected profit −$24. A Bell 412 cannot have a 15-tonne payload: Bell's [412 fact sheet](https://www.bellflight.com/products/-/media/site-specific/bell-flight/documents/products/412/bell-412epi-fact-sheet.pdf) gives a whole-aircraft maximum gross weight around 5.4 tonnes for the EPI variant; even the newer variants remain below six tonnes. This is an envelope sanity check, not a proposed EP payload specification.

Expected: an explicit appropriate helicopter capacity, or refusal of an unsupported freight conversion. `Simulation/FreightRates.CapacityTonnes` has thirteen fixed-wing cases and no B412 case; its non-widebody fallback returns 15. `RouteForecast.ForFreight` divides route demand by this capacity, so the error also depresses forecast and settlement revenue. The missing capacity is independent of QA ownership progression.

Evidence: [Bell freight forecast](evidence/remaining-bell-forecast.png); ordinary refit and [expanded profile](evidence/remaining-bell-profile.png).

## 24. The first-flight distinction says “1 flights to go”

**Low — copy.** Select an aircraft with zero completed trips and expand Fleet details. Bell VH-TS1 displays “Next distinction: First flight · 1 flights to go.” Expected: “1 flight to go.” `Presentation/FleetWorkspace` builds the distinction line with an unconditional plural, for both Adelaide and outstation entries. Counted once across those paths.

Evidence: [expanded Bell details](evidence/remaining-bell-profile.png).

## 25. Career goals say “fly” while progressing through ownership

**Low — misleading objective wording.** The Career panel shows “Fly 3 aircraft · 2/3” while its footer reports zero flights; neither owned aircraft has completed a flight. `Simulation/CareerRoadmap.Evaluate` uses fleet count for this objective and the finale fleet objective, and ownership predicates for “Fly a jet”/“Fly a widebody.” Buying or seeding an aircraft progresses these goals without flying it.

Expected: wording that tells the player the actual ownership requirement, or a flown-aircraft measure if that is the intended goal. This report does **not** propose changing tier rules or claim an illicit unlock. The seeded ownership exposes the same wording/measuring mismatch as an ordinary purchase.

Evidence: [Career ownership versus zero flights](evidence/remaining-career.png).

## 26. Extra contract offers are acknowledged but unreachable

**Medium — inaccessible gameplay choices.** At 1280×800, Contracts renders five cards and “1 more on offer.” Scrolling over the offers leaves the same five cards; there is no next-page control. The sixth offer cannot be inspected or accepted there. Expected: scroll or page through every offered contract.

`Presentation/ContractsWorkspacePainter.PaintOffers` limits rendering to `min(model.Offers.Count, layout.VisibleOffers)`, then adds the remainder caption. `AirsidePrototype.DrawContractsWorkspace` renders that draw list without a scroll view or paging action. The painter's three-offer assumption is stale: featured and rotating offers are combined, and recovery can add another. Shorter windows hide more choices. This is distinct from a particular offer being locked by eligibility.

Evidence: [five cards plus one hidden](evidence/remaining-contracts.png), [unchanged after scrolling](evidence/remaining-contracts-after-scroll.png).

## 27. Fleet never displays its freight-role/payload explanation

**Low — omitted decision information.** Before and after a normal refit, expanded Fleet details give no passenger/freight role, payload or freight pay explanation, although there is ample space beneath the assignment. The button says “TO PASSENGERS $2,400” after conversion; it does not explain the hold or the way freight pays.

Expected: make that existing explanatory fact available in the aircraft details so the player can assess the refit. `FleetWorkspace` constructs `RoleLine` (“Freighter · … t payload, paid on the freight a route offers” / passenger conversion capacity), but its painter only tests whether the string is nonempty to show the conversion button. No draw call paints its value. This affects fixed-wing aircraft too; it is separate from Bell's incorrect numeric capacity in finding 23.

Evidence: [expanded converted profile with unused space](evidence/remaining-bell-profile.png). This is a presentation omission, not lost simulation state.

## 28. Bell freight refits omit the promised CARGO title and registration

**Low — incomplete aircraft presentation.** Refit Bell VH-TS1, enter Exterior and inspect it in daylight. Its colour/stripe changes, but its fuselage has no operator CARGO title or player registration. [ADR 0194](../../decisions/0194-freight-flights-and-gear-pivot.md) specifies an operator CARGO fuselage title for converted aircraft.

Expected: fitted Bell identity/cargo markings, or a clearly unsupported conversion. `AirsidePrototype.FleetVisuals.PaintFleetLivery` returns from the rotorcraft branch after colouring fitted panels, before `EnsureAircraftIdentityMarkings`. The fixed-wing identity layout table has no Bell layout. Cargo colour change and Bell's window exception are **not** counted as bugs; the missing identity/title is the finding.

Evidence: [converted Bell Exterior](evidence/remaining-bell-cargo.png); [profile confirming freight role](evidence/remaining-bell-profile.png).

## 29. Fleet clips the start of its helipad-location label

**Low — text clipping.** At 1280×800, the Bell roster row's right location reads “elipad spot 2,” while the selected details correctly say “Helipad spot 2.” Expected: the complete location or an intentional readable abbreviation.

`FleetWorkspacePainter.PaintRosterRow` reserves 74 pixels for every location, draws the value at 12-point size in a 68-pixel right-aligned box, and does not fit or abbreviate longer stand labels. This occurs on a normal owned Bell as well as the seeded test aircraft; it does not depend on an unsupported return to a Starter base.

Evidence: [roster label versus full detail](evidence/remaining-bell-profile.png).

## 30. Turning blur off leaves Bell main/tail rotor blur enabled

**Low — inconsistent graphics/comfort control.** Launch the private native Bell scenario with `-airsideGraphicsOff propblur`, the existing temporary equivalent of `PropellerBlur=false`, then book Kingscote and enter Exterior. Both rotor blur discs are still visible during departure/cruise.

Expected: the shared rotating-aircraft blur opt-out also suppresses these discs, particularly when using the launch override to compare rendering cost. `AirsideSettings.ApplyGraphicsOff` sets `PropellerBlur=false`; fixed-wing propeller and engine-fan updates check it, but `AirsidePrototype.Helicopter.SpinHelicopterRotors` computes and enables the main/tail disc alpha without consulting it. Blade rotation should continue; the disabled blur layer is the issue. Native verification used the temporary flag, not a saved Options toggle; the latter uses the same property but was not separately changed.

Evidence: [blur still visible](evidence/remaining-rotor-blur-off.png), [launch script](evidence/rotor-blur-off-launch.py), [plan](evidence/rotor-blur-off-plan.json), [completed runtime report](evidence/rotor-blur-off-gameplay-report.json), [log](evidence/rotor-blur-off-player.log).

## Follow-up exclusions

No fixes were made. Cloud weather differences were probed but not counted without a clear defect. The initial exterior-camera frame was taken during its entry transition and was excluded. Bell return/stand availability with a QA-seeded Starter base was excluded as an unsupported fixture, as in the earlier collection. Prior report findings and the distant-origin defect already recorded as 21 were not counted again.
