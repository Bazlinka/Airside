# Airside player flows and interface contract

7 October 2026. Proposed consolidated flow specification for Bailey's interface planning request. This fills gaps in the 6 October whole-interface packet; it does not replace the approved product plan or claim these proposed interactions are implemented. Visual mockups are subordinate to this document. No new images or runtime changes in this task.

## Player role and the questions the interface must answer

I own an airline at a living airport. I need to know what my aircraft are doing, what I should deal with, what I can afford, what I can operate next, and how today's decisions grow my airline. I also want to explore the airport and watch my flights. The airport/tower run themselves; I do not manually clear every taxi movement, operate service vehicles or pilot aircraft through this management interface.

At entry and after every action, answer:

1. What is happening to my airline now?
2. Does anything require my decision, and why?
3. What can I do with this aircraft/route/base?
4. What will it cost or change before I commit?
5. Did my command succeed, and what happens next?
6. Where can I see the result or recover from a problem?

## Global interaction contract

- One selected aircraft/flight context, shared across airport, fleet, map and flight details. Preserve registration, base, destination draft, filters, scroll and camera context where useful when moving between surfaces.
- One decision workspace at a time. Opening a second replaces the first, with explicit Back to the originating context. Back/Close dismisses UI, never cancels a booking or maintenance job.
- Never infer a command succeeded from a click. Show authoritative acceptance/refusal and the resulting state. Prevent duplicate submission while pending. Revalidate availability, eligibility and funds at commit because the airport keeps running.
- Show costs, resale proceeds, capacity use, service mode and foreseeable consequences before financial/destructive commitments. Use a clear final action naming the affected aircraft/base/contract. Reversible inspection, camera changes and navigation need no confirmation dialog.
- Explain disabled actions with a specific reason and, when possible, a link to the remedy. Do not offer impossible commands as if they were available.
- Preserve the live clock. Current game has no player pause/time-rate controls; menu and workspaces do not stop the airport. Older pause language in docs is not authority for adding those controls.
- Surface actionable interruptions without stealing focus from an unfinished form. Informational events go into a readable activity/result history; camera watching must not hide urgent decisions.
- World labels default to selected/hovered/filtered traffic. Other operators remain inspectable. Real-feed traffic decorations must be distinguished from simulated actors and cannot offer unsupported camera/actions.
- State uses words plus restrained colour. Keyboard/focus and enlarged text must work; Escape closes the frontmost UI with sensible focus return. Clicking/scrolling UI must not move the camera behind it.

## Information architecture follows tasks

The new visual direction can use horizontal navigation, but labels/placement are still proposals. Five destinations are sufficient only if these tasks remain discoverable:

| Destination | Player purpose | Required contents |
|---|---|---|
| Airport | Watch and respond to live operation | Selected flight, attention queue, service/wait details, stand assignment, cameras, optional radar/traffic filters |
| Schedule | Plan and manage committed work | Player flights across bases, plan/change/cancel where supported, repeat schedules, availability and disruptions |
| Network | Understand where I can fly | Route/map discovery, demand/aircraft fit, locked-route reasons, route-to-planner handoff, simulated flight inspection |
| Fleet | Manage the resources I own | All bases/aircraft, profiles/logbooks, purchase, sale, checks, refit, base capacity, supported relocation |
| Career | Choose growth and review results | Goals/tier proof, Contracts prominently available as a tab and context links, airline finances/service history, identity/livery |

Help, settings, save/exit and return briefing belong to the shared shell. Contracts cannot become hard to find merely because a mockup has five tabs. Contract links must also appear from route details, aircraft eligibility and goal cards. The bottom flight tray is a quick entry point, not the entire operations interface.

## Complete task inventory

The identifiers below cover the presently documented player capability scope. Each workflow must be reviewed, rather than treating a screenshot of a page as proof of completion.

