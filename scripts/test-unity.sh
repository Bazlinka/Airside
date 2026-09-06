#!/bin/zsh
set -euo pipefail

root="${0:A:h:h}"
unity="/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity"
results="$root/work/editmode-results.xml"
log="$root/work/editmode-test.log"

mkdir -p "$root/work"
"$unity" -batchmode -nographics \
  -projectPath "$root/game/Airside" \
  -runTests -testPlatform editmode \
  -testResults "$results" \
  -logFile "$log"

echo "Unity tests passed. Results: $results"
