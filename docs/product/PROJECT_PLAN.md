# Airside Project Plan

## Product and technical blueprint

Version 1.0  
5 September 2026

## Purpose

Airside is a real-time, persistent airport management game for macOS, supported by a focused iPhone companion app. The player takes over a tiny airfield in a chosen real-world location and develops it into a major airport over weeks and months. Aircraft, passengers, ground vehicles, staff and construction operate automatically. The player designs the airport, sets policy, approves opportunities, invests in capacity and responds to meaningful disruptions.

This document turns the agreed concept into a buildable project. It defines what the game must feel like, which systems belong in each release, how the Mac and iPhone products work together, how persistent time and cloud data should behave, and the exact order in which the team should build and test the game.

The first objective is a small Mac prototype that makes one complete aircraft turnaround satisfying to watch and produces believable operational consequences. The project should earn its scale one stable layer at a time. The iPhone companion and broad real-world data arrive after the core simulation, persistence and save integrity are proven.

## Product vision

Airside should feel like a premium miniature airport that is alive whether the player is watching or away. A short visit should reveal what changed, present one or two useful decisions and leave the airport in a better state. Longer sessions should reward watching aircraft movements, improving layouts and studying optional operational detail.

The player fantasy is to become the operator of a growing airport. The player does not fly aircraft, drive service vehicles, roster named employees or own an airline. Airlines remain external organisations with their own needs. The player creates the conditions in which the whole airport succeeds or struggles.

The long-term progression is:

Tiny airfield to regional airport to domestic airport to major domestic airport to international airport to hub to endless improvement

There is no final victory screen. Milestones mark achievement, unlock new operating problems and create new goals. The first release supports one persistent airport. Multiple airport saves, social features and multiplayer remain later possibilities.

## Agreed product decisions

| Area | Decision | Delivery meaning |
|---|---|---|
| Main platform | macOS | Full 3D game, construction, planning, analytics and live operations |
| Companion | iPhone | Quick status, timers, offers, decisions and configurable notifications |
| Time | Real time | About one simulation minute per real minute; real flight and project durations |
| Persistence | Airport keeps operating | On return, elapsed time is reconciled and explained in an away summary |
| Player role | Airport operator | No direct control of aircraft or ground vehicles |
| Starting point | Tiny existing airfield | The first session contains visible operations immediately |
| Location | Exact point in a real-world region | Location influences time zone, daylight, climate, season and demand |
| Building | Hybrid | Free placement for airfield infrastructure; modular terminal construction |
| Visual style | Stylised realism | Premium architectural miniature, readable at distance, detailed where it matters |
| Camera | Free 3D with limits | Smooth orbit, tilt, zoom, follow and useful management views |
| Passengers | Simulated individuals when relevant | Visible behaviour nearby; aggregated cohorts at distance or while offline |
| Traffic | Passenger, cargo and general aviation | Each has distinct facilities, demand and economics |
| Airlines | External organisations | Player approves routes and slots; airlines generate detailed schedules |
| Economy | Realistic and readable | Airport revenue and costs, financing, recovery paths and broad policy controls |
| Progression | Money, reputation and research | Research and major projects take real time |
| Construction | Visible over time | Site preparation, crews, stages, closures and completion appear in the world |
| Failure | Forgiving with consequences | Poor choices cause delays and financial pressure, with credible recovery options |
| Business model | Free for now | Validate that the game is good before choosing monetisation |
| Connectivity | Online-first, resilient offline | Core simulation and saves work without live-data availability |

## Design pillars

### Real time has meaning

Flight sectors, aircraft turnarounds, research and construction use real elapsed time. The game may pause while the player is actively planning, but closing the game resumes the persistent timeline. Time cannot be an arbitrary waiting wall. Every timer must connect to a visible process, a planning choice or a future decision.

### The airport persists

Closing the Mac game does not freeze the airport. The next launch computes what happened between the last trusted save time and the present, updates the airport deterministically and presents a concise summary. The player must be able to understand large changes, delays, completed work and new opportunities without reading a raw event log.

### Watching is rewarding

Aircraft taxi, turn, line up, land, push back and take off with convincing motion. Vehicles follow safe paths and perform readable tasks. Construction changes visibly. Weather, daylight, runway lighting, terminal activity and sound give the world rhythm. Smooth animation and camera behaviour take priority over excessive geometric detail.

### Short sessions work

