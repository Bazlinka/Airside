# Airline Progression and HUD Plan

**Status:** Approved direction
**Owner:** Bailey
**Approved:** 16 September 2026
**Decision:** ADR 0053

## Player-visible outcome

Airside becomes a long-form airline growth game. The player starts with one ATR 42 at
Adelaide, proves the airline on regional contracts, builds capacity and reliability, and
eventually earns domestic and international capability. Every flight produces useful
feedback, while major progression takes sustained play.

Before career information is added, the HUD becomes calmer and easier to understand.
The player should see the current operation, the next useful action and the longer-term
goal without navigating a wall of equal-weight buttons.

## Current-state assessment

The movement simulation, schedule command, persistent fleet, AI traffic and first-flight
guide form a strong operating toy. `CompletedTrips` already increments when a player
aircraft returns to stand, which provides a natural career settlement point.

The gaps are:

- completed flights have no financial or reputation consequence;
- the first-flight guide ends without handing the player a new goal;
- aircraft and route access are catalogue choices rather than earned capabilities;
- player capacity is not yet a managed progression constraint;
- the HUD presents many controls with similar visual weight and repeats information
  across panels;
- developer controls live too close to the normal player interface.

The retired Kingscote airport economy is not a suitable shortcut. It modelled the player
as an airport manager and was removed before the current player-airline direction.

## Core loop

```text
Choose contract -> plan rotations -> operate reliably -> settle each flight
       ^                                                    |
       |                                                    v
Unlock route/aircraft/capacity <- earn tier progress <- funds + reliability
```

### Career state

| Value | Purpose | Player understanding |
|---|---|---|
| Funds | Buy or lease aircraft and future operating capability | What can I afford? |
| Reliability | Reward keeping accepted service commitments | Can partners trust my airline? |
| Operating tier | Gate route, fleet and capacity complexity | What am I qualified to operate? |

Operating tiers are **Provisional**, **Regional**, **Domestic** and **International**.
They are capability milestones with explicit requirements, not renamed player levels.

### Contracts

Contracts are authored, deterministic definitions. Each states:

- origin, destination and eligible aircraft capability;
- required number of completed rotations;
- time or frequency commitment, where applicable;
- payment per eligible rotation and completion reward;
- reliability effect for success, cancellation or expiry;
- capacity and tier requirements;
- the next capability or offer it unlocks.

The first contract is `REG-KGC-INTRO`: Adelaide-Kingscote using the starting ATR 42.
Exact values are tuning data and must be shown to the player before acceptance.

### Pacing guardrails

- Provide feedback after every eligible rotation.
- Make the first meaningful unlock achievable in 2-3 real days of normal play.
- Target 1-2 weeks for an established regional operation, 3-6 weeks for domestic
  capability and multiple months for sustained international capability.
- Do not use real-time wait gates as substitutes for operational requirements.
- Do not reward opening the game without operating the airline.
- Do not punish the player merely for being away.
- Keep recovery possible after mistakes; setbacks should create decisions, not dead saves.

## Capacity and acquisition rules

A new player aircraft may only be acquired when a compatible dedicated stand or operating
base capacity exists. The acquisition flow must show:

- purchase or lease commitment;
- compatible destinations and contracts;
- required capacity;
- expected role in the airline;
- any recurring obligations introduced later.

The six regional bays and terminal gates are currently part of a functioning player/AI
traffic design. Progression must add or explicitly reallocate player capacity; it must not
quietly displace AI aircraft or bypass reservation rules.

## Deterministic architecture

The career layer belongs in Domain/Simulation and remains independent of Unity presentation.
Initial types should express concepts similar to:

- `AirlineCareerState`
- `RouteContractDefinition`
- `ActiveRouteContract`
- `FlightSettlement`
- `SettlementId`

A settlement ID must be stable and applied exactly once. A practical first key is player
aircraft registration plus its completed-trip number, provided migration tests prove that
combination remains unique. Later fleet purchasing may add `FleetLeaseOffer` or equivalent,
but it is outside the starter slice.

All time comes from the injected simulation clock. Tuning definitions are data; presentation
reads state and issues commands but never awards money or progress itself.

### Save migration

The first career implementation advances the save schema from version 5 to version 6.
Migration must:

- preserve every v5 airline, aircraft, schedule, stand, movement and trip-count field;
- initialise the career in a defined Provisional state;
- avoid retroactively paying an unknown number of historical trips;
- persist accepted contracts and processed settlement IDs;
- produce the same result across reload and away catch-up.

## HUD information architecture

### Persistent layer

