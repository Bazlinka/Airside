#!/usr/bin/env bash
# Headless guard: long review delays must raise soak minutes past the delay.
# Locks the capture-game.sh fix that stopped auto-landing/boarding stills from
# being killed by the default 3-minute soak COMPLETE.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cap="$root/scripts/capture-game.sh"

expect_plan() {
  local delay="$1" timeout_in="$2" want_minutes="$3" want_timeout="$4"
  local plan
  plan="$(bash "$cap" --delay "$delay" --timeout "$timeout_in" --print-plan)"
  echo "plan delay=$delay: $plan"
  [[ "$plan" == *"delay=${delay}"* ]] || { echo "bad delay in: $plan" >&2; exit 1; }
  [[ "$plan" == *"minutes=${want_minutes}"* ]] || { echo "want minutes=$want_minutes in: $plan" >&2; exit 1; }
  [[ "$plan" == *"timeout=${want_timeout}"* ]] || { echo "want timeout=$want_timeout in: $plan" >&2; exit 1; }
  [[ "$plan" == *"soak_outlives_delay=1"* ]] || { echo "soak must outlive delay: $plan" >&2; exit 1; }
}

# Short stills keep the default 3-minute soak.
expect_plan 28 88 3 88
expect_plan 45 120 3 120

# Hangar / boarding / auto-landing / auto-takeoff must raise soak past the review delay.
expect_plan 90 180 4 300
expect_plan 320 420 8 540
expect_plan 360 450 8 540
# Auto-takeoff waits ~900s for FleetState.TakingOff (~13 min opening bank).
expect_plan 900 1020 17 1080

echo "capture-game soak window checks passed"