The normal visit lasts five to ten minutes: check the airport, understand what changed, make a few decisions, start or adjust projects and leave. Longer sessions remain worthwhile for construction, optimisation, analytics and simply watching the operation.

### Depth stays at the system level

The simulation may track many entities, but the player acts through capacity, layout, policy, contracts, investment and priorities. Staffing uses roles and counts. Pricing is mostly automatic with broad controls. Airlines build detailed timetables inside approved routes, frequencies, slots and facility constraints.

### Reality grounds the game without controlling it

Real geography, airports, aircraft performance, time zones, daylight and plausible travel times support immersion. The core aviation world and schedules are generated by the game. External services enrich the experience but never determine whether a save can run.

## Gameplay loop

### Return loop

1. Open the game or companion.
2. Read the away summary and current operational status.
3. Inspect the few events that need attention.
4. Review bottlenecks, cash, reputation and current projects.
5. Approve, reject or adjust routes, investment and policies.
6. Start construction or research.
7. Watch the airport respond.
8. Leave while the airport continues.

### Live management loop

Observe, diagnose, decide, commit resources, watch consequences and measure results

A baggage bottleneck, for example, should be visible as delayed carts and incomplete service tasks, measurable as longer turnaround time, actionable through staffing or infrastructure, and reflected later in on-time performance, airline satisfaction and reputation.

### Building loop

Identify a capacity problem, plan a layout, check cost and operating impact, approve construction, manage temporary disruption, open the facility and measure whether it solved the problem.

Construction must create trade-offs. Building a taxiway may reduce future congestion while causing a temporary closure. Buying adjacent land may protect space for a second runway while delaying a nearer-term terminal improvement.

## Airport simulation

### Simulation layers

The simulation uses three levels of detail so a large airport can remain believable and smooth.

| Layer | Used when | Representation |
|---|---|---|
| Detailed | On screen and operationally relevant | Individual aircraft, vehicles, passenger agents, paths and timed tasks |
| Reduced | Active but distant | Cohorts, reservations, queues and simplified movement |
| Offline | Game closed or long catch-up | Deterministic events and aggregated flows in bounded time steps |

All three layers must produce compatible outcomes. Zooming in may create visible agents from an aggregate cohort, but it must not change the number of passengers, their flight allocation or the airport's finances.

### Aircraft operations

Each flight follows an explicit state model:

Scheduled to inbound to approach to landing to taxi in to at stand to turnaround to pushback to taxi out to takeoff to outbound to completed

The first playable version needs one runway, one taxi route, two stands and a small set of regional aircraft. Aircraft reserve resources before transitions. A flight cannot land without runway capacity, occupy a stand already in use or begin pushback without a valid path.

Aircraft types use data-driven definitions for dimensions, stand category, passenger or cargo capacity, runway needs, cruise performance, turnaround profile, noise and operating cost factors. The simulation should use realistic relationships without claiming certification-grade performance.

### Turnaround operations

A turnaround is a dependency graph rather than one countdown. Typical tasks include:

- deboarding
- baggage unloading
- cleaning
- catering
- refuelling
- technical inspection
- baggage loading
- boarding
- doors closed and pushback preparation

Some tasks run in parallel; others depend on earlier work. Each needs a service resource, access point, duration and completion result. A late baggage load can delay door closure, occupy the stand longer, block an inbound flight and create a visible network effect.

### Passengers

Passengers follow a lightweight journey: arrive, check in or bag drop, security, amenities, gate, boarding, flight, transfer or baggage claim, then exit. Relevant attributes include assigned flight, arrival time, patience, mobility or service need, spending tendency, satisfaction and current goal.

Nearby passengers may be individual agents. Large crowds and offline periods use cohorts with distributions. The game should preserve the operational facts while changing only the visual resolution. Shops, food, toilets, seating, charging and lounges affect satisfaction, dwell time and non-aeronautical revenue as a secondary system.

### Airfield and ground movement

Runways, taxiways, stands, aprons and service roads form connected networks with occupancy and direction rules. Aircraft and service vehicles plan their own routes. The player designs those networks and sets high-level operating rules. The game explains dead ends, unsafe connections and unreachable stands before construction is committed.

Ground services include baggage, fuel, catering, cleaning, stairs or bridges, buses, pushback and light maintenance. The first prototype should use a small shared pool. Later upgrades add dedicated teams, depots, faster equipment and automated allocation.

### Terminals and baggage

