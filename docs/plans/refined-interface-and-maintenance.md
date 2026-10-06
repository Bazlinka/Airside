# Refined interface and maintenance movement

6 October 2026. Bailey approved refining the proposed maintenance journey and whole-interface design in chat. This packet makes that proposal concrete; the HTML study is a design prototype, not game implementation or a UI-framework migration. Existing product-plan files are unchanged.

## Player-visible outcome

An aircraft sent for maintenance prepares without boarding or loading, starts normally, leaves its stand and taxis through actual airport traffic to a fitting maintenance shed. It is manoeuvred continuously into a correctly aligned interior bay, shut down during repair, and returned to an available stand. The interface makes this journey easy to follow and makes airport management look deliberately composed, calm and polished.

Design study and retained views: `../art/interface-refinement-2026-10-06/preview.html`. Open locally in a browser; no dependencies or external network requests. Navigation, fleet filtering, aircraft detail dismissal, taxi/repair previews, time-control acknowledgement and settings toggles are interactive. Follow, flight planning and contract review explain their prototype boundaries. Airport geometry, aircraft positions, money, weather, progress and times are illustrative. No displayed estimate is an implemented simulation forecast.

## Visual direction

Continue ADR 0231's graphite, warm-white, sea-glass and soft-gold interface palette. Refinement comes from composition, type hierarchy and consistent component behaviour. Airport art retains its approved palette and proportions. Original SVG scenery in the study is a schematic backdrop only.

Use the following starting tokens, measured at 100% UI scale:

| Element | Target |
|---|---|
| Spacing | 4 / 8 / 12 / 16 / 24 / 32 px; 20–24 px outer margins |
| Text | 12 px secondary, 14 px body, 16–18 px section, 26–30 px page/aircraft identity |
| Captions | 10–11 px, restrained tracking; no body copy in capitals |
| Font | One readable sans family; tabular numbers for time, funds, speed and availability |
| Panel | Graphite at 94–98% opacity; one subtle edge and soft shadow |
| Shape | 10–12 px surface corners; 6–7 px controls; avoid nested rounded containers |
| Controls | 36–44 px desktop hit targets; 44 px touch targets where supported |
| Motion | 140–180 ms fades/slides; no geometry movement on hover; reduced-motion support |
| Colour | Sea-glass for selection; gold for the one primary action; green for completion; red for actionable failure |

System Arial is used for this dependency-free study. Shipping typography must be checked with the actual Unity font metrics. A different font requires licence/source registration and runtime glyph checks. No new external font is approved or downloaded here.

## Composition and interactions

### Airport overview

One slim status surface contains airline, local time, funds and pause/speed. Reliability is secondary and may disappear at compact widths. A narrow labelled navigation rail provides Operations, Map, Fleet and Contracts, followed by subordinate Career and Settings. One small objective sits at the lower-left edge. The central and lower-middle airfield remain clear.

No persistent live-flight tile stack, large career ring or empty radar frame. Operations and map are explicitly opened. Opening a management workspace replaces the inspector; closing it restores the airport context and selected aircraft. Selection survives workspace changes. Default persistent coverage target is below 20% at 1440×900; an opened aircraft inspector may increase coverage but stays below 30%. Measure actual surface rectangles, not just transparent pixel area. A management page is a deliberate workspace and may cover more.

### Aircraft inspector

One right-side inspector shows registration and type, current activity, a specific reason for any wait, destination/location and relevant timings. An explicit sequence conveys progress without adding multiple badges. The primary action is the useful next command: Plan flight, View plan, Assign stand or Follow aircraft. Secondary commands live below or in an overflow menu. The header and action footer stay visible while long details scroll independently.

Maintenance owns the inspector while the job is active: passengers/loading rows are absent; no misleading flight number, fare revenue or contract progress is added. Show a return estimate only when the simulation can supply a bounded estimate; otherwise say Waiting for route or Return time pending. Repair time and movement time are separately identified.

### Management screens

