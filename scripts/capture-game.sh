#!/usr/bin/env bash
# Capture a screenshot from the built game (work/builds/Airside.app, or $AIRSIDE_APP) without a
# person present.
#
#   scripts/capture-game.sh [--out PATH] [--delay SECONDS] [--follow REGISTRATION]
#                           [--minutes N] [--timeout SECONDS] [-- EXTRA GAME ARGS...]
#
# Runs a soak session (a fresh "Soak Air" career), optionally follows one aircraft, writes a PNG
# after --delay seconds and quits. Prints the PNG path on success.
# When --delay outlives the default 3-minute soak, minutes/timeout are raised automatically
# so soak COMPLETE cannot quit before the review shot.
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
out="$root/work/captures/capture-$(date +%Y%m%d-%H%M%S).png"
delay=40
follow=""
minutes=3
timeout=""
extra=()
while [ $# -gt 0 ]; do
  case "$1" in
    --out) out="$2"; shift 2 ;;
    --delay) delay="$2"; shift 2 ;;
    --follow) follow="$2"; shift 2 ;;
    --minutes) minutes="$2"; shift 2 ;;
    --timeout) timeout="$2"; shift 2 ;;
    --) shift; extra=("$@"); break ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
timeout="${timeout:-$((delay + 60))}"

if [ ! -x "$app" ]; then
  echo "No build at $app — run scripts/build-mac.sh first." >&2
  exit 1
fi

# Soak COMPLETE calls QuitGame when -airsideSoakMinutes elapses. A review delay
# longer than that (auto-landing / boarding stills at ~320–360s) never writes a PNG
# if we leave the default 3-minute soak. Keep soak alive past delay + quit buffer.
need_minutes=$(( (delay + 120 + 59) / 60 ))
raised_minutes=0
if [ "$minutes" -lt "$need_minutes" ]; then
  echo "Raising soak minutes $minutes → $need_minutes so soak outlives review delay ${delay}s" >&2
  minutes=$need_minutes
  raised_minutes=1
fi
# When soak minutes grow for a long delay, the shell watchdog must cover that window too.
if [ "$raised_minutes" -eq 1 ]; then
  need_timeout=$((minutes * 60 + 60))
  if [ "$timeout" -lt "$need_timeout" ]; then
    echo "Raising capture timeout ${timeout}s → ${need_timeout}s for ${minutes}m soak" >&2
    timeout=$need_timeout
  fi
fi

mkdir -p "$(dirname "$out")"
log="${out%.png}.log"
rm -f "$out" "$log"

# Wake the display now; keep it and the system awake until the game exits.
caffeinate -u -t 10 &
args=(-airsideSoak -airsideSoakMinutes "$minutes" -airsideReviewShot "$out" -airsideReviewDelay "$delay" -logFile "$log")
[ -n "$follow" ] && args+=(-airsideReviewAircraft "$follow")
"$app" "${args[@]}" ${extra[@]+"${extra[@]}"} >/dev/null 2>&1 &
pid=$!
caffeinate -d -i -w "$pid" &

started=$(date +%s)
while kill -0 "$pid" 2>/dev/null; do
  [ -f "$out" ] && break
  if [ $(( $(date +%s) - started )) -ge "$timeout" ]; then
    sample "$pid" 2 -file "${out%.png}.sample.txt" >/dev/null 2>&1 || true
    kill "$pid" 2>/dev/null || true
    sleep 1
    kill -9 "$pid" 2>/dev/null || true
    echo "The game did not write a capture within ${timeout}s; stopped it." >&2
    echo "  Log:    $log" >&2
    echo "  Sample: ${out%.png}.sample.txt" >&2
    echo "  Display events during the run:" >&2
    pmset -g log 2>/dev/null | grep "Display is turned" | tail -3 | sed 's/^/    /' >&2 || true
    exit 1
  fi
  sleep 2
done

# Give the player a moment to finish writing, then let it quit on its own.
for _ in 1 2 3 4 5; do [ -s "$out" ] && break; sleep 1; done
if [ ! -s "$out" ]; then
  echo "The game exited without writing a capture. Log: $log" >&2
  exit 1
fi
sleep 4
kill "$pid" 2>/dev/null || true
echo "$out"
