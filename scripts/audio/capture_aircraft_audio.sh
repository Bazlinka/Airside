#!/usr/bin/env bash
# Capture the built Unity listener: all 13 types, or one ICAO catalogue id.
# Usage: bash scripts/audio/capture_aircraft_audio.sh [all|ATR42|SF34|B38M|...] [OUT_DIR]
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
kind="${1:-all}"
out="${2:-$root/work/audio-review}"
app="${AIRSIDE_APP:-$root/work/builds/Airside.app}/Contents/MacOS/Airside"
mkdir -p "$out"
out="$(cd "$out" && pwd)"
if [ ! -x "$app" ]; then echo "Build the Mac player first: $app" >&2; exit 1; fi
rm -f "$out/complete.txt"
caffeinate -u -t 10 &
"$app" -airsideAudioReview "$kind" -airsideAudioReviewOut "$out" -logFile "$out/Player.log" &
pid=$!
caffeinate -d -i -w "$pid" &
started=$(date +%s)
while kill -0 "$pid" 2>/dev/null; do
  if [ $(( $(date +%s) - started )) -gt 480 ]; then
    kill "$pid" 2>/dev/null || true
    echo "Audio review timed out; inspect $out/Player.log" >&2
    exit 1
  fi
  sleep 2
done
if [ ! -s "$out/complete.txt" ]; then
  echo "Audio review did not finish; inspect $out/Player.log" >&2
  exit 1
fi
echo "Captured Unity mixer to $out"
