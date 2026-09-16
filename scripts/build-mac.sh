#!/usr/bin/env bash
set -euo pipefail

# Portable: works whether invoked as ./scripts/build-mac.sh, bash scripts/…, or zsh scripts/….
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
unity="${AIRSIDE_UNITY:-/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity}"
destination="$root/work/builds/Airside.app"
log="$root/work/mac-build.log"

if [ ! -x "$unity" ]; then
  echo "Unity not found at: $unity" >&2
  echo "Set AIRSIDE_UNITY to your Unity 6.3 LTS executable and retry." >&2
  exit 1
fi

mkdir -p "$root/work/builds"
rm -rf "$destination"
status=0
"$unity" -batchmode -nographics -quit \
  -projectPath "$root/game/Airside" \
  -buildTarget StandaloneOSX \
  -buildOSXUniversalPlayer "$destination" \
  -logFile "$log" || status=$?

# Everything Unity says goes to the log file, so a failure used to print nothing
# at all. Show why, and never claim a build that is not on disk.
if [ "$status" -ne 0 ] || [ ! -d "$destination" ]; then
  echo "Mac build failed (Unity exit $status). Last lines of $log:" >&2
  tail -40 "$log" >&2 || true
  exit 1
fi

echo "Mac build created at $destination"