- Fleet: spacious aircraft rows; identity, activity, location and availability; useful filters; one next action per row. Maintenance details open the shared inspector.
- Operations: readable movement board sorted by actionable disruption then time; maintenance appears as a ground movement. Flight information is factual, with no fabricated departure or ETA.
- Contracts: route, aircraft role/size, payout, deadline and requirements grouped consistently. Disabled assignment gives the exact eligibility reason. Review precedes any purchase or commitment.
- Map: map maximises available space; layers sit in a compact drawer; selected aircraft uses the same inspector and status terms. Selected/held routes receive emphasis rather than showing every route equally.
- Career: current milestone, completed requirements, unlock reward and cost. Move it out of the always-on HUD.
- Flight views: compact identity/status at the top edge, one small view switcher at an edge, expandable telemetry. Preserve windshield and window sightlines. Camera input is gated over controls.
- Title/setup/return/help/settings: same type, spacing and controls. Returning players get factual results and unresolved decisions with a stable Continue action.

### Responsive and accessible behaviour

Verify desktop 1280×720, 1440×900 and 1920×1080, then at enlarged UI scale. At reduced width remove optional readouts before reducing body text. At reduced height scroll the inspector body while preserving identity and actions. On a future touch surface, use a dismissible bottom sheet and larger hit targets; touch support is not implied by this desktop study. Preserve text+icon status, keyboard focus, sensible focus return and reduced motion. Escape closes the frontmost surface; it does not cancel the selected aircraft's job.

## Maintenance simulation design

### Current source and why it needs changing

- `Simulation/HangarTow.cs`: derives visible paths and poses from the check timer while simulation retains the stand. It switches between inside/outbound poses halfway through repair; that is not a continuous turn.
- `Simulation/EngineStartSequence.cs`: maintenance explicitly overrides engines/beacon to off. Ordinary jet/turboprop startup is already type-specific and should be shared.
- `Simulation/AirlineOperations.Fleet.cs`, `StartCheck`: charges, resets wear and starts `CheckUntil` immediately. It does not run a transport job.
- `Simulation/HangarBays.cs`, `HangarFront.cs`: existing candidate, bay and doorway geometry is useful input, but must be checked for actual movement and swept clearance.

### Proposed job phases

Requested → Preparing → Starting/pushback → Taxiing to shed → Waiting at apron → Positioning → Under repair → Exiting → Returning → Parked/available.

Keep maintenance separate from commercial flight scheduling. A movement purpose selects common startup behaviour and explicitly suppresses boarding, baggage/cargo loading, catering and flight settlement. Arriving passengers and cargo must finish leaving before preparation starts. Whether essential fuelling is needed is a readiness condition, not an automatic full passenger turnaround. Existing rotorcraft pad maintenance remains a separate supported path; no fake skid taxi is introduced.

Recommended enclosure sequence is engine-powered taxi to the shed apron, controlled shutdown, then a short tug move through the doorway into the final bay. This recommendation was included in the accepted chat plan. Define entrance alignment, shutdown point and interior nose datum explicitly for each usable bay. Aircraft self-taxi into an enclosed shed is not the default.

### Resources and timing

1. Validate owner, parked location, no flight booking, aircraft suitability, local/outsourced capability and funds.
2. Select a fitting bay and confirm a feasible door-to-bay route. No fitting shed must produce an explicit outsourced/pad option or refusal; never show a local shed journey that cannot happen.
3. Accept the command once and reserve the maintenance job/bay. Retain the existing immediate full-hangar refusal for the first slice; a wait-list requires a separate policy.
4. Request route segments through existing ground traffic reservations. Do not reserve an entire taxi network throughout the repair. Aircraft waits safely at a reserved holding point when blocked; hold reason names the obstacle.
5. Keep the originating stand until the aircraft clears its protected area, then release it. Return requires a fresh compatible stand reservation and may use a different stand.
6. Start repair duration when stationary in the bay after shutdown. Store repair work independently from outbound/return movement. Aircraft remains unavailable until it reaches and parks at its return stand.
7. Reset maintenance wear exactly once on successful repair completion, not when requested. Contract progress and passenger/cargo revenue remain unaffected.
8. Preserve exactly-once payment and completion when commands are retried or catch-up spans several phases.