Terminals use modular pieces such as a core, check-in hall, security zone, pier, baggage hall, international processing area and lounges. Modules expose capacity, connections, construction stages and operating requirements. Interior passenger flow should be represented through a navigable graph even when the full interior is not rendered.

Baggage moves through check-in, screening, sortation, make-up, cart transport, aircraft loading, unloading and reclaim. It becomes a major source of operational depth after the aircraft turnaround loop is stable.

### Airlines routes and demand

Airlines remain external. Each airline definition contains an interchangeable name, visual identity, fleet, service level, route preferences, price sensitivity, facility requirements and reliability profile. Low-cost, premium, regional and cargo operators should create different development pressure.

The player approves destinations, frequencies, broad operating windows, terminal or stand access and commercial terms. Airlines then generate detailed schedules within airport capacity. Baseline demand comes from location, population, distance, season and route type. Airport quality, connectivity, fees, reliability, facilities and reputation modify that demand.

### Cargo and general aviation

Cargo is a full progression branch with freighters, aprons, warehouses, handling capacity, contracts and night operations. General aviation gives early airports activity through small aircraft, charters, training, medevac and business aviation. Both use the common movement and resource systems while retaining different economics and facility needs.

### Weather seasons and daylight

The airport uses its selected location and time zone for local time, sunrise, sunset and seasonal behaviour. Wind affects runway selection and capacity. Rain, fog, storms, heat, snow or ice appear where appropriate and create operational consequences. A seeded simulated-weather model is always available. Optional live weather may initialise or influence conditions when a suitable service is available.

### Maintenance incidents and regulation

Maintenance remains strategic. Line-maintenance capability, hangars, parts and technical staffing change recovery times and airline confidence. Incidents are occasional and meaningful: medical calls, technical faults, bird strikes, security events, baggage failures, weather closures and diversions.

Preparation determines most outcomes automatically. Larger events may ask the player to close a runway, accept diversions, prioritise a flight or spend more for recovery. Noise, emissions, curfews and nearby land use matter mainly when planning major growth. Second runways, extensions, international facilities and rail links may require reputation, finance, compliance and an approval period.

## Progression and economy

### Progression stages

| Stage | Airport capability | New pressure introduced |
|---|---|---|
| Tiny airfield | One short runway, basic apron, tiny terminal, GA and limited regional service | Basic cash flow, stand use and service reliability |
| Regional | Turboprops, simple scheduled services and expanded terminal | Passenger processing and airline expectations |
| Domestic | Narrow-body jets, more gates and baggage capacity | Peak congestion, faster turnarounds and larger projects |
| Major domestic | Cargo, multiple taxi routes, surface access and stronger analytics | Network bottlenecks, financing and land planning |
| International | Customs, international module, long-haul stands and wide-bodies | Border processing, curfews and disruption recovery |
| Hub | Multiple terminals, possible second runway, major cargo and rail | System-wide coordination, environmental limits and resilience |
| Endless | Ongoing optimisation, prestige projects and changing demand | Efficiency, reliability and self-directed goals |

### Money

Revenue comes from passenger charges, landing and stand fees, retail rents, parking and access, cargo handling, fuel and services, and airline agreements. Costs include wages, utilities, maintenance, security, services, construction, debt interest and infrastructure renewal.

The economy should be realistic in structure and simplified in presentation. The player sees forecasts, operating margin, debt service and the main causes of change. Pricing uses automated market logic with a few policies, such as growth, balanced or yield-focused. Loans and grants allow major investment. A poor plan can create serious pressure, but the player normally has recovery options through finance, project deferral, service reduction or contract changes.

### Reputation

Reputation reflects reliability, passenger experience, safety preparation, airline relationships and responsible growth. It unlocks better airline proposals, desirable routes, larger aircraft, public funding and major approvals. Reputation changes should cite specific causes rather than behaving as an unexplained score.

### Research

Research is a real-time progression layer. Branches may include baggage, passenger processing, surface movement, gate allocation, radar and operations, sustainability, construction, maintenance and commercial capability. Research unlocks tools and infrastructure; it should not replace the need to design a functioning airport.

## Real time persistence

### Core rule

Persistence is calculated from saved state and elapsed time. The application does not need to run every aircraft continuously while closed. On save, the game stores the current state, a trusted timestamp, scheduled future events, active projects, random seed and simulation version. On return, it advances the simulation in deterministic chunks, resolves due events and writes an auditable summary.

