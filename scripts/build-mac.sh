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

# Bake the git commit into StreamingAssets before Unity copies it into the .app.
bash "$root/scripts/stamp-build-identity.sh" --refresh

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

# Burst may leave empty *.app-shaped debug bundles beside the real build. Spotlight
# indexes those as extra Airside applications even though they are not playable.
# They are explicitly marked DoNotShip and are not needed for a local playtest.
debug_bundle="$root/work/builds/Airside_BurstDebugInformation_DoNotShip"
if [ -d "$debug_bundle" ]; then
  rm -rf "$debug_bundle"
fi

# Finder Get Info shows this. Same short sha as the in-game corner label.
identity="$root/game/Airside/Assets/StreamingAssets/build-identity.txt"
version="$(git -C "$root" rev-parse --short=8 HEAD)"
if [[ -f "$identity" ]] && grep -q '^dirty=true$' "$identity"; then
  version="${version}-dirty"
fi
plist="$destination/Contents/Info.plist"
if [[ -f "$plist" ]] && [[ -x /usr/libexec/PlistBuddy ]]; then
  /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" "$plist"
  /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $version" "$plist"
fi

echo "Mac build created at $destination"
if [[ -f "$identity" ]]; then
  echo "Build identity:"
  cat "$identity"
fi
