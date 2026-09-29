#!/usr/bin/env bash
# Render each type's full turnaround layout (aircraft, crew, vehicle stops, planned routes).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
out="${1:-$root/work/turnaround-review}"
mkdir -p "$out"
"$unity" -batchmode -quit -projectPath "$root/game/Airside" \
  -executeMethod TurnaroundLayoutReview.Render -turnaroundReviewOutput "$out" \
  -logFile "$out/review.log"
echo "Turnaround review: $out"
