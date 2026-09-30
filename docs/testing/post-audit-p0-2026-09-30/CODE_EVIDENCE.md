# P0 code evidence (no Mac player)

Date: 2026-09-30 · Plan ADR 0205 · Tip `cursor/p0-auto-landing-follow-709e` (#491)

This maps each post-audit P0 row to **existing EditMode / headless proof**. It does
**not** replace Bailey’s packaged keep/fix/revert in `RESULTS.md`.
Cloud Linux cannot hear audio or judge look/feel.

Headless suite on tip: **1168+** (`scripts/test-domain.sh`); #491 tip CI green.

## Automated / deterministic (code green)

| P0 concern | Evidence | Verdict from code |
|---|---|---|
| Audio zoom gain curve (overview quieter, not silent) | `AircraftAudioMixTests` (`ZoomGain` 36–45000 m) | **keep** (levels); Bailey must still *listen* |
| Freight refit / pay / boarding skip / save v19 / CARGO titles | `FreightTests`, `FreightPaintTests` | **keep** (logic); Bailey must still *see* livery + tyres |
| Hangar bay capacity / tow path / mid-tow @ 90s | `HangarBaysTests`, `HangarTowTests` | **keep** (logic); Bailey must still *see* tow |
| Far zoom fog scale / max orbit | `CameraFeelTests` (`MaxOrbitDistance`, fog scale) | **keep** (math); Bailey must still *see* land cover |
| Storm ground-stop for departures | `HoldReasonTests.Storm_IsAGroundStop` | **keep** (departures held) |
| Arrival already on final lands in storm; inbound held until storm ends | `RunwayWeatherTests.Storm_LetsAnArrivalAlreadyOnFinalLandAndHoldsTheDeparture`, `Storm_HoldsAnArrivalThatHasNotReachedFinal` | **keep** (logic); Bailey must still *see* a storm final |
| Arrival clearance / holding-for-landing → landing | `ArrivalClearanceTests`, `TowerAndStandChoiceTests` | **keep** (clearance timing) |
| Phase 0 camera bookmarks | `VisualBaselineViewsTests` | **keep** (table locked) |
| Stand oil stains + soft ground edges | `StandOilStainsTests` | **keep** (logic); Bailey must still *see* stains |
| Apron patches + drainage pits | `ApronSurfaceWearTests` | **keep** (logic); Bailey must still *see* patches |
| Night-sky drawable cruise early in soak + yaw 270 sector | `SkyTrafficTests.NightSkyReviewWindow_HasDrawableCruiseTrafficEarlyInSoak`, `NightSkyReviewYaw_FacesADrawableOverflightSector` | **keep** (timing/aim); Bailey must still *see* cruise in the still |
| Auto-landing / auto-takeoff pick Landing / TakingOff (not parked Saab) | `ReviewAircraftFollowTests` (incl. packaged 830 / 780/783/786) | **keep** (follow pick); Bailey must still *see* tyres |
| Boarding mid-window @ 320/323s | `EngineStartSequenceTests.PackagedBoardingStill_IsMidBoardingForStarterRegional` | **keep** (timing); Bailey must still *see* tape |
| Multi-shot soak CLI schedule | `ReviewShotScheduleTests` | **keep** (parser); rebuild player required |
| Packaged remaining delays / Stage A–C ONLY / soak window | `scripts/test-p0-remaining-delays.sh`, `test-p0-remaining-only-filter.sh`, `test-p0-stages-chain.sh`, `test-capture-game-soak-window.sh`, `test-p0-mac-agent-launch.sh` | **keep** (script locks) |
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

On Mac (`~/Code/Airside`, tip #491 until merged), display awake:

```bash
git fetch origin && git checkout cursor/p0-auto-landing-follow-709e
git pull --ff-only origin cursor/p0-auto-landing-follow-709e
scripts/run-post-audit-p0-stages.sh
# or Finder: scripts/run-post-audit-p0-stages.command
```

Rebuild is required (multi-shot). Fill keep/fix/revert in
`docs/testing/post-audit-p0-2026-09-30/RESULTS.md`. Pre-fill Notes from this file;
do not invent verdicts. See `docs/testing/post-audit-p0-mac-terminal.md`.
