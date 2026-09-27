# 0132 — Moments: counting funds, flipping boards, celebration cards

Date: 27 September 2026. Author: Claude, continuing the "feel like a game" pass (ADR 0130).

## Context

The big achievements were a single line in a pop-up: reaching a tier, finishing a contract, the
finale. Money jumped to its new value. The new split-flap board was static.

## Decision

All of this is presentation only, with its logic in `Presentation/HudMoments.cs` (pure, tested).

- **`FundsTicker`:** the top bar's funds count to their new value over 0.9 s with an ease-out.
  They show green while rising and red while falling. `HudShellPainter.CapsuleValues` takes the
  shown value and the direction.
- **`FlapAnimator` / `FlapBoardState`:** when a departures-board field (time, gate or remarks)
  changes, its tiles spin through letters and settle left to right over 0.7 s. Fields seen for the
  first time don't flip.
- **`CelebrationCard` / `CelebrationPainter`:** a centred card that fades and rises in, with a
  CONTINUE button.
  - Tier cards name what the tier opens: new aircraft (short names, with the cheapest price and a
    picture), the base upgrade, and new routes for International.
  - Contract cards show the pay, the reliability gained, and the aircraft.
  - The finale card sums up the airline.
  - Cards queue up. One nobody closes leaves on its own after 20 s, because the airport keeps
    running.
- The runtime queues cards from `CareerEventKind.TierReached` and `Finale`, and from any settlement
  that finishes a contract. The pop-ups still appear too.

## Verification

- `HudMomentsTests` covers:
  - the ticker counts up, settles and counts down;
  - the flip settles left to right and doesn't flip on first sight;
  - every card fits every supported window size, with nothing clipped.
- `HouseStyleTests` now includes the card text.
- The offline render is at `docs/testing/hud-game-feel-2026-09-27/hud-celebration.png`.
- 866/866 headless tests pass; the type-check is clean.
- Mac checks:
  - the card over the live airport;
  - the funds count after a paid flight;
  - the board flipping when a status changes;
  - frame time with the flips running.
