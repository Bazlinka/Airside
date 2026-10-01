#!/usr/bin/env bash
# Capture screenshot(s) from the built game (work/builds/Airside.app, or $AIRSIDE_APP)
# without a person present.
#
#   scripts/capture-game.sh [--out PATH] [--delay SECONDS] [--follow REGISTRATION]
#                           [--shot PATH:DELAY[:ZOOM[:WEATHER]]]...
#                           [--minutes N] [--timeout SECONDS] [--print-plan]
#                           [-- EXTRA GAME ARGS...]
#
# Runs a soak session (a fresh "Soak Air" career), optionally follows one aircraft,
# writes PNG(s) after the review delay(s) and quits. Prints each PNG path on success.
# Repeat --shot (or combine --out/--delay with more --shot) for multi-PNG one-soak
# batches (Stage C landings/boarding). When --delay / max shot delay outlives the
# default 3-minute soak, minutes/timeout are raised automatically so soak COMPLETE
# cannot quit before the last review shot.
# AIRSIDE_CAPTURE_RESUME=1 keeps non-empty existing PNGs and only schedules missing
# shots (mid-batch timeout retry). --print-plan prints delay/minutes/timeout
# (and soak>delay) then exits without launching.
#
# Why this script exists: the Unity player on macOS waits for the display to show each frame.
# With the display asleep (an unattended Mac, a long build) the first frame never appears and
# the game hangs with every thread idle; two automated captures on 2026-09-29 froze exactly in
# the 09:37–09:42 window when the display was off, and seven with it on all worked. So this wakes
# the display and holds it (and the system) awake for the whole run, and if the game still hangs
# it stops it, saves a stack sample and says whether the display slept, instead of hanging too.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
app="${AIRSIDE_APP:-$root/work/builds/Airside.app}/Contents/MacOS/Airside"
out=""
delay=""
follow=""
minutes=3
timeout=""
print_plan=0
extra=()
declare -a SHOTS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --out) out="$2"; shift 2 ;;
    --delay) delay="$2"; shift 2 ;;
    --follow) follow="$2"; shift 2 ;;
    --shot) SHOTS+=("$2"); shift 2 ;;
    --minutes) minutes="$2"; shift 2 ;;
    --timeout) timeout="$2"; shift 2 ;;
    --print-plan) print_plan=1; shift ;;
    --) shift; extra=("$@"); break ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

# Legacy single-shot: --out and/or --delay (defaults preserved for callers that omit them).
if [ -n "$out" ] || [ -n "$delay" ]; then
  [ -z "$out" ] && out="$root/work/captures/capture-$(date +%Y%m%d-%H%M%S).png"
  SHOTS=("${out}:${delay:-40}" "${SHOTS[@]+"${SHOTS[@]}"}")
