#!/usr/bin/env bash
# Headless guard: packaged remaining P0 still delays match the locked capture windows
# (TakingOff ~830s, jet Landing ~780s, boarding mid ~320s, hangar mid-tow ~90s, night-sky ~45s)
# and capture-game soak always outlives each delay.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
remaining="$root/scripts/review-post-audit-p0-remaining.sh"
cap="$root/scripts/capture-game.sh"

require_delay() {
  local shot="$1" delay="$2"
  if ! awk -v shot="$shot" -v delay="$delay" '
    index($0, "CAPTURE_DELAY=" delay) && index($0, "capture " shot) { found=1; exit }
    $0 ~ ("CAPTURE_DELAY=" delay) { want=1; next }
    want && ($0 ~ /^FOLLOW=/ || $0 ~ /^CAPTURE_TIMEOUT=/ || $0 ~ /^[[:space:]]*$/) { next }
    want && index($0, "capture " shot) { found=1; exit }
    want { want=0 }
    END { exit found ? 0 : 1 }
  ' "$remaining"; then
    echo "missing CAPTURE_DELAY=${delay} for capture ${shot} in remaining.sh" >&2
    exit 1
  fi
  echo "ok delay ${delay}s → ${shot}"
}

require_delay overview-night-sky-traffic 45
require_delay follow-hangar-tow 90
require_delay follow-jet-takeoff 830
require_delay follow-jet-day 780
require_delay follow-jet-close 780
require_delay follow-storm-landing 780
require_delay follow-boarding-tape 320
require_delay follow-human-ops-close 320

grep -q 'capture follow-freighter' "$remaining" || { echo "missing follow-freighter" >&2; exit 1; }
echo "ok follow-freighter present"

# Night-sky re-run framing (ADR 0195) — prior still was nose-down.
grep -q 'airsideOverviewDistance 11000' "$remaining" || { echo "missing night-sky distance 11000" >&2; exit 1; }
grep -q 'airsideOverviewPitch 8' "$remaining" || { echo "missing night-sky pitch 8" >&2; exit 1; }
grep -q 'airsideOverviewYaw 270' "$remaining" || { echo "missing night-sky yaw 270" >&2; exit 1; }
echo "ok night-sky framing 11km / pitch 8 / yaw 270"

# Stage A cold-boot timeouts after a fresh Mac build.
grep -Eq 'CAPTURE_TIMEOUT=300 capture overview-night-sky-traffic' "$remaining" || {
  echo "night-sky CAPTURE_TIMEOUT must be 300 after fresh build" >&2
  exit 1
}
grep -Eq 'CAPTURE_TIMEOUT=240 capture follow-freighter' "$remaining" || {
  echo "freighter CAPTURE_TIMEOUT must be 240 after fresh build" >&2
  exit 1
}
echo "ok Stage A cold-boot timeouts"

for delay in 45 90 320 780 830; do
  plan="$(bash "$cap" --delay "$delay" --timeout $((delay + 180)) --print-plan)"
  echo "plan $plan"
  [[ "$plan" == *"soak_outlives_delay=1"* ]] || { echo "soak must outlive delay=$delay: $plan" >&2; exit 1; }
done

echo "P0 remaining delay locks passed"
