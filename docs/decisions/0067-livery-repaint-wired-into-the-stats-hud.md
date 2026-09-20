# 0067 — Livery repaint wired into the Stats HUD

Date: 2026-09-21

## Decision

`AirlineOperations.SetLivery` (ADR 0066) had no HUD control to actually reach it — it existed
only as a tested backend command. Added a "LIVERY" section to the Stats workspace's Overview
column: six clickable swatches, the same authored palette offered at airline creation
(`AirsidePrototype.LiveryChoices`, now aliased to a new shared, UnityEngine-free
`StatsWorkspaceModel.LiveryPalette` so there is one source of truth instead of two copies of the
same six colours). The swatch matching the airline's current livery is outlined; clicking
another calls `SetLivery` and repaints immediately.

Rename was deliberately left out of this pass. It needs a text field — a materially different
and riskier piece of IMGUI than a row of buttons reusing the existing `HudDrawList`
`Fill`/`Outline`/`Hotspot` primitives every other workspace already uses — and didn't fit
alongside a same-day change with no human review in between.

## Reason

The player asked "any more?" after the previous round's ten features shipped and merged
overnight. Of the two profile features from that round, only the resale command got a discovery
path (a line on the Fleet detail panel); rename and livery were flagged in GAME.md as still
needing "an actual HUD control." Livery is the one of the two that fits entirely inside the
existing button/hotspot vocabulary, so it's the safe one to close out immediately.

## Consequences

- Presentation-only (`StatsWorkspace.cs`, `AirsidePrototype.Airline.cs`, `HudDraw.cs` for the
  new `HudAction.Livery` prefix). Outside the headless harness only in the sense that the actual
  click-to-repaint wiring in `AirsidePrototype.Airline.cs` is Unity IMGUI; the model, layout and
  painter are UnityEngine-free like every other workspace piece and are covered by
  `scripts/test-domain.sh`.
- **Verified by rendering, not just reasoning**: re-ran `scripts/hud-mockup` +
  `render-hud-mockups.py` at both 2015×1260 and a narrow 1120×840 (mirroring the 800×600 viewport
  the headless layout tests already check). Both show six swatches, correctly coloured, the
  current livery outlined, no clipping or overlap with the Next Tier block above or the footer
  below.
- `scripts/test-domain.sh`: **527/527** (1 new test — `Stats_RepaintingTheLiveryIsReflectedOnTheNextRebuild`
  — plus two existing tests extended: the fresh-career assertion now also checks
  `CurrentLiveryHex`, and the layout no-overlap test now checks the swatch row's bounds at every
  viewport).
- No save-format change: livery is already captured via the existing `AirlineRecord.LiveryHex`
  field (ADR 0045) — repainting just changes what that field holds going forward.

## Tests

`scripts/test-domain.sh`: 527/527. Visually verified via the mockup renderer at two viewport
sizes (see Consequences) — the only Presentation change this session confirmed by eye rather
than left as "reasoned, not confirmed."