Bay occupancy lasts through entry, repair and exit clearance. Admission must account for simultaneous repairs of differing lengths/spans, not just same-type slots. Doorway clearance covers wings, tail height, body length and the swept turning envelope. Include tug footprint and practical clearance margin; never teleport, rotate instantly or reverse under engine power.

### Save and catch-up contract

If persisted fields are required, version and migrate them explicitly. Minimum job facts: stable job/aircraft ID, purpose, phase, phase start, bay ID/position, repair duration/progress, paid/completed flags and any return-stand assignment. Rebuild transient segment reservations deterministically after restore; persisted ownership must be reconciled before movement resumes.

Existing saves with only `CheckUntil` need a documented legacy path: complete the existing timed check using its old semantics, without charging again or replaying startup. New requests use the new job. Keep the compatibility path until the schema policy permits removal. Catch-up executes phase boundaries and releases resources in deterministic order, independent of frame rate and presentation visibility.

## Scoped implementation packets

| Slice | Scope | Acceptance and evidence |
|---|---|---|
| 1. Shared visual shell | `HudShell`, `AirlineHudLayout`, shared theme/painter, selected-aircraft layout and matching preview exporter | Airport centre clear; old competing panels removed; selection preserved; actions visible at both desktop sizes and enlarged UI scale. Layout/focus/pointer checks and retained previews. |
| 2. Maintenance job | `Maintenance`, fleet/job state, `AirlineOperations.Fleet`, common startup/boarding gating, save/catch-up | Empty maintenance startup matches normal type sequence; no revenue/boarding; charge and wear settlement once; old/new save restore cases. Deterministic simulation tests. |
| 3. Ground movement and shed placement | `HangarTow` replacement/adaptation, `HangarBays`, ground reservations, presentation pose/tug/engine/audio projections | Regional and jet routes reserve correctly; precise bay fit; blocked traffic waits; different-size simultaneous jobs cannot overlap; return stand reassigned when necessary. Geometry/reservation tests and later native playtest. |
| 4. Workspaces | Fleet, operations, contracts, map, career and shared inspectors | Consistent actions/type/spacing; real eligibility and wait reasons; large fleets scroll correctly; no duplicated status sources. Targeted layout/interaction verification. |
| 5. Remaining surfaces and polish | Flight HUD, title/setup, return report, settings/help, motion/audio | All supported cameras retain clear sightlines; no click-through; reduced motion/UI scale; stable font metrics; later native visual and listening review. |

User authorisation is to refine the plan and visual direction in this packet. Do not label the prototype as implemented Unity behaviour. Preserve domain/presentation separation, injected time, seeded randomness, reservation rules, command idempotency, save compatibility and aircraft-specific startup. Retain the recorded no-Unity restriction; native compilation, screenshots, audio and performance are future owner verification, not evidence from this study.

## Quality gates and remaining refinement

Reject the visual pass if it merely recolours existing cards. Review hierarchy, surface count, usable world area, alignment, optical balance and actual font metrics. Check day/dusk/night and weather contrast using genuine game captures when permitted. Readable static previews do not prove moving-scene usability.

The interactive study covers overview, selected maintenance taxi/repair, fleet, operations, contracts and lightweight career/map/settings composition. Flight-view, title/setup and return-report detailed mockups remain later design work. No new native game rendering or performance claim is made.

First implementation milestone: shared overview/aircraft inspector plus one complete Saab maintenance job. Expand only after traffic, bay placement and save/catch-up are proved. Then a terminal-gate jet demonstrates pushback/start sequencing and larger swept clearances before fleet-wide rollout.

## Implementation follow-through — 7 October 2026

Implemented under Bailey's approval in ADR 0245. Shared IMGUI shell/inspector and actual prop/jet maintenance jobs are integrated, including save v22 migration and deterministic catch-up. Evidence and remaining native checks: `docs/testing/refined-interface-maintenance-2026-10-07/`. Conservatively reserve one entire shed per active job until native swept-clearance validation supports multi-bay concurrency. Timed pad/no-fitting-shed checks retain their prior path.
