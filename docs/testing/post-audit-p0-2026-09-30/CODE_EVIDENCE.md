# P0 code evidence (no Mac player)

Date: 2026-09-30 · Plan ADR 0205 · Branch `main`

This maps each post-audit P0 row to **existing EditMode / headless proof**. It does
**not** replace Bailey’s packaged keep/fix/revert in `post-audit-p0-playtest.md`.
Cloud Linux cannot hear audio or judge look/feel.

Headless suite after Phase 0 merge: **1136/1136** (`scripts/test-domain.sh`).

## Automated / deterministic (code green)

| P0 concern | Evidence | Verdict from code |
|---|---|---|
| Audio zoom gain curve (overview quieter, not silent) | `AircraftAudioMixTests` (`ZoomGain` 36–45000 m) | **keep** (levels); Bailey must still *listen* |
| Freight refit / pay / boarding skip / save v19 | `FreightTests`, `FreightPaintTests` | **keep** (logic); Bailey must still *see* livery + tyres |
| Hangar bay capacity / tow path | `HangarBaysTests`, `HangarTowTests` | **keep** (logic); Bailey must still *see* tow |
| Far zoom fog scale / max orbit | `CameraFeelTests` (`MaxOrbitDistance`, fog scale) | **keep** (math); Bailey must still *see* land cover |
| Storm ground-stop for departures | `HoldReasonTests.Storm_IsAGroundStop` | **keep** (departures held) |
| Arrival already on final lands in storm; inbound held until storm ends | `RunwayWeatherTests.Storm_LetsAnArrivalAlreadyOnFinalLandAndHoldsTheDeparture`, `Storm_HoldsAnArrivalThatHasNotReachedFinal` | **keep** (logic); Bailey must still *see* a storm final |
| Arrival clearance / holding-for-landing → landing | `ArrivalClearanceTests`, `TowerAndStandChoiceTests` | **keep** (clearance timing) |
| Phase 0 camera bookmarks | `VisualBaselineViewsTests` | **keep** (table locked) |
| Stand oil stains + soft ground edges | `StandOilStainsTests` | **keep** (logic); Bailey must still *see* stains |
| Apron patches + drainage pits | `ApronSurfaceWearTests` | **keep** (logic); Bailey must still *see* patches |

## Needs Mac eyes / ears (cannot close from code)

| Check | Why |
|---|---|
| Aircraft audible at overview + follow | Perception; ZoomGain only proves the curve |
| Touchdown chirp / reverse / rollout | Listening |
| Freighter livery at follow distance | Visual |
| Tyres on ground at rotation / flare | Visual (gear pivot tests exist; look still owed) |
| Sky traffic cruise at night | Visual motion |
| Far land cover / zoom-in under cursor | Visual |
| Terminal doors / hangar roofs / freight sheds | Visual |
| Weather depth / storm fps feel | Visual + performance |
| Hangar tow / boarding tape in motion | Visual |
| Follow camera feel | Feel |
| Human-ops close matrix | Visual |

## Prior packaged evidence (not a substitute for tip re-check)

Committed Mac player captures that already speak to some P0 rows. Use as Notes;
still fill Verdict on the **current** tip (now includes #485/#486 ground).

| Topic | Evidence | What it supports | Gap |
|---|---|---|---|
| Weather depth (ADR 0193) | `docs/testing/weather-2026-09-30/` README + PNGs | Prior Mac acceptance of cloud bodies, fog with close aircraft clear, storm/rain; fps table (storm ~32–37) | Tip has Phase 1 ground since; re-capture weather rows on P0 script |
| Far land-cover data | `docs/testing/map-2026-09-29/landcover-far.png` | Land-cover **dataset** viz only | **Not** a gameplay far-zoom shot — does not close P0 |
| Phase 0 baseline | `docs/testing/visual-baseline-2026-09-30/README.md` | Bookmark table locked | **No PNGs / metrics.md yet** |
| Aircraft audio listen | `docs/testing/audio-2026-09-30/README.md` | Packaging/tests green | Explicitly **no** listening sign-off (0192/0196 still owed) |

## Next action

On Mac (checkout #491 tip until merged): `scripts/build-mac.sh`, then
`scripts/review-post-audit-p0-remaining.sh` (optional `AIRSIDE_P0_ONLY=`), fill
keep/fix/revert in `docs/testing/post-audit-p0-2026-09-30/RESULTS.md` remaining
inventory + manual rows. Pre-fill Notes from this file; do not invent verdicts.
