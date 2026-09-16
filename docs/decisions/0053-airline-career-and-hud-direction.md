# 0053 — Airline career progression and HUD direction

**Date:** 16 September 2026
**Status:** Accepted
**Decider:** Bailey

## Decision

Airside will progress as a player-airline career at an autonomous Adelaide Airport.
The core progression loop is authored service contracts, completed rotations, funds,
reliability and capability-based operating tiers: Provisional, Regional, Domestic and
International.

Major progression is intentionally slow, measured over days, weeks and eventually months,
while each completed eligible rotation gives immediate, visible feedback. The game will not
use generic XP, daily-login rewards, paid acceleration or waiting alone as progression.

The HUD will be simplified before career information is added. It will use one persistent
status/objective layer, four main workspaces (Operations, Map, Fleet and Contracts), one major
workspace at a time and one contextual primary aircraft action. The initial cleanup remains
on the existing IMGUI implementation.

## Reason

The current airline simulation has a strong aircraft loop but no lasting consequence beyond
completed-trip count. Its first-flight guide also ends without a new goal. Adding progression
to the current button-heavy HUD would make the interface more cluttered and obscure the
player's next decision.

The older product plan described a Kingscote airport-management economy that no longer
matches the player-airline role accepted in decision 0045. Restoring that system would mix
two incompatible game identities.

## Affected systems

- product plan and milestone ordering;
- HUD navigation and panel hierarchy;
- airline career Domain/Simulation state;
- flight-completion settlement;
- save schema and migration;
- contract, capacity, fleet and route availability;
- onboarding and away summary.

## Migration impact

This decision alone changes no runtime or save data. The later career-state implementation
will introduce save version 6 with an explicit v5 migration that preserves all current
operational state and does not retroactively pay historical trips.

## Guardrails

- Simulation owns career state and settlement; presentation only displays it and issues
  identifiable commands.
- A stable settlement ID prevents duplicate rewards.
- New aircraft require compatible dedicated player capacity.
- AI traffic is not silently displaced to create that capacity.
- The player sees costs, requirements and rewards before committing.
- Absence alone does not penalise progress.
- The retired airport economy is not restored wholesale.
- A future UI framework migration requires a separate decision.

## Implementation order

1. Simplify the existing HUD without changing gameplay.
2. Add deterministic career state and save migration.
3. Integrate the Adelaide-Kingscote starter contract.
4. Playtest before specifying regional fleet growth.

Detailed task packets are recorded in
`docs/product/AIRLINE_PROGRESSION_AND_HUD_PLAN.md`.
