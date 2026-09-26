# 0121 — One coherent career

Date: 26 September 2026. Author: Claude (Bailey asked for the career to be analysed, its logic
and progression improved, and for it to make sense end to end).

## Context

ADR 0120's self-led roadmap was layered over four older progression systems written at
different times: the five-chapter `Campaign`, `DailyService`, `CareerProgress.NextTier` and
`CareerMilestones`. An audit of `main` at 3dfc0bc found player-facing contradictions and
real defects:

- A rebought aircraft reused a sold aircraft's registration, so its first flights settled
  against already-spent keys and paid nothing while still charging dispatch.
- There was no way to drop an active contract. Broke with a contract the airline could no
  longer fly, the recovery contract could not be accepted: a dead save.
- Tier checks were independent, so an airline that finished Regional-stage goals while still
  Provisional could jump straight to Domestic with the contract goal still pinned forever.
- Cairns and Darwin counted as "international" for goals and the finale.
- Three different fleet counts (Adelaide-only, distinct types, whole airline) drove the
  objective card, route guidance and goals.
- The next-aircraft hint never moved past the ATR 42; completed pins never released;
  "Reach 70% reliability" was complete on day one; nothing announced a tier-up or the finale.
- Campaign chapter rewards and the daily bonus were described in code and tests but never
  paid, and every HUD fallback that read them was unreachable.

## Decision

`CareerRoadmap` is the single authority for progression; `AirlineCareerState.EvaluateTier`
promotes one tier at a time in order using it. Everything else reads from it or is removed.

- **Removed:** `Campaign`, `DailyService`, `CareerProgress.NextTier` and the legacy tier
  constants. Old `campaign:*`/`daily:*` settlement keys in saves are inert.
- **Goals say what they earn.** Each goal carries `UnlocksLabel` ("Regional" …
  "Established airline"); the HUD shows "TOWARD DOMESTIC". Reliability goals read "Hold
  reliability at N%+". The outstation goal counts outstations (0/1). The margin goal shows
  services counted and the running margin. A completed pin releases itself.
- **Finale = every International-stage goal** (the widebody included), so the track and the
  ending cannot disagree.
- **One fleet count:** the whole airline (Adelaide + outstations) everywhere.
- **One "international":** Tasman and beyond. Cairns and Darwin are National-band Australian
  cities. International routes need the International tier from Adelaide, at outstations
  and in the contract market alike.
- **Contracts:** `AbandonContract` drops the active contract for its advertised reliability
  cost. Accepting needs the eligible type based at Adelaide. Featured authored contracts
  only feature types the airline flies or can buy at its tier.
- **Registrations** of sold aircraft are never reissued.
- **Outstations** refuse a type with no route it can fly from that base; outstation checks
  are priced at the capability matching the type.
- **Career news:** tier-ups, goal completions and the finale are queued once each by
  `AirlineOperations` and shown by the HUD. The queue is seeded silently on load.
- **Achievements** are keepsakes that no longer duplicate goals or tiers and count the whole
  fleet, so "fleet at capacity" is reachable.

## Persistence and migration

No schema change; saves stay v13. Migration only: pre-v13 saves seed the manual-service count
from completed rotations (every earlier service was manual), so veterans keep repeat-schedule
access. Repeat-schedule validation now only accepts player registrations.

## Verification

`CareerLogicFixTests` pins each defect. The headless Domain/Simulation suite passes; Unity
EditMode, a Mac build and a rendered playtest are still required on Bailey's Mac.