| ID | I need to… | Entry point | Must see before acting | Completion/recovery |
|---|---|---|---|---|
| F01 | Start an airline | Title → New airline | Name/code validation, livery, starter aircraft/base, briefing and coach choice; existing-save overwrite consequence if applicable | Enter airport with an explicit first action; validation preserves entered values |
| F02 | Return to my airline | Title → Continue | Save identity and factual away summary, unresolved decisions | Continue to prior useful context; load failure preserves save and offers explicit recovery/new-game choices |
| F03 | Decide what needs attention | Airport/Schedule | Player flights across all bases, available aircraft, actionable wait reason, next departure and pinned objective | Choose the affected flight or aircraft; no fabricated task when everything is healthy |
| F04 | Find somewhere useful to fly | Network, route link or aircraft Plan | Origin/base, distance, demand, seats/cargo role, range/tier compatibility, contract relevance, available/locked reasons | Choose route and hand aircraft/route to planner; unavailable routes link to requirements |
| F05 | Book a rotation | Aircraft, Schedule, Network or Contract | Aircraft/base, destination, departure, availability/turnaround, authoritative dispatch/return/margin data, stand requirements | One accepted booking appears in Schedule and selected context; refusal retains the draft and remedy |
| F06 | Change or cancel a booking | Flight details/Schedule | Existing commitment, permitted changes, cancellation consequences and eligibility | Confirm actual supported command; refresh state and funds/reliability; retain other commitments |
| F07 | Understand a turnaround or delay | Airport selection/Schedule attention | Current service phase/progress, blocker, what is automatic, and what I can influence | Act only where supported; follow blocking aircraft and return to original; pending timing shown honestly |
| F08 | Choose a compatible stand | Aircraft details | Current stand, compatible/available alternatives, reservation/crowding reasons and move semantics | Assign via existing command; show accepted assignment/state without pretending an instant teleport |
| F09 | Watch my or another simulated flight | Airport, Schedule, Fleet or map marker | Correct operator/registration/phase, which views are supported | Follow/exterior/cockpit/cabin switching; return to originating context; cargo/remote limitations explained |
| F10 | Know what a completed flight achieved | Result event, History/Career | Revenue/cost/net outcome, punctuality and cause, reliability movement, contract and goal progress | Result settles once, stays reviewable, and links to next planning decision |
| F11 | Accept or leave a contract | Career → Contracts, route/goal link | Route/role, required rotations, deadline, payment/reward, eligibility and commitment consequences | Accept → choose eligible aircraft → plan; abandonment confirms effects; completed/failed results remain in history |
| F12 | Choose a goal and understand unlocks | Career | Current tier, completed/missing proof, goal reward, costs/capacity implications | Pin goal → link to actual required action; automatic earned tier/result shown; no arbitrary time gate |
| F13 | Manage a growing fleet | Fleet | One roster across every base; state, location, availability, wear, assignment, value; filters/sort | Select profile without leaving roster; actions remain visible and selection stable |
| F14 | Buy an aircraft | Fleet → Add aircraft, selected base | Type/role, price, eligible routes, base capability/capacity/stand, readiness/delivery semantics | Review and buy once; show registration/base and next useful action; refusal gives actual eligibility reason |
| F15 | Sell an aircraft | Aircraft profile | Registration, base, resale proceeds, flight/check eligibility, repeat-plan consequence | Explicit final confirmation; accepted sale removes aircraft/plan and updates balance; refusal preserves context |
| F16 | Maintain an aircraft | Profile or attention item | Wear/check need, price, local vs outsourced/pad path, availability, fitting shed, booked-flight restrictions | Review → request → follow phase/wait → repair → return/available; charge and wear reset once |
| F17 | Refit passenger/cargo role | Eligible aircraft profile | Supported conversion, current role, actual command constraints/cost, resulting route/service/view implications | Revalidate then apply; update role/eligibility; no seat view for freighter; do not invent unsupported conversions |
| F18 | Expand or inspect a base | Fleet → Bases/capacity link | Current capability, used/free capacity, opening/upgrade cost and requirements | Open outstation/upgrade Adelaide via existing command; immediate base/offer eligibility refresh |
| F19 | Move an aircraft between supported bases | Outstation aircraft profile | Supported destination, route range, Adelaide stand/capacity/capability, dispatch cost and ferry status | Confirm ferry to Adelaide, track inbound, preserve registration/history; no commercial reward for ferry |
| F20 | Delegate repeat services | Aircraft → Repeat schedule | Unlock proof, destination, interval, route fit, online-only generation and current commitments | Set/pause/resume/remove plan; show next booking or precise paused/refused reason; resume safely after restore |
| F21 | Review performance and finances | Career → Airline/History | Funds, reliability, recent service margin, fleet/route results, contract history, achievements | Inspect result → route/aircraft/goal remedy; distinguish service margin from one-off rewards |
| F22 | Change airline identity/livery | Career → Airline profile | Current name/colour and live preview, validation | Apply supported rename/livery commands, preserve identity across views/save; no invented paid repaint |
| F23 | Get help or change settings | Menu/Help/F1 | Controls, game rules, scenery credits, current graphics/audio/UI/camera settings | Apply existing settings; retain context; understandable restore-defaults behaviour |
| F24 | End and resume safely | Menu → Quit / Continue | Save status/errors and live-time/offline semantics | Existing save/exit behaviour succeeds; failures are visible; away catch-up does not generate repeat flights |