### Catch-up sequence

1. Load and validate the last complete save snapshot.
2. Compare the saved timestamp with a monotonic or trusted current time and detect suspicious clock changes.
3. Divide the elapsed period at important event boundaries such as flight movement, project completion and weather change.
4. Run aggregate simulation between boundaries.
5. Resolve capacity conflicts and incidents with the saved random seed.
6. Produce a structured away report.
7. Save the reconciled state before entering live play.

Catch-up work must be bounded. Very long absences use progressively coarser steps while preserving money, project completion, flight outcomes and major events. The player can drill from the summary into a filtered event history.

### Save integrity

Use versioned snapshots plus an append-only command and event journal. Write a new snapshot atomically, verify it, then retain at least one previous known-good save. Every migration is tested against representative older saves. Simulation changes that would alter deterministic replay require an explicit save-version migration.

### Pause and clock handling

The player may pause active play to inspect or build. The saved timeline records whether the application was deliberately paused. Closing or backgrounding ends that planning pause and resumes the persistent clock. Large backward or forward device-clock changes should be flagged and reconciled conservatively so an accidental time change does not corrupt the save.

## Mac and iPhone product split

### macOS game

The Mac product contains the authoritative live simulation, full 3D airport, building tools, detailed inspection, analytics, research, finances, contracts and disruption management. It is built in Unity with C# and exported as a native macOS application. Xcode handles Apple signing, entitlements, packaging and release work where required.

### iPhone companion

The companion is a native SwiftUI app. It shows airport status, arrivals and departures, construction and research progress, cash and reputation, major incidents and airline proposals. It supports a deliberately small set of actions, such as approving an offer, choosing between incident responses and adjusting a broad policy. It does not reproduce the 3D building game.

### Cloud ownership model

CloudKit transports shared records between the apps, but it does not act as a continuously running game server. For the first companion release:

- the Mac remains the authoritative writer for the full airport snapshot;
- the Mac publishes compact, versioned companion projections to CloudKit;
- the iPhone writes small command records rather than rewriting the airport save;
- the next active authoritative simulation validates and applies each command exactly once;
- records include revision, device, creation time and processed status;
- conflict tests cover two devices, repeated delivery and stale projections.

Known completion times can generate local notifications on iPhone. CloudKit change notifications can tell the app that synced records changed. Unpredictable new incidents cannot be generated reliably while every app is closed unless a future server-side simulation or notification service is introduced. The first release should state this honestly: notifications cover known timers and events generated when a device advances the simulation.

### Shared contracts

C# and Swift do not share runtime code directly. They share a documented data contract: record names, identifiers, units, time representation, enum values, schema version and migration rules. Generate test fixtures from that contract and load the same fixtures in Unity and Swift tests. Store all times in UTC and display them in the airport's local time zone.

## Technical architecture

### Technology choices

| Component | Choice | Responsibility |
|---|---|---|
| Mac game | Unity 6 and C# | Simulation host, 3D presentation, input, building and Mac release |
| iPhone app | Xcode and SwiftUI | Companion views, commands, notifications and Apple integration |
| Cloud sync | CloudKit | Player-private projections, commands and save backup records |
| Local persistence | Versioned files on Mac and native local store on iPhone | Fast startup, offline operation and recovery |
| Source control | Git | Code, data definitions, documentation and change review |
| Build automation | Unity batch mode and Xcode command-line builds | Repeatable checks and release candidates |

### Unity boundaries

Organise the Mac code into assemblies with narrow dependencies:

- Airside.Domain contains plain C# entities, value objects, clocks, commands and events.
- Airside.Simulation contains scheduling, resource allocation, demand, economy and catch-up.
- Airside.Persistence contains snapshots, journals, migrations and integrity checks.
- Airside.Presentation contains Unity scenes, views, animation and audio.
- Airside.UI contains management screens, overlays and accessibility settings.
- Airside.Integrations contains weather, map data and CloudKit adapters behind interfaces.
- Airside.Editor contains data validation and authoring tools.
- Airside.Tests contains edit-mode, play-mode, simulation and performance suites.

The domain and simulation layers must run without a Unity scene. This allows fast deterministic tests and prevents the visual frame rate from controlling simulation results. Rendering reads simulation state through stable view models and interpolation.

### Simulation time and determinism

