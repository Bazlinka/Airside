#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
shots="$root/work/captures/weather-coverage"
mkdir -p "$shots"
# Existing fresh-soak capture path: this only moves the review camera, not the sim.
for kind in cloudy fog storm; do
  bash "$root/scripts/capture-game.sh" --out "$shots/remote-$kind.png" --delay 20 --timeout 90 -- \
    -airsideOverviewCenterX 14000 -airsideOverviewCenterZ 8000 \
    -airsideOverviewYaw 100 -airsideOverviewPitch 12 -airsideOverviewDistance 3200 \
    -airsideReviewWeather "$kind" -airsideReviewTime 12:00 \
    -screen-fullscreen 0 -screen-width 1440 -screen-height 900
done
bash "$root/scripts/capture-game.sh" --out "$shots/remote-overcast-below.png" --delay 20 --timeout 90 -- \
  -airsideOverviewCenterX 14000 -airsideOverviewCenterZ 8000 \
  -airsideOverviewYaw 100 -airsideOverviewPitch 12 -airsideOverviewDistance 2200 \
  -airsideReviewWeather overcast -airsideReviewTime 12:00 \
  -screen-fullscreen 0 -screen-width 1440 -screen-height 900
bash "$root/scripts/capture-game.sh" --out "$shots/remote-overcast-above.png" --delay 20 --timeout 90 -- \
  -airsideOverviewCenterX 14000 -airsideOverviewCenterZ 8000 \
  -airsideOverviewYaw 100 -airsideOverviewPitch 45 -airsideOverviewDistance 3200 \
  -airsideReviewWeather overcast -airsideReviewTime 12:00 \
  -screen-fullscreen 0 -screen-width 1440 -screen-height 900
bash "$root/scripts/capture-game.sh" --out "$shots/airport-overcast-above.png" --delay 20 --timeout 90 -- \
  -airsideOverviewPitch 45 -airsideOverviewDistance 3200 \
  -airsideReviewWeather overcast -airsideReviewTime 12:00 \
  -screen-fullscreen 0 -screen-width 1440 -screen-height 900
if rg -n 'Shader error|NullReferenceException|InvalidOperationException|IndexOutOfRangeException|\[Airside soak\] STALL' "$shots"/*.log; then
  echo "Coverage review found an error/stall; inspect the logs." >&2
  exit 1
fi
echo "Weather coverage captures: $shots"
