# 0125 — Career balance simulator and the first tuning pass

Date: 27 September 2026. Author: Claude, at Bailey's request to improve the career next
("career balance sim" chosen from the options offered).

## Context

ADR 0120 set active-play checkpoints: Regional by about 8 hours, Domestic 8–35, International
35–70, the established-airline finale at 110–150. ADR 0121 made progression coherent and ADR
0123 added difficulty, but no number had been checked against play.

## Decision — measure careers with a simulated player

`Tests/EditMode/CareerBot.cs` plays the career using only commands the HUD issues and only
destinations the flight planner lists, so anything it cannot do a real player cannot either.
- **Competent** acts at once, parks on landing, checks on time, values routes by margin per
  aircraft-hour, chases open goals, and buys aircraft before bases, since aircraft earn and
  bases only unlock.
- **Casual** reacts 10–40 minutes late, lets the tower park, picks loosely among good routes,
  checks late, and keeps a larger cash reserve.

`CareerSimulation.Run` steps the live `AirlineOperations` event by event, at most a minute at a
time. It records **open hours** — time outside the 23:00–06:00 curfew, the stand-in for
active-play hours, because the game runs on the real clock. For each run it records:
- tier and goal times;
- cash, reliability, fleet and rotations;
- refusal reasons;
- stuck flags: an aircraft idle for 6 h, a stand wait over 1 h (with its `HoldReason`), or cash
  below the cheapest flight for 12 h.

`scripts/career-balance.sh` runs 3 difficulties × 2 styles × 5 seeds. It writes `report.md`,
`runs.json` and `daily.csv` and is deterministic. `CareerBalanceTests` guards the first
session, a Demanding/Casual early game, determinism and international planning.

## What the baseline found (`docs/testing/career-balance-2026-09-27/baseline/`)

1. **The finale was unreachable.** The planner listed only `MapDestinations()` (Australia),
   so no Adelaide aircraft could be sent abroad. The authored ADL–AKL/SIN/HKG contracts and the
   "three international destinations" goal depended on outstation aircraft alone.
2. **Player widebodies waited for their own stand.** Player narrowbodies took 28L, pier 28's only
   widebody line, before 28R. Qantas also kept returning to 28R as its home gate after the
   player leased the pier, and 28R blocks 28L. Both were diagnosed with the new hold reasons.
3. **Domestic → International ran long:** ~93 h on Standard/Competent. The Domestic stage
   asks for a jet, a jet gate, interstate cities and an outstation, all paid for from turboprop
   income of about $200/h per aircraft.
4. **Regional came at ~11 h**, set by the five-service goal.

## Changes

| Change | Before | After |
|---|---|---|
| Planner destinations | Australia only | Australia + international (`PlannableDestinations()`); career targets list first |
| International base narrowbody order | 27, 29, 28L, 28R | 27, 29, 28R, 28L |
| AI home-gate return | ignored the player's lease | skips gates the player leases |
| Provisional goal | Fly five services | Fly four services |
| Turboprop dispatch cost | 1.28 per km | 1.12 per km (jets unchanged) |
| Outstation bases | $40k, then $65k | $15k, then $40k |
| Jet-gate base upgrade | $8k | $6k |
| 737-8 / A321neo | $38k / $55k | $28k / $45k |
| A350-900 / 787-10 | $98k / $95k | $85k / $82k |

There is no save schema change. Saves stay at v14; goals re-evaluate on load, and prices apply
to future purchases only.

## Result (`docs/testing/career-balance-2026-09-27/tuned/`, median open hours)

| | Regional | Domestic | International | Established |
|---|---|---|---|---|
| Target (ADR 0120) | ≤ 8 | 8–35 | 35–70 | 110–150 |
| Standard, before (competent) | 11.4 | 34.9 | 92.7 | never |
| Standard, after (competent) | 9.5 | 29.6 | 78.4 | 141.1 |
| Standard, after (casual) | 11.5 | 37.3 | 87.0 | 177.1 |
| Relaxed, after (competent / casual) | 9.5 / 11.5 | 25.1 / 33.0 | 54.3 / 62.3 | 116.9 / 128.6 |
| Demanding, after (competent / casual) | 9.5 / 11.5 | 33.0 / 40.6 | 94.6 / 105.0 | 185.2 / 213.1 |

Every difficulty and style now reaches the finale with no stuck flags (before: none did).
Relaxed finishes about 17 % faster than Standard and Demanding about 31 % slower.

## Known gaps

- **International** is still ~8 h over target on Standard/Competent. Regional is ~1.5 h over,
  because each turboprop rotation plus the two-leg contract sets a floor. Both are left for
  human playtests rather than tuned further against a bot.
- **Reliability** goals complete at once in every run because the bot always pushes back on
  time. Lateness pressure needs a human.
- **Bot changes between reports.** The "before" row used the first bot policy. The bot was
  improved while tuning (margin per hour, aircraft before bases, the cheapest aircraft for
  fleet goals), so part of the gain is better play, not only better numbers.
