#!/bin/zsh
set -euo pipefail

root="${0:A:h:h}"
unity="/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity"
destination="$root/work/builds/Airside.app"
log="$root/work/mac-build.log"

mkdir -p "$root/work/builds"
"$unity" -batchmode -nographics -quit \
  -projectPath "$root/game/Airside" \
  -buildTarget StandaloneOSX \
  -buildOSXUniversalPlayer "$destination" \
  -logFile "$log"

echo "Mac build created at $destination"
