#!/usr/bin/env bash
# Render frames from the middle of each hands-on turnaround job (bags, fuel, catering, planeside).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
out="${1:-$root/work/service-review}"
mkdir -p "$out"
"$unity" -batchmode -quit -projectPath "$root/game/Airside" \
  -executeMethod ServiceWorkReview.Render -serviceReviewOutput "$out" \
  -logFile "$out/review.log"
echo "Service work review: $out"