elif [ ${#SHOTS[@]} -eq 0 ]; then
  out="$root/work/captures/capture-$(date +%Y%m%d-%H%M%S).png"
  SHOTS=("${out}:40")
fi

resume="${AIRSIDE_CAPTURE_RESUME:-0}"
max_delay=0
resume_skipped=0
declare -a ALL_OUTS=()
declare -a KEPT_OUTS=()
declare -a REVIEW_ARGS=()
for spec in "${SHOTS[@]}"; do
  IFS=':' read -r shot_path shot_delay shot_zoom shot_weather <<<"$spec"
  if [ -z "${shot_path:-}" ] || [ -z "${shot_delay:-}" ]; then
    echo "Bad --shot spec '$spec' (need PATH:DELAY[:ZOOM[:WEATHER]])" >&2
    exit 2
  fi
  ALL_OUTS+=("$shot_path")
  if [ "$resume" = "1" ] && [ -s "$shot_path" ]; then
    resume_skipped=$((resume_skipped + 1))
    if [ "$print_plan" -eq 0 ]; then
      echo "Resume: keeping existing $shot_path" >&2
    fi
    continue
  fi
  KEPT_OUTS+=("$shot_path")
  if [ "$shot_delay" -gt "$max_delay" ]; then
    max_delay=$shot_delay
  fi
  REVIEW_ARGS+=(-airsideReviewShot "$shot_path" -airsideReviewDelay "$shot_delay")
  [ -n "${shot_zoom:-}" ] && REVIEW_ARGS+=(-airsideReviewFollowZoom "$shot_zoom")
  [ -n "${shot_weather:-}" ] && REVIEW_ARGS+=(-airsideReviewWeather "$shot_weather")
done

delay=$max_delay
timeout="${timeout:-$((delay + 60))}"

# Soak COMPLETE calls QuitGame when -airsideSoakMinutes elapses. A review delay
# longer than that never writes a PNG if we leave the default 3-minute soak.
raised_minutes=0
if [ "$delay" -gt 0 ]; then
  need_minutes=$(( (delay + 120 + 59) / 60 ))
  if [ "$minutes" -lt "$need_minutes" ]; then
    if [ "$print_plan" -eq 0 ]; then
      echo "Raising soak minutes $minutes → $need_minutes so soak outlives review delay ${delay}s" >&2
    fi
    minutes=$need_minutes
    raised_minutes=1
  fi
fi
if [ "$raised_minutes" -eq 1 ]; then
  need_timeout=$((minutes * 60 + 60))
  if [ "$timeout" -lt "$need_timeout" ]; then
    if [ "$print_plan" -eq 0 ]; then
      echo "Raising capture timeout ${timeout}s → ${need_timeout}s for ${minutes}m soak" >&2
    fi
    timeout=$need_timeout
  fi
fi

if [ "$print_plan" -eq 1 ]; then
  soak_seconds=$((minutes * 60))
  ok=0
  if [ "$delay" -eq 0 ] || [ "$soak_seconds" -gt "$delay" ]; then
    ok=1
  fi
  printf 'delay=%s minutes=%s timeout=%s soak_seconds=%s soak_outlives_delay=%s shots=%s resume_skipped=%s\n' \
    "$delay" "$minutes" "$timeout" "$soak_seconds" "$ok" "${#KEPT_OUTS[@]}" "$resume_skipped"
  exit 0
fi

if [ ${#KEPT_OUTS[@]} -eq 0 ]; then
  echo "Resume: every requested capture already exists; skipping launch." >&2
  for shot_path in "${ALL_OUTS[@]}"; do
    echo "$shot_path"
  done
  exit 0
fi

if [ ! -x "$app" ]; then
  echo "No build at $app — run scripts/build-mac.sh first." >&2
  exit 1
fi

declare -a LOGS=()
for shot_path in "${KEPT_OUTS[@]}"; do
  mkdir -p "$(dirname "$shot_path")"
  log="${shot_path%.png}.log"
  LOGS+=("$log")
  rm -f "$shot_path" "$log"
done
log="${LOGS[0]}"

# Wake the display now; keep it and the system awake until the game exits.
caffeinate -u -t 10 &
args=(-airsideSoak -airsideSoakMinutes "$minutes" -logFile "$log")
args+=("${REVIEW_ARGS[@]}")
[ -n "$follow" ] && args+=(-airsideReviewAircraft "$follow")
"$app" "${args[@]}" ${extra[@]+"${extra[@]}"} >/dev/null 2>&1 &
pid=$!
caffeinate -d -i -w "$pid" &

all_kept_ready() {
  local p
  for p in "${KEPT_OUTS[@]}"; do
    [ -s "$p" ] || return 1
  done
  return 0
}

started=$(date +%s)
while kill -0 "$pid" 2>/dev/null; do
  all_kept_ready && break
  if [ $(( $(date +%s) - started )) -ge "$timeout" ]; then
    sample "$pid" 2 -file "${KEPT_OUTS[0]%.png}.sample.txt" >/dev/null 2>&1 || true
    kill "$pid" 2>/dev/null || true
    sleep 1
    kill -9 "$pid" 2>/dev/null || true
    echo "The game did not write all captures within ${timeout}s; stopped it." >&2
    echo "  Log:    $log" >&2
    echo "  Sample: ${KEPT_OUTS[0]%.png}.sample.txt" >&2
    echo "  Display events during the run:" >&2
    pmset -g log 2>/dev/null | grep "Display is turned" | tail -3 | sed 's/^/    /' >&2 || true
    exit 1
  fi
  sleep 2
done

for _ in 1 2 3 4 5; do all_kept_ready && break; sleep 1; done
if ! all_kept_ready; then
  echo "The game exited without writing every capture. Log: $log" >&2
  exit 1
fi
sleep 4
kill "$pid" 2>/dev/null || true
# Multi-shot: Unity only accepts one -logFile (first shot). Mirror it onto sibling
# .log paths so Stage C inventory can open per-shot logs without false-failing.
if [ ${#LOGS[@]} -gt 1 ] && [ -f "${LOGS[0]}" ]; then
  for ((i = 1; i < ${#LOGS[@]}; i++)); do
    cp -f "${LOGS[0]}" "${LOGS[i]}"
  done
fi
for shot_path in "${ALL_OUTS[@]}"; do
  echo "$shot_path"
done
