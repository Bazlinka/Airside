# 0139 — Tighter progress gates

Date: 27 September 2026. Author: Claude, at Bailey's request ("more progress requirements").
Bailey chose tighter gates over adding new goals.

## Context

- **Aircraft gates.** Most aircraft flight-count gates were already met by the time their tier
  opened. For example, Domestic needs 30 flights and the 737-8 needed 20, so the gate did nothing.
- **Bases and outstations.** Base upgrades asked only for a tier and flights. Outstations asked only
  for the Domestic tier and money.
- **Reliability goals.** The "keep reliability at X%" goals were met the moment reliability touched
  X, even if it dropped straight after.

## Decision

- **Base upgrades need reliability** (`PlayerBaseSpec.RequiredReliability`):

  | Base | Reliability | Flights |
  |---|---|---|
  | Expanded regional | 75% | 4 |
  | Jet-gate | 80% | 16 (was 12) |
  | International | 88% | 50 (was 24) |

  `PlayerBase.Requirement` names the gap, and the Airline page's base roadmap uses
  `PlayerBase.GatesMet`.
- **Outstations are earned one at a time** (`AirlineOperations.OutstationGates`):

  | Outstation | Reliability | Flights |
  |---|---|---|
  | 1st | 85% | 40 |
  | 2nd | 88% | 70 |
  | 3rd | 90% | 110 |

  The Network page shows "Needs …" under the base list, and the refusal says exactly what is
  missing.
- **Aircraft flight counts now bind**, set above their tier's own flight goal:

  | Aircraft | Flights |
  |---|---|
  | E190 | 32 |
  | A220 | 36 |
  | 737-800, A320 | 38 |
  | 737-8 | 42 |
  | A321neo | 60 |
  | A330 | 90 |
  | 787-9 | 95 |
  | 787-10, A350 | 100 |

- **Reliability goals are held.** "Keep reliability at 80% for 10 flights" counts the latest
  flights in a row that ended at 80% or more (`FlightsHeldAtOrAbove`, from ADR 0138's readings).
  - A dip restarts the count.
  - Provisional asks for 4 flights, its whole flight goal. Later tiers ask for 10.
  - The goal's detail shows today's reliability.
- **Older saves.** A career restored with flights already flown is seeded with readings at its
  current reliability, up to 20. Goals it had met stay met, and a v17 save replaces the seed with
  the real readings.

## Balance

`scripts/career-balance.sh` was run with 5 seeds and a 180 h cap. The report is in
`docs/testing/career-balance-2026-09-27/gates/`.

| | Regional | Domestic | International | Established |
|---|---|---|---|---|
| Standard Competent | 9.5 h | 27.5 h | 53.2 h | 140.9 h |
| Before (lineup) | 10.8 h | 29.3 h | 61.6 h | 142.8 h |

- Re-run on the final branch state, after the Pacific band (ADR 0140) and drifting weather
  (ADR 0143): Standard Competent 9.5 / 27.5 / 55.2 / 144.6 h, Casual 11.6 / 33.4 / 78.2 / 176.7 h,
  with no stuck runs.
- Pacing stays inside ADR 0120's targets for Domestic, International and the finale. Regional was
  already just over 8 h.
- The gates don't slow a competent airline: the bot keeps reliability high and has the flights.
  They stop a careless one skipping ahead.
- The fair contracts (ADR 0138) save time that lapsed contracts used to cost.
- Challenges paid rose from 18 to 20.
- The bot now checks `CanStillFinish` before accepting, so it no longer runs into the accept guard.

## Verification

- `ProgressGateTests` covers:
  - base reliability and flight gates, with their refusal and requirement text;
  - outstations one at a time;
  - every aircraft's flight gate above its tier's flight goal;
  - held reliability, with a dip restarting the count;
  - restored careers keeping the goals they had met.
- Test fixtures that bought aircraft or opened bases below the new counts now use the new numbers.
