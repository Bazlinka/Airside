# Post-audit P0 — remaining manual checks

Date: 2026-09-30 · Plan ADR **0205** · Evidence so far:
`docs/testing/post-audit-p0-2026-09-30/RESULTS.md`

Automated stills are mostly **keep**. These rows are still **unverified** and block
P0 sign-off (and keep freight AI parked).

Canonical Mac checkout: `~/Code/Airside` on `main` (or the plan tip until #484 merges).
Keep the display awake. Prefer a **fresh** `scripts/build-mac.sh` so the night-sky
framing fix is in the player.

## 1. Re-capture night sky traffic (ADR 0195)

```bash
cd ~/Code/Airside && git pull
scripts/build-mac.sh
# night-sky + auto-landing follow stills only (preferred):
scripts/review-post-audit-p0-remaining.sh
```

Or single night-sky shot:

```bash
scripts/capture-game.sh \
  --out docs/testing/post-audit-p0-2026-09-30/overview-night-sky-traffic.png \
  --delay 35 --timeout 120 -- \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewDistance 9000 -airsideOverviewPitch 12 -airsideOverviewYaw 210
```

Judge: do overflights **cruise** (not crawl)? Is a fleet inbound drawn once on final?

## 2. Listen (ADR 0192 / 0196)

1. Play the packaged game; stand overview near the apron — engines should be audible
   (not silent at default zoom).
2. Press F / Follow on an aircraft; confirm idle / power / distance fade.
3. Follow an **arrival** through touchdown — chirp, reverse, rollout.
4. Optional mixer dump: `bash scripts/audio/capture_aircraft_audio.sh all`
5. Tune note only if needed: `AircraftAudioMix.ZoomGain`.

## 3. Freighter + tyres (ADR 0194) — unblocks P2

1. Fleet card → refit a parked aircraft to freighter → Follow: cargo shade + "... CARGO" title.
   Packaged still helper: `follow-freighter` in `scripts/review-post-audit-p0-remaining.sh`
   (`-airsideReviewFreighter`). Still needs a person to mark keep/fix.
2. Follow a **jet** (not the soak Saab at stand) from the side on takeoff rotation and on flare:
   main tyres on the runway.
   Packaged still helpers in `scripts/review-post-audit-p0-remaining.sh`:
   `-airsideReviewAircraft auto-landing` (~280s) and `auto-takeoff` (~360s). Soak is live
   wall-clock; opening AI inbound #1 reaches the circuit at ~3 min.

## 4. Storm final (ADR 0190)

Force or wait for storm; confirm an aircraft **already on final** continues to land while
departures stay held. Code already covers this (`RunwayWeatherTests`); eyes-on still owed.
Packaged still helper: `follow-storm-landing` in `scripts/review-post-audit-p0-remaining.sh`
(`auto-landing` + storm). Still needs a person to confirm it keeps landing.

## 5. Hangar tow + boarding tape (ADR 0186–0188 / 0196)

1. Send an aircraft to hangar check; watch tow in/out; engines quiet while towed.
   Packaged still helper: `follow-hangar-tow` in `scripts/review-post-audit-p0-remaining.sh`
   (`-airsideReviewHangarCheck`, ~90s delay). Still needs listening for quiet engines.
2. Board/deplane a regional bay; temporary tape only while passengers walk.
   Packaged still helper: `follow-boarding-tape` (`-airsideReviewBoarding`, ~320s delay).

## 6. Camera feel (ADR 0189 / 0191)

1. Follow climb-out and landing — no lag / swing / bob.
2. Zoom far over the city, then scroll in — zoom stays under the cursor.

## 7. Human-ops close (ADR 0174)

Airstair / bus+stairs / bridge glass at follow distance — clipping, scale, glass.
Packaged still helper: `follow-human-ops-close` (boarding + zoom 0.35). Bridge glass still
needs a jet-gate follow by hand.

## Exit

Fill Verdict columns in `docs/testing/post-audit-p0-2026-09-30/RESULTS.md` (or a new
dated RESULTS). Update `GAME.md`. Push via the protected-main PR workflow.

Do **not** start freight AI (P2) until freighter + tyre rows are keep (or explicitly deferred by Bailey).
