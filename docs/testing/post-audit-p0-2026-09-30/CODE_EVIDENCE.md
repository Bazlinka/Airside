# P0 code evidence (no Mac player)

Date: 2026-09-30 · Plan ADR 0202 · Branch `cursor/post-audit-improvement-plan-709e`

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

## Next action

Run `scripts/review-post-audit-p0.sh` on the Mac and fill
`docs/testing/post-audit-p0-playtest.md` keep/fix/revert. Pre-fill code rows
from this file where helpful; leave visual rows blank until seen.