Inject a clock and seeded random source. Use fixed simulation ticks for live operational logic and discrete event scheduling for long-duration work. Never use the frame delta as the source of economic or persistent truth. Give entities stable identifiers and avoid order-dependent iteration when results affect saved state.

### Data driven content

Aircraft, facilities, airlines, services, research and region definitions live in validated data assets rather than scattered code. Each definition includes a schema version and source or licence metadata. Fictional and licensed airline packs use the same interfaces, allowing branding to change without rewriting airline behaviour.

### Performance strategy

Set budgets early for active aircraft, visible vehicles, visible passenger agents, draw calls, memory, save size and catch-up time. Use object pooling, level of detail, instancing, batched path requests and cohort simulation. Profile on the oldest Mac the first release intends to support. A beautiful small airport at a stable frame rate is the acceptance target before expanding scale.

## Data and licensing

### Governing rules

The game must remain playable with bundled data and simulated weather. No paid API is required during development. Every imported dataset and asset receives a licence record containing its source, version or retrieval date, permitted use, attribution requirement, modification rule and redistribution status.

Real airport locations and identifiers may come from public-domain sources such as OurAirports after validation. OpenStreetMap data is available under the ODbL and requires attribution and care around derived databases. Open-Meteo's hosted free API is described for non-commercial use, so it is suitable for early personal development only under its current terms; a public commercial release requires a fresh terms and cost review. Cache policy and attribution must follow the provider's current rules.

Aircraft facts and geographic distances can be modelled from documented sources. Airline names, logos and liveries must be treated separately from factual schedule or route data. The public build should use fictional carriers unless written licences or clearly sufficient rights have been documented. The game data model must allow licensed and fictional branding to be swapped.

### Release gate for external content

Before any public beta, produce a data and asset register. Each entry must have an owner, provenance, licence, attribution text and evidence file. Remove or replace anything marked unknown. Recheck Unity, Apple, data-provider and asset-store terms immediately before release because commercial thresholds and conditions can change.

### Cost policy

Prototype with free tools and data where the relevant terms permit it. Unity Personal currently has an eligibility threshold, while Apple distribution and some production services may create unavoidable costs. The project should record these as release costs rather than silently designing around a permanent zero-cost assumption.

## Artificial intelligence assisted development

### Roles

| Participant | Primary role |
|---|---|
| Bailey | Product owner, final design decisions, playtesting and release authority |
| ChatGPT | Product design, system specifications, planning, explanation and cross-system review |
| Codex | Repository inspection, scoped implementation, builds, tests, debugging and reviewable changes |
| Cursor | Focused implementation and interactive work inside the codebase |
| Claude | Independent architecture review, large-context review and second opinions |

These roles are flexible, but one agent owns each change at a time. Parallel work uses separate branches or worktrees and explicit file boundaries. No agent should make simultaneous overlapping edits to the same system.

### Required task packet

Every implementation task should state:

- the player-visible outcome;
- files or module in scope;
- relevant decisions and invariants;
- acceptance criteria;
- tests or playtest steps;
- what must remain unchanged.

Agents read the current project brief and module documentation before editing. They inspect the actual code and build state, make a narrow change, run the relevant checks and report the evidence. Generated code is reviewed like human code. No merge is based solely on an agent's claim that a build passed.

### Decision discipline

Keep one decision log. A design change records the date, decision, reason, affected systems and migration impact. New ideas go into the backlog rather than directly into the active milestone. The playable build and automated checks are the source of truth for implementation status.

### AI content policy

Record the origin, licence and permitted use of generated or third-party code, images, audio and 3D assets. Do not ask an image or code model to imitate a living artist, reproduce airline liveries or reconstruct proprietary assets. Final assets require human review for visual consistency, rights and technical suitability.

## Repository and documentation structure

This layout reflects the actual repository on disk. Earlier drafts referred to
`mac-game/` and `iphone-companion/`; the real folders are `game/Airside/` and
`companion/AirsideCompanion/`.