## Detailed journey: first service

Title → create airline → choose coach → airport shows starter Saab and one actionable prompt → select aircraft → Plan rotation → choose eligible destination (Network is optional help, not a forced detour) → choose departure → review costs/commitment → Schedule → observe turnaround and departure → optionally watch flight → return/unload → flight result → next decision.

Contract acceptance is a linked growth path, not a required action for every flight. When a coach recommends a contract, show why it matters and carry that route into the planner. Never make the player reselect the same aircraft/route across three screens. First-flight guidance must distinguish selecting a route from actually committing it.

## Detailed journey: planning and managing a flight

1. Start from a specific aircraft, route, contract or Schedule. Carry known fields forward. Origin is the selected aircraft's base, not always Adelaide.
2. Aircraft-first: eligible route list filtered for that aircraft. Route-first: compatible owned aircraft shown with base and next availability. Locked options remain explainable.
3. Planner combines availability, departure, forecast and relevant commitment information. Explain occupied seats versus total seats, and estimate versus guaranteed payout. Cost lines must reconcile; do not copy inconsistent generated mock data.
4. Review the proposed rotation. Validate against current simulation; one final scheduling action commits it.
5. Accepted flight appears in Schedule and the quick tray with the same identity/status. Refusal stays beside the form with draft preserved.
6. View plan shows permitted changes. Current commands expose scheduling and cancellation, not a generic atomic EditBooking. Where supported UI replaces a booking by cancel/rebook, disclose the two steps and their consequences; never silently cancel to edit or promise rollback that does not exist.
7. Cancelling is a separate explicit action, with real effects explained. Closing planner/details does not cancel. After departure, hide/refuse unsupported cancellation with reason.
8. Return result records settlement once. Re-plan/repeat actions use actual returned availability, not an assumed arrival estimate.

## Detailed journey: responding to a problem

Attention item → affected flight → factual reason → available response → command result → monitor resolution. Separate:

- **Automatic wait:** servicing, runway/taxi reservation, weather/curfew handling or return-stand wait. State what is automatic; show a forecast only if available. Do not put an inert Fix button beside it.
- **Player decision:** no booking, permitted stand reassignment, check needed, unaffordable operation or paused repeat plan. Show the specific command/requirement.
- **Other aircraft blocks movement:** link to inspect that aircraft, then Back to the original flight; the player cannot issue commands to another airline.
- **Ineligible choice:** explain aircraft range/role, tier, base capability, funds or occupied capacity; offer the relevant Fleet/Career/Base/Route context.

## Detailed journey: fleet growth

Review route demand/goal → identify aircraft role → Fleet chooses delivery base → compare eligible offers → inspect capability/stand/capacity/funds → review purchase → accepted new aircraft/profile → readiness/arrival → assign/plan first service.

A purchase must not strand the player without visible next steps. Capacity and aircraft purchases are separate commitments; show both costs and link between them. Base selection persists while comparing offers. One fleet roster includes on-field, in-flight, outstation and maintaining aircraft; filters never misrepresent a hidden aircraft as sold or unavailable forever.

