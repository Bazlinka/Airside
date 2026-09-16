# Airside Product Plan

Version 3.0
16 September 2026

## Approved product direction

Airside is an airline management game set inside an autonomous Adelaide Airport.
The player owns and grows an airline; the airport, tower and other operators continue
to function around it. The first aircraft is an ATR 42 and the long-term aspiration is
to build a credible domestic and international operation.

This plan supersedes the old Kingscote airport-management plan. Decision 0045 remains
the foundation for the player's role. Decision 0053 defines the career progression and
HUD direction approved by Bailey on 16 September 2026.

## Product promise

Airside should take time to master and progress through, without making individual
sessions feel unrewarding.

- **Slow progression:** meaningful aircraft, route and capability unlocks take days,
  weeks and eventually months of play.
- **Fast feedback:** every completed rotation advances a visible objective and reports
  its operational and financial result.
- **Operational ownership:** progress comes from planning and reliably operating an
  airline, not from passive waiting, generic XP or daily-login rewards.
- **A living airport:** AI traffic makes Adelaide feel active but does not compete for
  resources unfairly or obscure the player's next decision.

## Current playable foundation

The game already provides the foundation for the career:

- a deterministic aircraft movement and reservation simulation;
- a player airline with a persistent ATR 42;
- player-selected destinations, departure times and stands;
- schedule, cancel and stand-assignment commands;
- autonomous Adelaide traffic across regional, domestic and international aircraft;
- first-flight onboarding, map, fleet, Hangar, flight board, follow camera and
  away-summary flows;
- save compatibility and deterministic headless and Unity tests.

The missing layer is consequence: a completed flight currently increases the aircraft's
trip count, but does not materially grow the airline or create a longer-term objective.

## Career loop

The career is built around airline service contracts:

1. Accept an authored route contract.
2. Schedule and operate the required rotations.
3. Complete them safely and reliably.
4. Receive funds, reliability movement and contract progress after each rotation.
5. Unlock the capacity, aircraft and contracts needed for the next operating tier.

Three persistent values carry the progression:

- **Funds** pay for airline growth and later operating commitments.
- **Reliability** measures whether the airline delivers the service it promises.
- **Operating tier** represents real capability: Provisional, Regional, Domestic and
  International. A tier is earned by completing explicit requirements, not by filling
  an arbitrary experience bar.

Initial pacing targets, to be tuned through playtesting:

| Stage | Target time | Meaningful outcome |
|---|---:|---|
| Provisional | 2-3 real days | Complete the first service contract and prove reliability |
| Regional | 1-2 weeks | Add regional capacity, routes and a second aircraft |
| Domestic | 3-6 weeks | Operate regular interstate jet services |
| International | Multiple months | Sustain widebody-capable international operations |

These are progression targets, not mandatory real-time lockouts. A good operator can
move faster, while a player who returns after time away is not punished for absence.

## First career vertical slice

The first implementation proves the model with one authored Adelaide-Kingscote service
contract:

- the player sees a clear contract objective;
- each eligible completed rotation pays a fixed, transparent amount;
- cancellations or broken commitments can reduce reliability;
- contract progress persists and settles exactly once;
- completing the contract unlocks the next regional contract;
- the result is visible immediately in the HUD and after returning to the game.

Do not add a full economy, wages, maintenance, fuel market or automated route income in
this slice. The player can currently schedule individual round trips, so a broad economy
would create fake depth before the operating model can support it.

## Fleet and capacity progression

Aircraft acquisition follows operational readiness:

- every aircraft requires compatible dedicated Adelaide capacity before acquisition;
- route and aircraft offers state their requirements and practical purpose;
- the next purchase should open a useful decision, not merely make a number larger;
- current AI stands are not silently reassigned to solve player capacity;
- aircraft families and destinations are introduced in authored steps so the player can
  understand what changed.

## HUD direction

The present HUD is functional but visually busy, button-heavy and hard to scan. Career
systems must not be added as another layer of panels and badges. The HUD is simplified
before the progression UI is introduced.

The target structure is:

- one quiet persistent status strip for time, airline, funds and reliability;
- one active objective showing what matters now and its progress;
- one navigation strip: **Operations, Map, Fleet, Contracts**;
- one major workspace open at a time;
- one contextual primary aircraft action, such as **Plan**, **Track**, **View plan** or
  **Assign stand**, rather than several competing buttons;
- secondary actions visually subordinate to the next useful action;
- developer tools isolated from the player interface.

The visual language is game-like but restrained: navy and charcoal foundations, warm
white text, safety yellow only for priority, airline colour used sparingly and red reserved
for warnings. Use sentence case and natural language. Reduce nested boxes, badges and
repeated labels.

The first cleanup stays on the existing IMGUI path. Introducing a parallel UI framework
would increase inconsistency and risk; any later technology migration requires its own
decision and acceptance plan.

## Delivery sequence

1. **HUD information architecture:** reorganise the current controls without changing
   simulation behaviour.
2. **Career domain:** add deterministic contract, funds, reliability and tier state with
   save migration and settlement idempotency.
3. **Starter contract:** connect Adelaide-Kingscote completion to the HUD, save and
   away-summary flows.
4. **Regional growth:** add a second contract, capacity decision and aircraft acquisition.
5. **Domestic growth:** introduce interstate jet operations only after the regional loop
   is proven enjoyable.
6. **International growth:** treat widebody operations as a long-term capability goal,
   not an early catalogue purchase.

The detailed task packets and acceptance criteria are in
`AIRLINE_PROGRESSION_AND_HUD_PLAN.md`.

## Safety and fairness rules

- No purchasable shortcut, daily-login streak or generic XP grind.
- No state-changing command is applied more than once.
- Frame rate and reload timing do not change career outcomes.
- Save changes have explicit versions and migrations.
- AI traffic cannot consume capacity already promised to the player.
- Costs and rewards are disclosed before the player commits.
- Missing a real-world day does not damage the airline.

## Verification standard

Each behavioural slice requires deterministic domain tests, Unity EditMode coverage,
save migration coverage and a Mac playtest. HUD work is checked at 1280x720, 1440x900
and Retina resolution across setup, first flight, planner, map, fleet, Hangar, Flights,
away summary and stand assignment.

At any moment the game should answer three questions without opening several panels:

1. What is my airline doing now?
2. What needs my attention?
3. What am I working toward?
