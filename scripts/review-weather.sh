#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
shots="$root/work/captures/weather-final"
mkdir -p "$shots"
for kind in fog cloudy storm rain overcast clear; do
  bash "$root/scripts/capture-game.sh" --out "$shots/$kind-day.png" --delay 25 --timeout 90 -- \
    -airsideReviewWeather "$kind" -airsideReviewTime 12:00 -airsideSoakHeartbeatSeconds 10 \
    -screen-fullscreen 0 -screen-width 1600 -screen-height 900
done
bash "$root/scripts/capture-game.sh" --out "$shots/cloudy-oblique.png" --delay 25 --timeout 90 -- \
  -airsideReviewWeather cloudy -airsideReviewTime 12:00 -airsideOverviewPitch 20 \
  -airsideOverviewDistance 3000 -airsideSoakHeartbeatSeconds 10 \
  -screen-fullscreen 0 -screen-width 1600 -screen-height 900
bash "$root/scripts/capture-game.sh" --out "$shots/overcast-low.png" --delay 25 --timeout 90 -- \
  -airsideReviewWeather overcast -airsideReviewTime 12:00 -airsideOverviewPitch 12 \
  -airsideOverviewDistance 3000 -airsideSoakHeartbeatSeconds 10 \
  -screen-fullscreen 0 -screen-width 1600 -screen-height 900
bash "$root/scripts/capture-game.sh" --out "$shots/fog-follow.png" --follow VH-PAX --delay 25 --timeout 90 -- \
  -airsideReviewWeather fog -airsideReviewTime 12:00 -airsideReviewFollowZoom 0.6 \
  -airsideSoakHeartbeatSeconds 10 -screen-fullscreen 0 -screen-width 1600 -screen-height 900
bash "$root/scripts/capture-game.sh" --out "$shots/fog-night.png" --delay 25 --timeout 90 -- \
  -airsideReviewWeather fog -airsideReviewTime 23:30 -airsideSoakHeartbeatSeconds 10 \
  -screen-fullscreen 0 -screen-width 1600 -screen-height 900
if rg -n 'Shader error|NullReferenceException|InvalidOperationException|IndexOutOfRangeException|\[Airside soak\] STALL' "$shots"/*.log; then
  echo "Weather player review found an error; inspect the logs above." >&2
  exit 1
fi
echo "Weather captures and logs: $shots"
