#!/usr/bin/env bash
# Phase 0 visual-overhaul baseline: named camera bookmarks × day/dusk/night at 1600×900.
#
#   scripts/capture-visual-baseline.sh [--out DIR] [--delay SECONDS] [--views LIST] [--times LIST]
#
# Needs a Mac build (scripts/build-mac.sh) and an awake display — see capture-game.sh.
# Writes PNGs plus a metrics.md scraped from the soak heartbeats (fps / p95 / setpass).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stamp="$(date +%Y%m%d)"
out="$root/work/captures/visual-baseline-$stamp"
delay=30
views="overview,terminal-airside,terminal-kerb,hangar-row,suburb-edge,coast"
times="12:00,18:30,23:30"
while [ $# -gt 0 ]; do
  case "$1" in
    --out) out="$2"; shift 2 ;;
    --delay) delay="$2"; shift 2 ;;
    --views) views="$2"; shift 2 ;;
    --times) times="$2"; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

mkdir -p "$out"
metrics="$out/metrics.md"
{
  echo "# Visual baseline metrics — $(date -u +%Y-%m-%d)"
  echo
  echo "Budget: overview ≥ 60 fps at 1600×900 (High). Values are the last soak heartbeat before the shot."
  echo
  echo "| View | Time | FPS | Frame p95 (ms) | Draw / Batch / SetPass |"
  echo "|---|---|---:|---:|---|"
} > "$metrics"

IFS=',' read -r -a view_list <<< "$views"
IFS=',' read -r -a time_list <<< "$times"

time_slug() {
  case "$1" in
    12:00) echo day ;;
    18:30) echo dusk ;;
    23:30) echo night ;;
    *) echo "$1" | tr ':' '-' ;;
  esac
}

for view in "${view_list[@]}"; do
  view="$(echo "$view" | tr -d '[:space:]')"
  [ -n "$view" ] || continue
  for when in "${time_list[@]}"; do
    when="$(echo "$when" | tr -d '[:space:]')"
    slug="$(time_slug "$when")"
    shot="$out/${view}-${slug}.png"
    echo "Capturing $view @ $when → $shot"
    bash "$root/scripts/capture-game.sh" --out "$shot" --delay "$delay" --timeout $((delay + 90)) -- \
      -airsideReviewView "$view" \
      -airsideReviewTime "$when" \
      -airsideReviewWeather clear \
      -airsideSoakHeartbeatSeconds 10 \
      -screen-fullscreen 0 \
      -screen-width 1600 \
      -screen-height 900

    log="${shot%.png}.log"
    line="$(grep '\[Airside soak\]' "$log" | grep 'fps ' | tail -1 || true)"
    fps="$(echo "$line" | sed -n 's/.*fps \([0-9][0-9]*\).*/\1/p')"
    p95="$(echo "$line" | sed -n 's/.*p95 \([0-9.][0-9.]*\) ms.*/\1/p')"
    dbs="$(echo "$line" | sed -n 's/.*draw\/batch\/setpass \([-0-9][-0-9]*\/[-0-9][-0-9]*\/[-0-9][-0-9]*\).*/\1/p')"
    [ -n "$fps" ] || fps="?"
    [ -n "$p95" ] || p95="?"
    [ -n "$dbs" ] || dbs="?"
    echo "| $view | $when | $fps | $p95 | $dbs |" >> "$metrics"
  done
done

if command -v rg >/dev/null 2>&1; then
  if rg -n 'Shader error|NullReferenceException|InvalidOperationException|IndexOutOfRangeException|\[Airside soak\] STALL' "$out"/*.log; then
    echo "Visual baseline player review found an error; inspect the logs above." >&2
    exit 1
  fi
fi

echo "Visual baseline captures: $out"
echo "Metrics: $metrics"
