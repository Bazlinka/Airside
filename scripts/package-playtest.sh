#!/usr/bin/env bash
set -euo pipefail

# Zip the exact packaged Mac build for an external playtest (PROJECT_PLAN step 4).
# Builds first, refuses a dirty tree so the zip always matches a commit, and puts
# the tester note beside the app. Output: work/playtest/Airside-<commit>.zip

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$root"

if [ -n "$(git status --porcelain -- game scripts)" ]; then
  echo "Working tree has uncommitted changes under game/ or scripts/; commit first so the build matches a commit." >&2
  exit 1
fi

commit="$(git rev-parse --short HEAD)"
"$root/scripts/build-mac.sh"
# Unity rewrites this on build; it is not part of the playtest.
git checkout -- game/Airside/ProjectSettings/ProjectSettings.asset 2>/dev/null || true

out="$root/work/playtest/Airside-$commit"
rm -rf "$out" "$out.zip"
mkdir -p "$out"
ditto "$root/work/builds/Airside.app" "$out/Airside.app"
cp "$root/docs/testing/PLAYTEST_TESTER_NOTE.md" "$out/READ ME FIRST.md"
(cd "$root/work/playtest" && ditto -c -k --keepParent "Airside-$commit" "Airside-$commit.zip")

echo "Playtest build: $out.zip"
