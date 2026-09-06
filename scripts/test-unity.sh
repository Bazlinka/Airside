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
"$unity" -batchmode -nographics \
  -projectPath "$root/game/Airside" \
  -runTests -testPlatform editmode \
  -testResults "$results" \
  -logFile "$log"

echo "Unity tests passed. Results: $results"
