# 0200 — Visual overhaul Phase 0 baseline bookmarks

Date: 2026-09-30 · Owner: Cursor

**Decision:** ship six named camera bookmarks (`overview`, `terminal-airside`,
`terminal-kerb`, `hangar-row`, `suburb-edge`, `coast`) in
`AirsideVisualBaselineViews`, selected with `-airsideReviewView`, and capture them
day/dusk/night at 1600×900 via `scripts/capture-visual-baseline.sh`. Budget stays
60 fps at the overview on the High tier.

**Reason:** the visual overhaul plan (ADR 0198) requires a repeatable Phase 0
baseline before ground/land work lands, so before/after frame-time numbers are
comparable.

**Affected systems:** presentation camera framing for review shots; capture
scripts; documentation under `docs/testing/visual-baseline-2026-09-30/`.

**Migration impact:** none. Simulation, saves and interactive camera defaults are
unchanged unless a review flag is passed.