- compact time and simulation speed;
- airline name/mark;
- funds and reliability once career state exists;
- alerts only when an action is genuinely required;
- one active objective with plain-language progress.

### Competitive layer

- compare the player with live AI airlines using values the simulation genuinely records;
- begin with completed Adelaide rotations, labelled as activity rather than invented market share;
- always show the player's rank and a concrete next rival to pass;
- do not add generic XP, fabricated opponent ratings or permanent leaderboard clutter;
- add passenger, revenue or network-share competition only when those quantities are actually
  simulated and explainable to the player.

### Workspaces

Use one navigation strip with four destinations:

| Workspace | Primary job |
|---|---|
| Operations | Understand current and upcoming flights; act on exceptions |
| Map | Choose destinations and inspect the network |
| Fleet | Inspect aircraft, condition and assignment |
| Contracts | Choose goals and understand rewards and requirements |

Hangar content belongs within Fleet. The flight board belongs within Operations. These do
not need equal top-level buttons of their own.

### Contextual aircraft action

Show one primary action based on state:

| Aircraft state | Primary action |
|---|---|
| Idle and available | Plan flight |
| Scheduled | View plan |
| Active | Track flight |
| Awaiting player stand decision | Assign stand |

Cancel, camera choices and detail views remain available but visually secondary. Disabled
actions explain why they are unavailable rather than leaving a row of unclear buttons.

### Visual contract

- Navy/charcoal base, warm white text and safety yellow for the single current priority.
- Airline colour is an accent, not a competing panel colour.
- Red means warning or destructive action only.
- Sentence case and short natural labels.
- Fewer containers; use spacing, alignment and hierarchy before adding another border.
- Avoid badge clouds, repeated headings, tiny status chips and equal-weight actions.
- Maintain readable text and touch-sized targets at supported Mac resolutions.

### Technology constraint

The first HUD pass reorganises the existing IMGUI implementation. Do not create a second
UI Toolkit or uGUI shell alongside it. A full UI technology migration may be considered
later through a separate ADR after the information architecture is proven.

## Implementation task packets

### Task 1 — HUD shell cleanup

**Outcome:** the current game is easier to read and operate, with no career behaviour yet.

**Scope:** existing HUD/panel presentation, navigation, contextual action hierarchy and
developer-tool separation.

**Acceptance:**

- four player workspaces replace the current collection of equal top-level controls;
- only one major workspace is open at a time;
- current operation, required action and next objective are visible without panel hopping;
- setup, first-flight guide, planner, map, fleet, Hangar, Flights, away summary and stand
  assignment remain reachable and functional;
- no simulation command, timing, route, traffic, save or aircraft behaviour changes;
- visual checks pass at 1280x720, 1440x900 and Retina.

### Task 2 — career domain and save migration

**Outcome:** a new or migrated save has deterministic funds, reliability, tier and contract
state without changing flight operation.

**Scope:** Domain, Simulation, save schema v6, migration and tests. No economy balancing UI.

**Acceptance:** reload, duplicate events, frame rate and away catch-up cannot double-settle a
flight; v5 fixtures migrate without losing operational state; all domain and Unity EditMode
tests pass.

### Task 3 — starter contract

**Outcome:** Adelaide-Kingscote rotations advance a visible contract and unlock the next
regional opportunity.

**Scope:** one authored contract, flight settlement, objective/status presentation and
away-summary result.

**Acceptance:** the player can inspect terms, accept the contract, complete eligible flights,
see each result and retain progress after restart. Unrelated AI or player flights do not pay it.

### Task 4 — regional growth

**Outcome:** the player makes the first meaningful growth choice involving a second contract,
dedicated capacity and another aircraft.

This task is specified only after Task 3 playtesting proves the reward rate and interface.

### Task 5 — domestic and international growth

Jet and widebody progression stays in the backlog until the regional loop has demonstrated
that operating decisions remain interesting over time.

## Explicit non-goals for the first slice

- restoring the retired airport-management economy;
- wages, maintenance wear, fuel pricing, loans or insolvency;
- automated/passive route income while the player is away;
- multiplayer, mobile companion progression or live airline data;
- broad route and aircraft content production;
- a UI framework rewrite;
- monetised acceleration, daily-login rewards or generic XP.

## Required evidence for each behavioural commit

- deterministic domain tests;
- Unity EditMode test result;
- save migration/reload evidence where state changes;
- packaged Mac compile before merge;
- focused playtest steps recorded in `GAME.md`;
- `CHANGELOG.md`, `GAME.md` and any affected ADR updated in the same commit.
