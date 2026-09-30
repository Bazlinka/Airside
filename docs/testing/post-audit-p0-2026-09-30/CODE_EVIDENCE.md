# P0 code evidence (no Mac player)

Date: 2026-09-30 · Plan ADR 0205 · Tip `cursor/p0-freighter-pick-lock-709e` (#492)

This maps each post-audit P0 row to **existing EditMode / headless proof**. It does
**not** replace Bailey’s packaged keep/fix/revert in `RESULTS.md`.
Cloud Linux cannot hear audio or judge look/feel.

Headless suite on tip: **1180+** (`scripts/test-domain.sh`); #492 tip CI green.
#491 helpers (`auto-landing` / `auto-takeoff` / framing fail-closed) are already on `main`.

## Automated / deterministic (code green)

| P0 concern | Evidence | Verdict from code |
|---|---|---|
| Audio zoom gain curve (overview quieter, not silent) | `AircraftAudioMixTests` (`ZoomGain` 36–45000 m) | **keep** (levels); Bailey must still *listen* |
| Freight refit / pay / boarding skip / save v19 / CARGO titles | `FreightTests`, `FreightPaintTests` | **keep** (logic); Bailey must still *see* livery + tyres |
| Stage A freighter pick on soak seed (jet preferred, T+0/T+28 stand) | `ReviewFreighterPickTests` + soak `PickBest` | **keep** (pick); Bailey must still *see* cargo shade |
| Stage C hangar pick on soak seed (non-founding preferred, StartCheck @ T+0/T+90) | `ReviewHangarPickTests` + soak `PickBest` | **keep** (pick); Bailey must still *see* tow |
| Stage C boarding pick on soak seed (regional bookable @ T+0/T+320) | `ReviewBoardingPickTests` + soak `PickBest` | **keep** (pick); Bailey must still *see* tape |
| Hangar bay capacity / tow path / mid-tow @ 90s | `HangarBaysTests`, `HangarTowTests` | **keep** (logic); Bailey must still *see* tow |
| Far zoom fog scale / max orbit | `CameraFeelTests` (`MaxOrbitDistance`, fog scale) | **keep** (math); Bailey must still *see* land cover |
| Storm ground-stop for departures | `HoldReasonTests.Storm_IsAGroundStop` | **keep** (departures held) |
| Arrival already on final lands in storm; inbound held until storm ends | `RunwayWeatherTests.Storm_LetsAnArrivalAlreadyOnFinalLandAndHoldsTheDeparture`, `Storm_HoldsAnArrivalThatHasNotReachedFinal` | **keep** (logic); Bailey must still *see* a storm final |
| Arrival clearance / holding-for-landing → landing | `ArrivalClearanceTests`, `TowerAndStandChoiceTests` | **keep** (clearance timing) |
| Phase 0 camera bookmarks | `VisualBaselineViewsTests` | **keep** (table locked) |
| Stand oil stains + soft ground edges | `StandOilStainsTests` | **keep** (logic); Bailey must still *see* stains |
| Apron patches + drainage pits | `ApronSurfaceWearTests` | **keep** (logic); Bailey must still *see* patches |
| Night-sky drawable cruise early in soak + yaw 270 sector + upper-half frustum | `SkyTrafficTests.NightSkyReviewWindow_HasDrawableCruiseTrafficEarlyInSoak`, `NightSkyReviewYaw_FacesADrawableOverflightSector`, `NightSkyReviewFraming_PutsDrawableCruiseInUpperHalfOfFrame` | **keep** (timing/aim/frustum); Bailey must still *see* cruise in the still |
| Night-sky CLI pose fail-closed (reject nose-down default) | `ReviewOverviewFramingTests` + soak abort `overview framing mismatch` + inventory pitch band | **keep** (no false-success PNG); Bailey must still *see* cruise after Mac Stage A |
| Auto-landing / auto-takeoff pick Landing / TakingOff (not parked Saab) | `ReviewAircraftFollowTests` (incl. packaged jet takeoff 1330 / landing 780/783/786) | **keep** (follow pick); Bailey must still *see* tyres |
| Review follow fail-closed (auto-* **and** freighter/hangar/boarding registration) | Soak abort `follow never started` / `follow lost before delay`; inventory requires `following=True` | **keep** (no blind overview); Bailey must still *see* the subject |
| Night-sky inventory yaw/dist corridor | Inventory pitch 3–13 + yaw 262–278 + dist 8800–13200 (matches framing tolerances) | **keep** (stamp); Bailey must still *see* cruise |
| Boarding mid-window @ 320/323s | `EngineStartSequenceTests.PackagedBoardingStill_IsMidBoardingForStarterRegional` | **keep** (timing); Bailey must still *see* tape |
| Multi-shot soak CLI schedule | `ReviewShotScheduleTests` | **keep** (parser); rebuild player required |
| Packaged remaining delays / Stage A–C ONLY / soak window / capture resume | `scripts/test-p0-remaining-delays.sh`, `test-p0-remaining-only-filter.sh`, `test-p0-stages-chain.sh`, `test-capture-game-soak-window.sh`, `test-capture-game-resume.sh`, `test-p0-mac-agent-launch.sh` | **keep** (script locks) |
| Wheel spin on ground roll / stop airborne | `PresentationLayoutTests.FlightPath_WheelsStopOnceTheAircraftIsAirborne` | **keep** (math); Bailey must still *see* tyres |

## Needs Mac eyes / ears (cannot close from code)

| Check | Why |
|---|---|
| Aircraft audible at overview + follow | Perception; ZoomGain only proves the curve |
| Touchdown chirp / reverse / rollout | Listening |
| Freighter livery at follow distance | Visual |
| Tyres on ground at rotation / flare | Visual (pick + wheel math locked; look still owed) |
| Sky traffic cruise at night (still framing) | Visual motion in the PNG |
| Far land cover / zoom-in under cursor | Visual / feel |
| Terminal doors / hangar roofs / freight sheds | Visual |
| Weather depth / storm fps feel | Visual + performance |
| Hangar tow / boarding tape in motion | Visual |
| Follow camera feel | Feel |
| Human-ops close matrix | Visual |

## Prior packaged evidence (not a substitute for tip re-check)

Committed Mac player captures that already speak to some P0 rows. Use as Notes;
still fill Verdict on the **current** tip (multi-shot + framing helpers).

| Topic | Evidence | What it supports | Gap |
|---|---|---|---|
| Weather depth (ADR 0193) | `docs/testing/weather-2026-09-30/` README + PNGs | Prior Mac acceptance of cloud bodies, fog with close aircraft clear, storm/rain; fps table (storm ~32–37) | Tip has Phase 1 ground since; re-capture weather rows on P0 script |
| Far land-cover data | `docs/testing/map-2026-09-29/landcover-far.png` | Land-cover **dataset** viz only | **Not** a gameplay far-zoom shot — does not close P0 |
| Phase 0 baseline | `docs/testing/visual-baseline-2026-09-30/README.md` | Bookmark table locked | **No PNGs / metrics.md yet** |
| Aircraft audio listen | `docs/testing/audio-2026-09-30/README.md` | Packaging/tests green | Explicitly **no** listening sign-off (0192/0196 still owed) |
| #490 automated matrix | `docs/testing/post-audit-p0-2026-09-30/*.png` + RESULTS keep rows | Overview/terminal/hangar/weather mostly **keep** | Night-sky still nose-down; follow stills were parked Saab; remaining rows unverified |

## Next action

On Mac (`~/Code/Airside`, tip #492 until merged), display awake:

```bash
git fetch origin && bash scripts/p0-checkout-mac-tip.sh
# defaults to cursor/p0-freighter-pick-lock-709e; falls back to main after merge
scripts/run-post-audit-p0-stages.sh
# or Finder: scripts/run-post-audit-p0-stages.command
# Stage A only: scripts/run-post-audit-p0-stage-a.command
```

Rebuild is required (multi-shot + follow fail-closed). Fill keep/fix/revert in
`docs/testing/post-audit-p0-2026-09-30/RESULTS.md`. Pre-fill Notes from this file;
do not invent verdicts. See `docs/testing/post-audit-p0-mac-terminal.md`.
