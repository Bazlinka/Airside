# 0079 — Career page is the airline profile, rank first

Date: 21 September 2026. Bailey: ranking, and more about "my profile."

## Decision

The Career workspace leads with a **profile**, not a flat stat dump.

1. **Rank is the first line** — `#N of M at Adelaide` (or tied), from the same completed-rotation standings as ADR 0070. Not passenger share, not a hidden score.
2. **Identity** — tier, reliability, rotations flown.
3. **Fleet** — count and the type names actually owned (Saab 340B, and later ATR / Dash 8 / jets).
4. **Next aircraft** — the same hangar shortfall as the objective card (`$funds of $price · rotations`), or "ready to buy" / fleet full.
5. **Adelaide ranking table** stays under the profile (leaders + the player, pass-target line). Caption is "ADELAIDE RANKING". Livery swatches stay.

## Reason

Rank and career facts were already computed, but the page read as overview + livery + a table that often sat below the fold. Bailey wanted the page to feel like *their* airline.

## Affected systems

Presentation only (`StatsWorkspaceModel` / painter). No save change. No new competitive metric.

## Migration impact

None.

## Guardrails

- Do not invent market share, passenger counts, or an AI rating.
- Ranking stays completed rotations in this live Adelaide save.
- Do not move livery or rename off this page.
