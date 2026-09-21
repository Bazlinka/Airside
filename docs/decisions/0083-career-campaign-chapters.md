# 0083 — Career campaign chapters

Date: 21 September 2026. Bailey: make the game enjoyable with clear career goals and tiers,
"more things to plan as you play".

## Decision

The career reads as five chapters, each a short checklist derived only from what the
airline has actually done (contracts fulfilled and where, tier, fleet, reliability):

1. **Island Hopper** (+$1,500) — fulfil a Kingscote contract, 3 rotations, reliability ≥ 60.
2. **Eyre Peninsula** (+$3,000) — contracts to 2 towns, own 2 aircraft, reach Regional.
3. **Regional Network** (+$6,000) — contracts to 4 towns, own a Dash 8-400, 18 rotations, ≥ 80.
4. **Interstate** (+$12,000) — reach Domestic, own a jet, an interstate contract, 6 contracts.
5. **Going Global** (+$25,000) — reach International, own a widebody, an international
   contract, reliability ≥ 90.

Chapters complete in order. The reward is paid exactly once through the career's existing
settlement keys (`campaign:N`), so there is **no save-format change** and a reload cannot pay
twice. The simulation claims rewards inside `ProcessDue` (and after a purchase), so the
outcome is identical however the clock is stepped.

## Where the player sees it

- The objective card's caption becomes "CHAPTER 2 · EYRE PENINSULA".
- The Stats page's list starts with the current chapter and its goals, ticked as they are met.
- A toast when a chapter completes: its reward and the next chapter.

## Not in this slice

Reward sizes are first guesses to be tuned by play. Tier thresholds are unchanged.
