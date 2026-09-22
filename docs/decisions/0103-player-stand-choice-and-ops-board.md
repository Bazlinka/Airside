# 0103 — Player stand choice and longer Operations boards

Date: 22 September 2026. Bailey: cannot choose a stand when flights land; Arrivals
and Departures are nowhere near long enough or accurate.

## Decision

### Player stand choice

Landing player aircraft wait at the exit for a manual stand choice. The selection
card and Operations detail list every assignable stand (base allocation, type fit,
lead-in clear), with the suggested stand labelled **BEST**. If the player does
nothing for `PlayerStandAutoSeconds` (90 s from `StateStartedAt`), the tower
auto-parks on the suggested stand — same deterministic deadline under any step
size or skip-to-next-event (ADR 0056 spirit kept; the race that assigned on the
landing tick is gone).

AI operators still auto-stand immediately once the path is clear.

### Operations Arrivals / Departures

- Keep recent completed movements on the board for six hours, built from frozen
  `FleetEvent` snapshots (registration, route, stand, time) so a later state
  change cannot rewrite history.
- Departures no longer print an airborne destination ETA as "est" under the
  departure TIME. Arrivals that have touched down show touchdown under TIME and
  stand ETA under est while taxiing in.
- EVENT HISTORY footer shows five airport movement lines (not one player-only
  line) in a taller Operations-only footer.

## Why

The toast already said "choose a stand" and ADR 0045 requires the player to pick
parking, but `AwaitingStand` auto-assigned on the landing tick and the HUD button
only called `SuggestStand` — no list. The boards listed live metal only, so once
an aircraft parked or went away the page emptied; the footer painted a single
line from a six-deep list.

## Migration

None. Events are not persisted. No save schema change.

## Tests

`StandChoiceAndBoardHistoryTests`, updated `FirstFlightGuideTests` /
`FleetVisualTests`. `scripts/test-domain.sh` 691 passed.