```text
Airside/                        Git repository - the single source of truth
  README.md                     Orientation and first-run steps
  GAME.md                       Living status board - read before every task
  CHANGELOG.md                  One line per merged change, newest first
  LICENSES.md                   Licence status (full register in docs/data/)
  AGENTS.md                     Shared working contract for every contributor
  CLAUDE.md                     Pointer for Claude Code
  .cursor/rules/airside.mdc     Pointer for Cursor
  .gitattributes                Unity smart-merge and binary asset rules
  docs/
    product/                    Airside Project Plan.docx, PROJECT_PLAN.md
    architecture/               Technical decisions and data contracts
    decisions/                  Numbered decision records (0001, 0002, ...)
    data/                       ASSET_AND_DATA_REGISTER.md
    testing/                    Acceptance checks and fixtures
  game/Airside/                 Unity 6.3 LTS macOS game - open THIS in Unity
    Assets/Airside/
      Domain/                   Pure rules: time, ids (no UnityEngine types)
      Simulation/               Deterministic simulation, injected clock
      Persistence/              Save schema, load and offline catch-up
      Presentation/             MonoBehaviours, camera, visuals (Unity-facing)
      Editor/                   Editor-only startup helpers
      Tests/EditMode/           Deterministic NUnit tests
      Scenes/                   AirsidePrototype.unity
    ProjectSettings/
  companion/AirsideCompanion/   SwiftUI iPhone companion (after save/sync stable)
  scripts/
    test-unity.sh               Deterministic simulation checks
    build-mac.sh                Local macOS application build
  work/                         Local scratch and builds - git-ignored
```

GAME.md is the short briefing read before every task. It contains the current vision, current milestone, invariants, build instructions, known issues and next approved work. Detailed documents remain in docs so GAME.md stays easy to trust.

### Version control and multi-tool workflow

The repository is the single source of truth for implementation. It is hosted as a
private GitHub repository named `Airside`, and every participant - Bailey, ChatGPT,
Cursor, Codex and Claude - works through that remote. Work that is not committed and
pushed does not exist for the other tools.

- Before a task: `git pull --rebase origin main`.
- One narrow change per commit, tied to a single acceptance criterion. Run
  `scripts/test-unity.sh` and confirm the project compiles in Unity 6.3 LTS.
- Update `GAME.md` and `CHANGELOG.md` in the same commit as any behaviour change,
  then push straight away.
- One change has one owner at a time. Parallel work uses a `feature/<name>` branch
  or a git worktree with explicit, non-overlapping file boundaries.
- A design change adds a dated record under `docs/decisions/`. New ideas go to a
  backlog, not straight into the active milestone.
- `AGENTS.md` carries this contract in full and is the file every tool reads first.
  `CLAUDE.md` and `.cursor/rules/` redirect their tools to it.

## Testing strategy

### Simulation tests

Use deterministic unit and scenario tests for aircraft transitions, resource reservations, turnaround dependencies, passenger cohorts, queues, demand, weather effects, economy and research. Run the same scenario twice with the same seed and confirm the same outcome. Compare one hour of live stepping with one hour of offline catch-up within defined tolerances.

### Persistence tests

Test atomic save replacement, interrupted writes, corrupted latest snapshot, fallback recovery, schema migration, duplicate commands, clock changes and very long absences. Maintain golden saves from every released schema. A release candidate cannot ship if a previous supported save fails to migrate.

### Cloud and companion tests

Use fixtures to verify C# and Swift interpret the same records. Test offline edits, delayed delivery, duplicated CloudKit records, two-device conflicts, stale projections, account changes and quota errors. Confirm that an iPhone command is applied once and that the Mac publishes the resulting revision.

### Play mode and visual tests

Automate the critical operational path where possible: spawn flight, land, taxi, reserve stand, complete services, push back and depart. Maintain screenshot baselines for core views, but use human review for motion, camera comfort, readability, night lighting and sound. Test common colour-vision settings, scalable text and reduced motion options.

### Performance and soak tests

Define tiered airport scenarios and measure frame time, memory, pathfinding cost, simulation tick time, save duration and catch-up duration. Run multi-hour accelerated soak tests to find resource leaks, stuck entities and economic drift. Performance gates apply to target hardware, not only the development Mac.

### Player testing

Each milestone answers one question with observed play:

- Is one aircraft satisfying to watch?
- Can a new player understand why a delay occurred?
- Does returning after an absence feel informative and rewarding?
- Can the player solve a bottleneck without micromanagement?
- Does the iPhone app offer a useful decision in under one minute?

Record behaviour and confusion before asking for opinions. Fix unclear cause and effect before adding more systems.

## Phased milestones

### Phase zero Project foundation

Deliver the repository, Unity and Xcode projects, coding conventions, decision log, data licence register, automated builds and a one-scene performance harness. Create a greybox visual target for the premium miniature style.

Exit criteria: both projects build on the development Mac; the Mac app opens a blank airport scene; tests run from one documented command; no paid service is required.