Sell flow uses an explicit confirmation naming registration, proceeds and repeat-plan removal; a booked/active/maintaining aircraft obeys authoritative eligibility. Ferry flow supports outstation-to-Adelaide only in the documented implementation. Adelaide-to-outstation movement, arbitrary hub swaps, outstation helicopters and outstation cockpit views remain unsupported; do not draw affordances for them.

## Detailed journey: maintenance

Inspect wear/readiness → review check price and applicable local/timed path → confirm check → preparing after unloading → normal startup/pushback → traffic-reserved taxi → apron shutdown → tug positioning → under repair → tug exit → startup/return taxi → compatible stand/parked → available.

Show wait reasons and separate repair work from travel time. Flight/passenger loading is absent during maintenance. Completion of repair does not mean dispatchable until return/parking. Shed full, insufficient funds, booking conflicts and no fitting shed use the actual refusal or timed outsourced/pad path. Job state and payment survive reload; no charge replay. UI dismissal never cancels an accepted job; no unsupported abort action.

## Detailed journey: repeat schedules and returning

After unlock → choose aircraft/route/interval → review online-only operation → enable → first eligible booking → next booking after availability. Pause disables future automatic bookings, not an already committed flight. Remove plan similarly preserves whatever existing booking policy says. A refusal exposes its reason and pauses the plan until addressed.

Quit → save → away period → Continue → catch-up completes previously committed services → factual summary of funds, results and fleet changes → unresolved decisions → Continue airport. Repeat plans do not manufacture additional flights while the game is closed. Summary must distinguish completed commitments from future automation and show negative outcomes clearly.

## Detailed journey: watching versus managing

Select simulated aircraft → choose supported view → compact live identity/status → switch views or open flight details → back to previous view/airport with the same selection. Airport camera can orbit/pan/zoom; managing a flight does not make cockpit instruments into pilot controls. Remote/outstation aircraft expose profile/map/status where no rendered camera exists. Disabled cabin seats on freighters explain the reason. Leaving Follow retains the camera position according to current controls; Overview explicitly resets it.

## Current support and specification gaps

Source evidence establishes capability, not that the proposed flow exists or is usable. Current commands cover scheduling/cancel, stand assignment, contract accept/abandon, pin goal, rename/livery, passenger/freighter role, buys/sells, base opening/upgrade, outstation check, local check, supported ferry and repeat-plan controls. A UI pass must wire these safely rather than create a new economy or transfer system.

Evidence: `docs/product/PROJECT_PLAN.md`; ADRs 0120, 0231, 0239, 0244 and 0245; `docs/plans/refined-interface-and-maintenance.md`; `GAME.md`; `Simulation/AirlineOperations.{Runway,Stands,Career,Fleet,Relocation,Purchase}.cs`; `Presentation/{HudShell,FleetWorkspace,StatsWorkspace,ContractsWorkspace}.cs`.

The earlier whole-interface packet describes pages and a detailed maintenance journey, but not this full task contract. It also contains stale pause/speed design language. The fresh visual studies cover overview, Schedule and aircraft materials only. They do not prove contract, purchase, multi-base, repeat, setup, return or error flows. Detailed screen states/interactions for those journeys remain to be prototyped from this inventory.

## Acceptance before choosing the visual design

Walk through F01–F24 using an early career and a mature multi-base save, at 1280x720/1440x900, compact supported sizes and enlarged UI. For each task record entry, needed information, decisions, commit, accepted result, refusal, Back and reload behaviour. Native interaction proof is distinct from an offline prototype.

Required scenarios: successful first service; unreachable/ineligible route; unaffordable dispatch; changed availability while planning; legitimate cancel; busy stand; automatic ground wait; blocking-aircraft drill-down; eligible/ineligible contract; capacity-constrained purchase; sale with booking; local and timed maintenance; no free shed/return stand; refit restrictions; remote fleet selection; supported ferry; repeat refusal/pause/resume; cargo camera limits; negative return summary; save/load failure. No existing command or player information may disappear merely to fit a mockup.

Next design work: produce clickable states for first service, disruption recovery, purchase/capacity, maintenance and multi-base repeats; then cover the remaining inventory. Only after those flows work should palette, component geometry and polished images be treated as design recommendations. Runtime implementation requires separate native validation; this planning task changes documentation only.
