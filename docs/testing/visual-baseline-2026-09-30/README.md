# Visual overhaul Phase 0 baseline — 30 September 2026

Scope: ADR 0200 / `docs/plans/visual-overhaul-plan.md` Phase 0.

Repeatable packaged-player captures at **1600 × 900**, clear weather, six named
camera bookmarks × day (`12:00`) / dusk (`18:30`) / night (`23:30`).

## How to run

```bash
scripts/build-mac.sh
scripts/capture-visual-baseline.sh
```

Captures and Player logs land under ignored `work/captures/visual-baseline-<date>/`
with a scraped `metrics.md` (fps / p95 / draw·batch·setpass from the soak heartbeat).

Single bookmark:

```bash
scripts/capture-game.sh --out work/captures/overview-day.png --delay 30 -- \
  -airsideReviewView overview -airsideReviewTime 12:00 -airsideReviewWeather clear \
  -screen-fullscreen 0 -screen-width 1600 -screen-height 900
```

## Bookmarks

| Id | Aim |
|---|---|
| `overview` | Default field overview (BareField framing) |
| `terminal-airside` | Mid Terminal 1 airside / aerobridges |
| `terminal-kerb` | Terminal 1 landside kerb |
| `hangar-row` | Eastern hangar row |
| `suburb-edge` | Suburb edge south-east of the field |
| `coast` | West toward Gulf St Vincent / West Beach |

Table source of truth: `AirsideVisualBaselineViews` (headless-locked).

## Budget

- High tier: **≥ 60 fps** at the `overview` bookmark, 1600×900, on the dev Mac.
- Every later visual phase must record before/after numbers against this folder.

## Status

Harness / EditMode locks for the bookmark table are in this commit. Packaged PNG
evidence and the first `metrics.md` numbers are produced on a Mac with
`scripts/capture-visual-baseline.sh` and committed here when available.
