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
"$unity" -batchmode -nographics \
  -projectPath "$root/game/Airside" \
  -runTests -testPlatform editmode \
  -testResults "$results" \
  -logFile "$log"

if [ ! -s "$results" ]; then
  echo "Unity produced no test results (compile error or project already open?). See $log" >&2
  exit 1
fi

summary="$(grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$results" | head -1 || true)"
echo "Unity tests passed. ${summary:+$summary. }Results: $results"
