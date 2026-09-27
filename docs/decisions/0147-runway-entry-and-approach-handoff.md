# 0147 — Onto the runway and off the approach, smoothly

Date: 28 September 2026. Author: Claude, at Bailey's request ("taxi onto runway and approach isn't
natural or smooth").

## Context

- **Lineups turned too tightly and finished off the centreline heading.** Measured per runway and
  type:
  - the baked 05/23 lineups turned on 23–31 m radii and finished 5–19° off the centreline heading;
  - the 12/30 ones turned on radii as tight as 3 m;
  - so the takeoff roll began with a heading snap.
- **The approach handoff lurched.** An arrival cleared early was blended onto the landing path
  over 3 s with a smoothstep. That is too short for the gap, with a speed kink at both ends.
- **The holding S-turn looked wrong.** An arrival holding near the runway flew a 16 m S-turn at
  about 130 ft.

## Decision

- **`Simulation/LineupGeometry`** builds each lineup: straight on from the holding point, one turn
  on the aircraft's own radius (twice nose-to-main-gear, 18–45 m), then straight along the
  centreline to the takeoff position.
  - `TryBest` picks the longest centreline straight, up to 2.2 × wheelbase so the trailing main
    gear lines up too, that still turns on at least three quarters of the radius.
  - Results:

    | Aircraft | 05 and 23 | 12 and 30 | End heading |
    |---|---|---|---|
    | Turboprop | 12–13 m | 11–13 m (was 3–9 m) | aligned |
    | 737 | 18–22 m | | within 1.5° |

  - The A350 is limited by the short space between the hold and the takeoff position, and still
    finishes 7–10° off. Jets don't use 12/30.
  - Falls back to the baked line where the geometry does not suit.
- **Approach handoff.** It lasts 6 s with a smootherstep curve, so there's no speed kink at either
  end.
- **Holding.** The drift near the hold is 5 m, slow, not a 16 m S-turn.

## Verification

A per-runway, per-type measurement of turn radius and end heading, quoted above. The type-check
is clean. Bailey asked to skip new tests this round.

Mac checks:

- lineups on 05/23 (737, A350) and on 12/30 (Saab/ATR);
- the takeoff roll starts without a heading snap;
- early-cleared arrivals blend onto the landing path smoothly.
