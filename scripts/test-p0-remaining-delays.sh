#!/usr/bin/env bash
# Headless guard: packaged remaining P0 still delays match the locked capture windows
# (jet TakingOff ~1330s, jet Landing ~780s batch, boarding mid ~320s batch, hangar mid-tow ~90s,
# night-sky ~45s) and capture-game soak always outlives each delay.
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

require_shot_spec() {
  local shot="$1" delay="$2"
  if ! grep -Eq "${shot}:${delay}(:|$)" "$remaining"; then
    echo "missing capture_shots spec ${shot}:${delay} in remaining.sh" >&2
    exit 1
  fi
  echo "ok batch delay ${delay}s → ${shot}"
}

require_delay overview-night-sky-traffic 45
require_delay follow-hangar-tow 90
require_delay follow-jet-takeoff 1330
require_shot_spec follow-jet-day 780
require_shot_spec follow-jet-close 783
require_shot_spec follow-storm-landing 786
require_shot_spec follow-boarding-tape 320
require_shot_spec follow-human-ops-close 323

grep -q 'capture follow-freighter' "$remaining" || { echo "missing follow-freighter" >&2; exit 1; }
echo "ok follow-freighter present"

grep -q 'capture_shots' "$remaining" || { echo "missing capture_shots batching" >&2; exit 1; }
echo "ok capture_shots batching present"

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

for delay in 45 90 320 780 1330; do
  plan="$(bash "$cap" --delay "$delay" --timeout $((delay + 180)) --print-plan)"
  echo "plan $plan"
  [[ "$plan" == *"delay=${delay}"* ]] || { echo "plan delay must be $delay: $plan" >&2; exit 1; }
  [[ "$plan" == *"soak_outlives_delay=1"* ]] || { echo "soak must outlive delay=$delay: $plan" >&2; exit 1; }
done

# Multi-shot plan uses max delay for soak sizing.
plan="$(bash "$cap" --shot /tmp/a.png:780:0.55:clear --shot /tmp/b.png:783:0.35 --shot /tmp/c.png:786:0.55:storm --print-plan)"
echo "multi $plan"
[[ "$plan" == *"delay=786"* ]] || { echo "multi-shot plan must use max delay 786: $plan" >&2; exit 1; }
[[ "$plan" == *"shots=3"* ]] || { echo "multi-shot plan must report shots=3: $plan" >&2; exit 1; }
[[ "$plan" == *"soak_outlives_delay=1"* ]] || { echo "multi-shot soak must outlive max delay: $plan" >&2; exit 1; }
echo "ok multi-shot soak plan"

runner="$root/scripts/run-post-audit-p0-remaining.sh"
grep -Fq 'ReviewShotSchedule' "$runner" || {
  echo "Mac runner must preflight ReviewShotSchedule in the player binary" >&2
  exit 1
}
grep -Fq 'strings' "$runner" || {
  echo "Mac runner multi-shot preflight must use strings on the player" >&2
  exit 1
}
echo "ok multi-shot player preflight"

echo "P0 remaining delay locks passed"
