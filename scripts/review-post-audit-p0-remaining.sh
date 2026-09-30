#!/usr/bin/env bash
# Re-capture only the stills that still block P0 sign-off after #490:
#   - overview-night-sky-traffic (framing fix: 9 km / pitch 12 / yaw 210)
#   - follow-jet-day / follow-jet-close (auto-landing, ~280s live delay)
#
# Requires a rebuilt player that includes those fixes. Does not invent RESULTS.
# Usage:
#   scripts/build-mac.sh
#   scripts/review-post-audit-p0-remaining.sh
#   AIRSIDE_P0_OUT=work/captures/post-audit-p0-remaining scripts/review-post-audit-p0-remaining.sh
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
stamp="$(date +%Y%m%d)"
shots="${AIRSIDE_P0_OUT:-$root/work/captures/post-audit-p0-remaining-$stamp}"
mkdir -p "$shots"

common=(
  -airsideSoakHeartbeatSeconds 10
  -screen-fullscreen 0
  -screen-width 1600
  -screen-height 900
)

capture() {
  local name="$1"; shift
  echo "==> $name"
  bash "$root/scripts/capture-game.sh" \
    --out "$shots/$name.png" \
    --delay "${CAPTURE_DELAY:-28}" \
    --timeout "${CAPTURE_TIMEOUT:-100}" \
    ${FOLLOW:+--follow "$FOLLOW"} \
    -- "$@" "${common[@]}"
  FOLLOW=""
}

capture overview-night-sky-traffic \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewDistance 9000 -airsideOverviewPitch 12 -airsideOverviewYaw 210

FOLLOW=auto-landing
CAPTURE_DELAY=280 CAPTURE_TIMEOUT=360 capture follow-jet-day \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

FOLLOW=auto-landing
CAPTURE_DELAY=280 CAPTURE_TIMEOUT=360 capture follow-jet-close \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.35

echo "Remaining P0 stills written under $shots"
echo "Copy keep PNGs into docs/testing/post-audit-p0-<date>/ and update RESULTS.md verdicts."
echo "Manual listening/play rows still need a person — see docs/testing/post-audit-p0-manual-checklist.md"
