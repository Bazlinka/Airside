#!/usr/bin/env bash
# Real-time packaged SF34 cockpit proof. Does not force flight state or touch the player's save.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/work/cockpit-game}"
mkdir -p "$out"
bash "$root/scripts/capture-game.sh" \
  --shot "$out/startup.png:220" \
  --shot "$out/local-flight.png:400" \
  --minutes 8 --timeout 540 -- \
  -airsideReviewCockpit -airsideReviewTime 12:00 -airsideReviewWeather clear
