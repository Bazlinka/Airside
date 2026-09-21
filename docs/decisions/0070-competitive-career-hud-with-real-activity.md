# 0070 — Competitive career HUD using real Adelaide activity

Date: 2026-09-21

## Decision

Give the Career workspace a competitive Adelaide activity table derived from completed rotations
already recorded by every live airline in the save. Show the player's rank in the always-visible
Career overview; on layouts with room, show up to five leaders while always retaining the player
row and name the next carrier plus the rotations required to pass it.

At the same time, reduce the top navigation's selected state from a large filled tab to a precise
blue underline, and stop sharing the objective card with workspaces below 680 points of usable
workspace width. At 1024×640 the Career page now takes the full width instead of collapsing into
overlapping stacked sections.

## Reason

Airside needs competitive pressure, but inventing passenger share, opponent reputation or hidden
AI bonuses would make the HUD look game-like while lying about the simulation. Completed rotations
are already deterministic, saved for player and AI aircraft, and central to career progression.
They provide a fair first comparison whose movement the player can understand and influence.

The packaged HUD already contained the right navigation and career information. Its selected tabs
were visually louder than warnings and the compact Career layout overlapped at 1024×640. Improving
hierarchy and responsiveness creates more value than adding another permanent dashboard panel.

## Consequences

- Competition is presentation-only; no save migration or simulation balance changes.
- The table is explicitly labelled **Adelaide activity**, not market share or passenger volume.
- Tied airlines share a rank. A fresh all-zero field tells the player the first rotation takes the
  outright lead rather than arbitrarily ranking alphabetically.
- Narrow layouts hide the full table but keep the player's Adelaide rank in the Overview list.
- A future passenger/revenue market-share system requires actual simulated data and a separate
  decision; it must not silently reinterpret this activity table.
- The offline HUD renderer now selects an installed Linux or macOS font, so the same draw-list QA
  workflow runs on both environments.

## Verification

- `scripts/test-domain.sh`: 550/550.
- Unity EditMode: 784/786; the two failures are unchanged pre-existing save-migration and
  dual-runway reservation tests documented in the current handoff.
- Shared draw lists re-rendered at 1440×900 and 1024×640. Career standings are clean at the wide
  size; the compact page no longer overlaps and retains the rank summary.
- Fresh packaged Mac build completed.