### Phase one Movement prototype

Build the camera, one runway, taxi graph, two stands and one aircraft type. Implement approach, landing, taxi, stand occupancy, pushback, departure and basic sound with placeholder art.

Exit criteria: fifty consecutive cycles complete without collision, deadlock or manual intervention; movement looks smooth at target frame rate; following and overview cameras feel comfortable.

### Phase two Turnaround vertical slice

Add a small service fleet, task dependencies, stand resources, passengers as cohorts, a minimal schedule, money and delay explanations. Add a compact operations panel.

Exit criteria: the player can identify why every delay occurred; service shortages create visible and numerical consequences; a ten-minute session includes at least one meaningful decision.

### Phase three Persistence prototype

Add versioned saves, event journal, deterministic catch-up, project timers and the away summary. Include clock-change and recovery handling.

Exit criteria: live and offline outcomes match within documented rules; simulated absences from one minute to thirty days finish within the catch-up budget; corrupted latest saves recover to the prior snapshot.

### Phase four First playable airport

Add a tiny existing airfield, basic building, regional routes, simple airline proposals, staffing by role, reputation, research and an economy with recovery paths. Add the first location and simulated weather.

Exit criteria: a new save can progress from its opening state through the first major expansion; the player understands the next goal; no required action depends on an external API.

### Phase five Visual and interaction standard

Replace critical placeholders, establish final camera motion, aircraft animation, day and night lighting, weather presentation, construction stages, interface transitions and airport ambience.

Exit criteria: representative scenes meet the visual target on supported hardware; information remains readable at overview and gate scale; reduced-motion settings work.

### Phase six Systems expansion

Add modular terminals, detailed passenger flow, baggage, cargo, general aviation, maintenance, surface access, incidents, seasons, land purchase, finance, analytics and light approvals. Expand aircraft and facility data.

Exit criteria: each system changes existing decisions and exposes its cause and effect; performance budgets still pass at the defined large-airport tier; features that fail this test return to the backlog.

### Phase seven iPhone companion

Define the shared schema, publish Mac projections, build SwiftUI status and decision flows, add command processing, local caching and notification preferences.

Exit criteria: the phone loads useful status offline, commands survive delayed sync and apply exactly once, conflicts are recoverable, and the main check-in flow takes less than one minute.

### Phase eight Alpha and beta

Complete onboarding, accessibility, settings, save migration, diagnostics, content balance and privacy disclosures. Run internal alpha, then a bounded external beta. Freeze new systems during beta and focus on comprehension, stability and performance.

Exit criteria: no known save-loss defect, no unresolved external-content rights, crash and performance targets met, onboarding completion measured, and the release checklist signed off.

### Phase nine Release and operation

Prepare signed Mac and iPhone builds, store material, support information, licence attribution and a rollback plan. Decide monetisation only after retention and player response are understood. Continue with small updates, save-compatible content and measured expansion.

Exit criteria: release builds match the tested commits, cloud production schema is deployed deliberately, support can diagnose a save without exposing private data, and a restore path exists.

## Concrete build order

1. Create the repository and place this plan in docs/product.
2. Install a supported Unity 6 release and create the mac-game project.
3. Create assemblies for domain, simulation, persistence and presentation.
4. Implement an injected clock, stable identifiers, seeded random source and simulation runner.
5. Define the aircraft state machine and write deterministic transition tests.
6. Build a greybox runway, taxi graph and two stands.
7. Move one aircraft through landing, taxi, stand, pushback and takeoff.
8. Tune camera movement, interpolation and sound until the loop is enjoyable to watch.
9. Add resource reservations and deadlock diagnostics.
10. Implement turnaround tasks and a small automatic service fleet.
11. Add a minimal schedule, delay causes and an operations panel.
12. Add the first money flows and a single meaningful capacity upgrade.
13. Implement atomic snapshot saves and the event journal.
14. Implement deterministic offline catch-up and the away summary.
15. Build the first complete tiny-airfield progression scenario.
16. Add location, local daylight and seeded simulated weather.
17. Add route proposals, airline constraints, reputation and research.
18. Establish the final visual language and replace critical placeholders.
19. Add modular terminal and passenger-flow foundations.
20. Add baggage, then cargo and general aviation using existing resource systems.
21. Add incidents, maintenance, land, surface access, finance and analytics one at a time.
22. Freeze the cloud data contract and create cross-platform fixtures.
23. Create the SwiftUI companion and read-only status projection.
24. Add phone commands, conflict handling and known-timer notifications.
25. Run alpha, fix save and comprehension problems, then enter beta.
26. Complete rights review, performance validation, signing and release preparation.

