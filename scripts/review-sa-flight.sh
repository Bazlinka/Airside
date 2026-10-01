#!/usr/bin/env bash
# Real packaged SF34 round trip in a fresh soak airline. Rate >1 is QA, not FPS evidence.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
code="${1:-KGC}"
rate="${2:-20}"
out="${3:-$root/work/sa-flight-$code-$(date +%Y%m%d-%H%M%S)}"
timeout="${AIRSIDE_JOURNEY_TIMEOUT:-1800}"
perf="${AIRSIDE_JOURNEY_PERF_SECONDS:-0}"
extra=()
if [ "$perf" != 0 ]; then extra=(-airsideReviewJourneyPerfSeconds "$perf"); fi
app="${AIRSIDE_JOURNEY_EXE:-}"
if [ -z "$app" ]; then app="${AIRSIDE_APP:-$root/work/builds/Airside.app}/Contents/MacOS/Airside"; fi
if [ ! -x "$app" ]; then echo "Build the Mac player first." >&2; exit 1; fi
mkdir -p "$out"
out="$(cd "$out" && pwd)"
log="$out/player.log"
caffeinate -u -t 10 &
"$app" -airsideSoak -airsideSoakMinutes 30 -airsideReviewCockpit \
  -airsideReviewJourney "$code" -airsideReviewJourneyRate "$rate" \
  -airsideReviewJourneyOut "$out" -airsideReviewTime 12:00 \
  -airsideReviewWeather clear -logFile "$log" ${extra[@]+"${extra[@]}"} >/dev/null 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null || true' EXIT
caffeinate -d -i -w "$pid" &
started=$(date +%s)
last_sample=0
while kill -0 "$pid" 2>/dev/null; do
  review_now=$(date +%s)
  if [ -f "$log" ]; then
    log_at=$(stat -f %m "$log")
    if [ $((review_now - log_at)) -ge 20 ] && [ $((review_now - last_sample)) -ge 60 ]; then
      last_sample=$review_now
      sample "$pid" 2 -file "$out/stall-$review_now.sample.txt" >/dev/null 2>&1 || true
      echo "Player log stalled; thread sample saved in $out" >&2
    fi
  fi
  if [ $((review_now - started)) -ge "$timeout" ]; then
    sample "$pid" 2 -file "$out/timeout.sample.txt" >/dev/null 2>&1 || true
    echo "Journey timed out. Evidence: $out" >&2
    exit 1
  fi
  sleep 2
done
player_status=0
wait "$pid" || player_status=$?
echo "Player exit status: $player_status" >&2
trap - EXIT
if ! rg -q '\[Airside journey\] COMPLETE round trip; origin 0,0 airport True' "$log"; then
  echo "Journey did not complete with the airport restored. Log: $log" >&2
  exit 1
fi
if rg -q 'Exception:|review aborted|Error:|ERROR' "$log"; then
  echo "Journey completed with logged errors; inspect $log" >&2
  exit 1
fi
python3 - "$out" <<'PY'
from pathlib import Path
import sys
out=Path(sys.argv[1])
required=['TaxiOut','TakingOff','Outbound-approach','Outbound-rollout','AtDestination','Inbound','Landing','TaxiIn','completed-overview']
missing=[phase for phase in required if not list(out.glob('*-'+phase+'.png'))]
if missing:
    sys.exit('Missing rendered journey phases: '+', '.join(missing))
PY
echo "Completed $code round trip at review rate $rate. Inspect frames and timing: $out"
