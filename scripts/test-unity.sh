#!/usr/bin/env bash
set -euo pipefail

# Portable: works whether invoked as ./scripts/test-unity.sh, bash scripts/…, or zsh scripts/….
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
results="$root/work/editmode-results.xml"
log="$root/work/editmode-test.log"

if [ ! -x "$unity" ]; then
  echo "Unity not found at: $unity" >&2
  echo "Set AIRSIDE_UNITY to your Unity 6.3 LTS executable and retry." >&2
  exit 1
fi

mkdir -p "$root/work"
# A compile error or a second editor on the project exits before tests run and writes no
# results, so a stale file from the previous run used to be mistaken for this run's.
rm -f "$results"
status=0
"$unity" -batchmode -nographics \
  -projectPath "$root/game/Airside" \
  -runTests -testPlatform editmode \
  -testResults "$results" \
  -logFile "$log" || status=$?

if [ ! -s "$results" ]; then
  echo "Unity produced no test results (compile error or project already open?). Last lines of $log:" >&2
  tail -40 "$log" >&2 || true
  exit 1
fi

summary="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$results" | head -1 || true)"
failed="$(grep -o 'failed="[0-9]*"' "$results" | head -1 | grep -o '[0-9]*' || true)"

# A failing run aborted with no output at all: the names live in the results XML and
# everything else is in the log, so neither was ever shown.
if [ "$status" -ne 0 ] || [ "${failed:-0}" != "0" ]; then
  echo "Unity tests failed. ${summary:+$summary. }Results: $results" >&2
  grep -o 'name="[^"]*" [^>]*result="Failed"' "$results" | sed 's/^/  /' | head -20 >&2 || true
  echo "Log: $log" >&2
  exit 1
fi

echo "Unity tests passed. ${summary:+$summary. }Results: $results"
