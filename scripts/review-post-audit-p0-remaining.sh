#!/usr/bin/env bash
# Re-capture only the stills that still block P0 sign-off after #490:
#   - overview-night-sky-traffic (framing: 11 km / pitch 8 / yaw 270 — early-soak corridor)
#   - follow-jet-day / follow-jet-close (auto-landing, ~360s live — reach Landing)
#   - follow-jet-takeoff (auto-takeoff for tyre rotation)
#   - follow-storm-landing (auto-landing under storm — ADR 0190 still evidence)
#   - follow-freighter / follow-hangar-tow / follow-boarding-tape / follow-human-ops-close
#
# Requires a rebuilt player that includes those fixes. Does not invent RESULTS.
# Usage:
#   scripts/build-mac.sh
#   scripts/review-post-audit-p0-remaining.sh
#   AIRSIDE_P0_OUT=work/captures/post-audit-p0-remaining scripts/review-post-audit-p0-remaining.sh
#   AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/review-post-audit-p0-remaining.sh
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
stamp="$(date +%Y%m%d)"
shots="${AIRSIDE_P0_OUT:-$root/work/captures/post-audit-p0-remaining-$stamp}"
mkdir -p "$shots"

# Optional comma/space list of shot names. Empty = run every remaining capture.
only_raw="${AIRSIDE_P0_ONLY:-}"
only_raw="${only_raw//,/ }"
declare -a ONLY_SHOTS=()
if [[ -n "$only_raw" ]]; then
  # shellcheck disable=SC2206
  ONLY_SHOTS=($only_raw)
fi

want_shot() {
  local name="$1"
  if ((${#ONLY_SHOTS[@]} == 0)); then
    return 0
  fi
  local s
  for s in "${ONLY_SHOTS[@]}"; do
    [[ "$s" == "$name" ]] && return 0
  done
  return 1
}

common=(
  -airsideSoakHeartbeatSeconds 10
  -screen-fullscreen 0
  -screen-width 1600
  -screen-height 900
)

capture() {
  local name="$1"; shift
  if ! want_shot "$name"; then
    echo "-- skip $name (not in AIRSIDE_P0_ONLY)"
    FOLLOW=""
    return 0
  fi
  echo "==> $name"
  bash "$root/scripts/capture-game.sh" \
    --out "$shots/$name.png" \
    --delay "${CAPTURE_DELAY:-28}" \
    --timeout "${CAPTURE_TIMEOUT:-100}" \
    ${FOLLOW:+--follow "$FOLLOW"} \
    -- "$@" "${common[@]}"
  FOLLOW=""
}

# Order: short/high-priority first so an interrupted Mac run still lands night-sky +
# freighter (P2 gate) before the multi-minute landing/boarding waits.

# Shallow pitch + longer range so cruise corridors fill the upper frame (ADR 0195).
# Lighting is 23:30; SkyTraffic still follows soak sim time (drawable from ~T+30s).
CAPTURE_DELAY=45 CAPTURE_TIMEOUT=120 capture overview-night-sky-traffic \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewDistance 11000 -airsideOverviewPitch 8 -airsideOverviewYaw 270

# Parked freighter cargo shade + "... CARGO" title (ADR 0194). Short delay — no bank wait.
capture follow-freighter \
  -airsideReviewFreighter \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# Hangar tow mid-move (ADR 0186–0188). Tow takes minutes; 90s usually catches outbound.
CAPTURE_DELAY=90 CAPTURE_TIMEOUT=180 capture follow-hangar-tow \
  -airsideReviewHangarCheck \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# Opening AI departures publish from ~2 min (ADR 0110); allow lineup/roll (tyre rotation).
FOLLOW=auto-takeoff
CAPTURE_DELAY=360 CAPTURE_TIMEOUT=450 capture follow-jet-takeoff \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.45

# Opening inbound holds ~3 min then lands; wait for FleetState.Landing (tyre/flare).
FOLLOW=auto-landing
CAPTURE_DELAY=360 CAPTURE_TIMEOUT=450 capture follow-jet-day \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

FOLLOW=auto-landing
CAPTURE_DELAY=360 CAPTURE_TIMEOUT=450 capture follow-jet-close \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.35

FOLLOW=auto-landing
CAPTURE_DELAY=360 CAPTURE_TIMEOUT=450 capture follow-storm-landing \
  -airsideReviewWeather storm -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# Walkway tape mid-boarding (ADR 0187). Starter Saab: fuel+catering+baggage ≈ 255s, then board.
CAPTURE_DELAY=320 CAPTURE_TIMEOUT=420 capture follow-boarding-tape \
  -airsideReviewBoarding \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# Human-ops close (ADR 0174): airstair / tape scale at follow distance — same boarding window.
CAPTURE_DELAY=320 CAPTURE_TIMEOUT=420 capture follow-human-ops-close \
  -airsideReviewBoarding \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.35

echo "Remaining P0 stills written under $shots"
echo "Copy keep PNGs into docs/testing/post-audit-p0-<date>/ and update RESULTS.md verdicts."
echo "Manual listening/play rows still need a person — see docs/testing/post-audit-p0-manual-checklist.md"
