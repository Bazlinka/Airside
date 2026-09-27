# 0127 — One career balance, and more to do in it

Date: 27 September 2026. Author: Claude, at Bailey's request: "improve career mode — forget about
difficulties".

## Decision — difficulty is removed

The Relaxed / Standard / Demanding choice from ADR 0123 is gone. The setup wizard is three cards:
identity, livery and briefing. Every airline plays the one balance tuned in ADR 0125 (Standard).
The title screen and the Flight Manual no longer mention difficulty.

**Saves:**
- v14 still writes the field, so older builds can read newer saves.
- Loading ignores the field: a game founded on Relaxed or Demanding continues on Standard.
- An unknown value no longer fails the load.

The `CareerDifficulty` plumbing stays internal and fixed at Standard, so the economy code paths are
unchanged. The balance simulator drops the difficulty axis.

## Decision — more to do in the career

**Contract kinds and deadlines (save v15).** `RouteContractDefinition` gains a `Kind` and a
`DeadlineSeconds`. The market (`ContractMarket.KindFor` / `Terms`, deterministic per 6-hour window)
now offers four kinds:

| Kind | Share of offers | Terms | Deadline |
|---|---|---|---|
| Charter | ~16 % | one flight, 1.3× route pay | 6 h |
| Medical | ~10 % | one flight to a regional town, turboprops only, +4 reliability | 3 h |
| Freight | turboprops | 2–3 rotations, modest pay | 12 h per rotation |
| Scheduled | the rest | as before | 10 h per rotation |

- A contract that misses its deadline lapses at that exact instant (processed in `ProcessDue`, an
  event in `NextEventAt`) for its reliability cost. Flights already paid stay paid.
- Authored career contracts and the Kingscote recovery contract have no deadline.
- The Contracts page shows the kind, "within 6 h", and a "Due in" line on the active contract.
- Save v15 stores the kind, the deadline and the on-time streak. v14 contracts load as Scheduled
  with no deadline.

**Demand events** (`DemandEvents`, derived from the Adelaide date, nothing saved). On about 55 % of
days a festival, school holidays, finals, a mining boom or similar multiplies demand on a few
destinations (×1.35–1.9). The event is announced as news, shown in the forecast and paid at
settlement.

**Daily report.** At 23:00 local, the day's flights, revenue, margin, reliability change and best
route are announced. This is runtime only: a reload starts a fresh day.

**Challenges** (`CareerChallenges`), paid once through the saved award keys:

| Challenge | Reward |
|---|---|
| 10 on-time pushbacks in a row | $1,500 |
| A profitable day of 4+ flights | $1,000 |
| Two medical calls | $2,000 |
| Three charters | $2,500 |
| 30 on-time pushbacks in a row | $5,000 |

After the finale, prestige challenges become the sandbox's goals:

| Prestige challenge | Reward |
|---|---|
| Every long-haul city | $25,000 |
| 25 aircraft | $20,000 |
| 95 % reliability past 300 services | $15,000 |

Open challenges head the Stats page. **Achievements** (`CareerMilestones`) are now announced when
reached; they were silent before.

## Result (`docs/testing/career-balance-2026-09-27/variety/`, Standard, median open hours)

| | Regional | Domestic | International | Established |
|---|---|---|---|---|
| Target (ADR 0120) | ≤ 8 | 8–35 | 35–70 | 110–150 |
| Competent | 10.8 | 29.3 | 69.0 | 139.3 |
| Casual | 11.5 | 34.7 | 84.4 | 172.6 |

Across 10 runs: 1 contract lapsed, 34 challenges paid, and no dead ends. The one stand wait is a
genuine peak with every regional bay in use.

## Consequences

- One balance to tune and to reason about.
- Reliability now matters minute to minute: streak challenges, medical calls and lapsing
  contracts. The bot is still always punctual, so human playtests must judge that pressure.
- `CareerVarietyTests` pins market kinds and terms, exact deadline lapses for any step size, v15
  round trips, demand events, one-time challenge payment and the daily report.
- A player who founded on Relaxed earns a little less per flight after updating; one on Demanding,
  a little more. Their progress is kept.
