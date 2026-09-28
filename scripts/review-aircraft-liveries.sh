#!/usr/bin/env bash
# Render every original paint colourway with the real Unity aircraft builder.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
out="${1:-$root/work/aircraft-liveries-review}"
mkdir -p "$out"
for scheme in coastline emu southern-cross; do
  case "$scheme" in
    coastline) colour='#0F8B8D'; airline='Coastline Regional' ;;
    emu) colour='#B8742A'; airline='Emu Air' ;;
    southern-cross) colour='#1F3A93'; airline='Southern Cross Link' ;;
  esac
  "$unity" -batchmode -quit -projectPath "$root/game/Airside" \
    -executeMethod AircraftAppearanceReview.Render \
    -aircraftReviewOutput "$out/$scheme" -aircraftReviewViews front \
    -aircraftReviewColour "$colour" -aircraftReviewAirline "$airline" \
    -logFile "$out/$scheme.log"
done
