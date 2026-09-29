#!/usr/bin/env bash
# Render the ramp-crew equipment and passenger luggage lineup with the game's own rig.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
out="${1:-$root/work/people-review}"
mkdir -p "$out"
"$unity" -batchmode -quit -projectPath "$root/game/Airside" \
  -executeMethod CharacterEquipmentReview.Render -peopleReviewOutput "$out" \
  -logFile "$out/review.log"
echo "People review: $out"
