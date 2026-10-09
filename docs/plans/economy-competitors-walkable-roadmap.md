# Economy v2, competitor airlines and a walkable airport — roadmap

**Status:** approved by Bailey 9 Oct 2026 (assumptions 4–6 confirmed). Planning only; no game code changes. Changing the approved plan needs his approval (`AGENTS.md`).
**Date:** 9 October 2026. **Supersedes the open question in** `economy_realism_plan.md` §4 (Bailey chose **A, real-world scale**, 9 Oct).
**Bailey's goal:** make the game more immersive, with more to do — a real economy, rival airlines, and eventually walking around the airport.

## 0. Decisions taken and assumptions to confirm

| # | Item | Status |
|---|---|---|
| 1 | Real-world dollar scale (economy plan option A) | **Decided by Bailey, 9 Oct** |
| 2 | Competitor airlines in the economy | **Decided by Bailey, 9 Oct** |
| 3 | Walkable airport, as a later phase | **Decided by Bailey, 9 Oct** |
| 4 | A bad stretch must not hard-lock a save: negative cash triggers the existing recovery contract and a bank loan, never game over | **Confirmed by Bailey, 9 Oct** |
| 5 | Loans and a moving fuel price are in the first economy pass | **Confirmed by Bailey, 9 Oct** |
| 6 | Difficulty stays a cost multiplier; Hard additionally gets fuel volatility | **Confirmed by Bailey, 9 Oct** |

## 1. Why this order

`PROJECT_PLAN.md` says systems are added in order, and broad content waits for a stable core loop. Each phase below
reuses the one before it:

1. **Economy v2** is the foundation. Competitors need the same cost and revenue model, and a walkable airport needs
   something worth walking to (desks, hangar, tower, the aircraft you just bought).
2. **Competitor airlines** run the same economy code, so they cost far less after phase 1.
3. **Walkable airport** is the largest and riskiest item. It needs navigable geometry and interiors that mostly do
   not exist, so it starts small and is scoped separately.

## 2. Phase 1 — Economy v2 at real-world scale

Builds on `economy_realism_plan.md` (cost build-up, standing costs, fares x load factor, fuel price, loans, recovery
path). Only the A-scale consequences are new here.

### 2.1 Re-denomination

Every dollar value in the game moves to a real-world scale. All figures below are **indicative, from memory, and must be
sourced** into `docs/data/AIRLINE_OPERATING_COSTS.md` (licence, attribution, cost, fallback) before any code, as
`AGENTS.md` requires. Treat the numbers as ranges, not facts.

| Type | Today | Indicative real-world value |
|---|---:|---:|
| Saab 340 (used) | $1,600 | ~$1.5–3M |
| ATR 42-600 | $5,200 | ~$8–20M |
| Dash 8 Q400 | see code | ~$12–30M |
| Bell 412 | see code | ~$6–7M |
| Embraer E190 (used) | $20,000 | ~$20–30M |
| A220-300 | $24,000 | ~$40–90M |
| 737-800 (used) | $24,500 | ~$25–40M |
| A320ceo (used) | $25,500 | ~$20–35M |
| A321neo | $45,000 | ~$55–130M |
| A330-900 | $66,000 | ~$90–150M |
| 787-9 / 787-10 | $74,000 / $82,000 | ~$140–170M |
| A350-900 | $85,000 | ~$150M+ |

Used-market, lease and list prices differ a lot. Decide per type which one the game uses; recommend a used price for
older types and list price for current ones.

### 2.2 Playable start

A real Saab costs millions, so a 2,800 float cannot work. Proposal: the player starts with **an owned Saab 340 plus an
operating-cash float and a bank loan facility** (the loan from the economy plan). The opening hours must still reach the
first profitable rotation, and the first aircraft must not be a purchase the player cannot afford.

### 2.3 Cost and revenue lines

Per flight: fuel (block hours x burn x fuel price), crew, maintenance reserve, landing and terminal fees, handling, en-route
charges. Per day while owned: depreciation or lease, insurance, base rent, ground staff. Revenue: seats x load factor x fare,
with load driven by route demand, time of day and reliability. Contracts keep their advertised fixed pay. See the economy
plan §3 for the model; this doc only fixes the scale.

### 2.4 Acceptance

- Payback targets per type are written down and tested (Saab, ATR, 737-800 and a widebody), as the economy plan §5 asks.
- The career bot (`CareerBot`) confirms the 100–150 hour pacing target of ADR 0120 still holds.
- Saves migrate with an explicit version; old saves get a one-time rebalance notice and keep their relative wealth.
- A bad run reaches the recovery path; it never soft-locks.
- Simulation outcomes stay independent of frame rate; the fuel-price walk is seeded.

## 3. Phase 2 — Competitor airlines

Today's AI traffic only moves aircraft; it has no money, routes or fleet. A competitor is a company simulated by the
same rules as the player.

- **Company model:** name, livery (original, no real-airline marks), fleet, routes, cash, strategy profile (budget, regional,
  premium). Deterministic, clock-injected, seeded, and run by catch-up so it advances while the game is closed.
- **Behaviour:** each simulated day they price, schedule, buy or lease aircraft, and open or close routes using the phase 1
  cost and revenue code. No special rules that the player does not also face.
- **Effect on the player:** shared demand on a route (a rival at the same fare takes load share), fare pressure, and rivals
  competing for the same stands and slots at Adelaide. The existing six-aircraft Adelaide allocation and "AI cannot consume
  capacity promised to the player" rule stay.
- **Visible to the player:** a market panel (who flies where, at what fare, with how many seats), and rival aircraft in the
  existing live map (ADR 0076).
- **Out of scope for the first cut:** mergers, buy-outs, alliances and codeshares.

Acceptance: deterministic replay with the same seed, identical results after reload and catch-up, no change to existing
Adelaide reservations, rivals able to fail without breaking saves.

## 4. Phase 3 — Walkable airport (scoped separately)

There is no avatar, character controller or first-person mode today. Existing people (passengers, ramp crew) are
presentation of simulation state only (`human-operations-plan.md`), and the cockpit mode is a seat camera.

Walking is only valuable if there is something to do on foot, so the first version is deliberately small:

1. A player avatar with a third-person or first-person camera on the apron and terminal frontage, with simple collision.
2. Interaction points that open existing screens: the aircraft door (Track), a check-in or crew desk (Plan), the hangar
   (Fleet and maintenance), the tower (Operations), and a market desk (competitors).
3. Later: terminal interiors, boarding with passengers, and walking through an aircraft cabin.

Open design questions for Bailey before scoping: first-person or third-person; whether the player must walk to open a screen
or can use menus as a shortcut; whether the walk should be free movement or points of interest only.

Acceptance and risk are written when this phase starts. The main risks are navigable geometry, performance with the
existing streaming and floating-origin systems, and keeping simulation independent of the player's position.

## 5. What does not change

Domain and simulation stay independent of Unity scenes; time comes from an injected clock; reservations are required before
use; every state-changing command applies exactly once; persisted schema changes carry a version and migration; no external
data or asset enters without a recorded licence; the procedural presentation stays as the fallback.

## 6. Suggested next steps

1. Bailey confirms assumptions 4–6 above, or changes them.
2. Source the operating-cost data into `docs/data/AIRLINE_OPERATING_COSTS.md` (no code).
3. Write the ADR and save-version plan for Economy v2, then implement the pure cost model with tests.
4. Revisit this doc before Phase 2 and Phase 3.
