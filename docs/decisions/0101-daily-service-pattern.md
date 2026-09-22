# 0101 — Daily Service Pattern

Date: 22 September 2026. Bailey approved the career session loop after apron
density / buy→plan (ADR 0100): chapters are unlock checklists and contracts are
accept→fly-N→done, but nothing told the player what to fly *today*.

## Decision

Each current campaign chapter defines a small **local-day service pattern**:

| Chapter | Today pattern | Bonus |
|---|---|---|
| 1 Island Hopper | 2 rotations to Kingscote | $200 +1 reliability |
| 2 Eyre Peninsula | 1 rotation to a *new* regional town (any regional once two towns are already fulfilled) | $350 +1 |
| 3 Regional Network | 3 regional rotations | $500 +1 |
| 4 Interstate | 1 interstate hop, only once a jet is owned | $800 +1 |
| 5 Going Global | 1 international hop, only once a widebody is owned | $1,200 +1 |

Progress and the once-per-day bonus use existing career settlement keys
(`dailyservice:YYYY-MM-DD:N:i` and `dailyservice-bonus:YYYY-MM-DD:N`). No new
save fields. Incomplete at curfew pays nothing; the next Adelaide local date
resets the hop count.

The objective card progress line is prefaced with `TODAY · … n/m` so the
session goal sits next to the contract / hangar / chapter line already there.

Qualifying hops are recorded when a player rotation settles
(`AirlineOperations.TrySettleFlight`), after the normal flight payment.

## Affected systems

`DailyService` (new), `Campaign` (shared destination helpers),
`AirlineOperations.TrySettleFlight`, `OperationsSummary.Objective`, EditMode
tests.

## Migration impact

None. Keys join the settlement set already persisted; older saves simply have
no day-pattern progress until the next qualifying hop.

## Guardrails

- Do not invent a second XP bar or parallel progression track.
- Do not punish incomplete days in v1 — no bonus only.
- Do not require live timetables; patterns use route bands and fulfilled
  destination codes already derived from career state.
- Chapter 4 / 5 patterns stay inactive until jet / widebody ownership so the
  day goal never asks for a leg the fleet cannot file.
