#!/usr/bin/env bash
# Re-capture only the stills that still block P0 sign-off after #490:
#   - overview-night-sky-traffic (framing: 11 km / pitch 8 / yaw 270 — early-soak corridor)
#   - follow-jet-day / follow-jet-close / follow-storm-landing (one soak, ~780–786s)
#   - follow-jet-takeoff (auto-takeoff, ~830s live — TakingOff tyre roll)
#   - follow-freighter / follow-hangar-tow
#   - follow-boarding-tape / follow-human-ops-close (one soak, ~320–323s)
#
# Landing and boarding batches share one soak so Stage C does not pay 780s / 320s
# three and two times. Requires a rebuilt player with multi-shot review support.
# Does not invent RESULTS.
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

# Multi-PNG one soak. Specs: name:delay[:zoom[:weather]]
capture_shots() {
  local timeout="$1"; shift
  local specs=()
  while [[ $# -gt 0 && "$1" != "--" ]]; do
    specs+=("$1")
    shift
  done
  if [[ "${1:-}" == "--" ]]; then
    shift
  fi

  local wanted=()
  local spec name
  for spec in "${specs[@]}"; do
    name="${spec%%:*}"
    if want_shot "$name"; then
      wanted+=("$spec")
    else
      echo "-- skip $name (not in AIRSIDE_P0_ONLY)"
    fi
  done
  if ((${#wanted[@]} == 0)); then
    FOLLOW=""
    return 0
  fi

  local shot_args=()
  local labels=()
  for spec in "${wanted[@]}"; do
    name="${spec%%:*}"
    local rest="${spec#*:}"
    labels+=("$name")
    shot_args+=(--shot "$shots/${name}.png:${rest}")
  done
  echo "==> ${labels[*]} (one soak)"
  bash "$root/scripts/capture-game.sh" \
    --timeout "$timeout" \
    ${FOLLOW:+--follow "$FOLLOW"} \
    "${shot_args[@]}" \
    -- "$@" "${common[@]}"
  FOLLOW=""
}

# Order: short/high-priority first so an interrupted Mac run still lands night-sky +
# freighter (P2 gate) before the multi-minute landing/boarding waits.

# Shallow pitch + longer range so cruise corridors fill the upper frame (ADR 0195).
# Lighting is 23:30; SkyTraffic still follows soak sim time (drawable from ~T+30s).
# Timeouts include Unity boot + cold shader compile after a fresh build.
CAPTURE_DELAY=45 CAPTURE_TIMEOUT=300 capture overview-night-sky-traffic \
  -airsideReviewView overview \
  -airsideReviewWeather clear -airsideReviewTime 23:30 \
  -airsideOverviewDistance 11000 -airsideOverviewPitch 8 -airsideOverviewYaw 270

# Parked freighter cargo shade + "... CARGO" title (ADR 0194). Short delay — no bank wait.
CAPTURE_DELAY=28 CAPTURE_TIMEOUT=240 capture follow-freighter \
  -airsideReviewFreighter \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# Hangar tow mid-move (ADR 0186–0188). Tow takes minutes; 90s usually catches outbound.
CAPTURE_DELAY=90 CAPTURE_TIMEOUT=300 capture follow-hangar-tow \
  -airsideReviewHangarCheck \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.55

# TakingOff (lineup/roll — tyre rotation). Mid first TakingOff window on soak seed
# 20260913 (~790–880s). 900s overshoots into HoldingShort — locked by EditMode.
FOLLOW=auto-takeoff
CAPTURE_DELAY=830 CAPTURE_TIMEOUT=980 capture follow-jet-takeoff \
  -airsideReviewWeather clear -airsideReviewTime 12:00 \
  -airsideReviewFollowZoom 0.45

# Jet Landing batch (flare / tyre + storm). One soak: day → close → storm.
# 360s is turboprop Landing; first jet Landing ~720–840s. Stagger ~3s for zoom/weather.
FOLLOW=auto-landing
capture_shots 1000 \
  follow-jet-day:780:0.55:clear \
  follow-jet-close:783:0.35 \
  follow-storm-landing:786:0.55:storm \
  -- -airsideReviewTime 12:00

# Boarding tape + human-ops close share one soak (same -airsideReviewBoarding window).
capture_shots 480 \
  follow-boarding-tape:320:0.55:clear \
  follow-human-ops-close:323:0.35 \
  -- -airsideReviewBoarding -airsideReviewTime 12:00

echo "Remaining P0 stills written under $shots"
echo "Copy keep PNGs into docs/testing/post-audit-p0-<date>/ and update RESULTS.md verdicts."
echo "Manual listening/play rows still need a person — see docs/testing/post-audit-p0-manual-checklist.md"