Do not start detailed terminal interiors, dozens of aircraft, global live data, multiplayer or a full iPhone 3D view before step 15. Those features multiply content and performance work without proving the central loop.

## Risks and responses

| Risk | Early warning | Response |
|---|---|---|
| Scope grows faster than the playable game | Many definitions and assets, no complete aircraft loop | Enforce milestone exits and keep new ideas in the backlog |
| Persistent simulation diverges | Live and catch-up tests produce different finances or delays | Use one deterministic domain model with fixed rules and golden scenarios |
| Save loss or incompatible updates | Migration errors or partial files | Atomic snapshots, journal, previous-save fallback and migration fixtures |
| Large airports perform poorly | Frame time and path queues rise sharply by regional stage | Cohorts, level of detail, pooling, budgets and target-hardware profiling |
| Airport looks busy but causes are unclear | Players cannot explain a delay | Every outcome carries cause records and links to visible evidence |
| Cloud conflicts corrupt state | Both devices rewrite the same snapshot | Mac authority, phone command queue, revisions and idempotent processing |
| Notifications promise events that cannot exist while closed | Incidents appear only after launch | Notify known timers first; add unpredictable push only with an authoritative service |
| External API disappears or becomes paid | Weather or maps fail in a clean build | Bundle core data and provide deterministic simulation fallbacks |
| Airline or asset rights block release | Unknown provenance or recognisable protected branding | Fictional defaults and a maintained data and asset register |
| AI edits make the code inconsistent | Overlapping rewrites or unverified claims | One owner per task, narrow scope, reviewable diffs and required builds |
| Real time feels empty | Player has nothing meaningful to inspect between timers | Layer operations, proposals, analytics and visible progress without notification spam |
| Economy becomes punitive or trivial | Saves spiral irrecoverably or money has no trade-offs | Scenario simulation, recovery tools and broad policy controls |

## Initial scope boundaries

The first playable airport includes one location, one runway, one taxi network, two stands, one terminal module, two regional aircraft types, passenger cohorts, a small ground-service pool, a short schedule, a basic economy, one airline type, one construction upgrade, simulated weather and offline catch-up.

The first playable airport excludes terminal interiors, international processing, cargo, general aviation, detailed baggage, maintenance hangars, regulatory approvals, live weather, real airline branding, multiple saves, multiplayer and the iPhone app. These remain part of the roadmap, but they do not belong in the proof of the central loop.

## First build brief

The first implementation task is to create a greybox Mac scene where one aircraft repeatedly lands, taxis to an available stand, completes a timed turnaround, pushes back, taxis out and departs. The camera can orbit, zoom, follow the aircraft and return to an overview. A small panel shows the flight state and time remaining. The loop uses the domain clock and state machine rather than animation timing as game truth.

Acceptance criteria:

- the full cycle repeats fifty times without manual intervention;
- no aircraft enters an occupied runway segment or stand;
- pausing stops active simulation without breaking the cycle;
- changing frame rate does not change transition times;
- the same seed produces the same event sequence;
- the player can tell the current aircraft state without opening a debug view;
- movement and camera motion are smooth on the target development Mac.

Once this is enjoyable and stable, build the turnaround resource system. Do not begin global airport content before this gate passes.

## Sources to verify at release

- Unity Personal and licensing: https://unity.com/products/unity-personal
- Unity macOS build documentation: https://docs.unity3d.com/6000.0/Documentation/Manual/macos-building.html
- Apple CloudKit and CKSyncEngine: https://developer.apple.com/documentation/cloudkit/cksyncengine-4b4w9
- Apple background execution guidance: https://developer.apple.com/documentation/BackgroundTasks/choosing-background-strategies-for-your-app
- Apple App Review Guidelines: https://developer.apple.com/app-store/review/guidelines/
- Apple Developer Program: https://developer.apple.com/programs/whats-included/
- OurAirports open data: https://ourairports.com/data/
- OpenStreetMap copyright and licence: https://www.openstreetmap.org/copyright
- Open-Meteo terms: https://open-meteo.com/en/terms

These links record the planning basis. Terms, fees, eligibility thresholds and API conditions must be checked again before public testing or release.
