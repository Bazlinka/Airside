#!/usr/bin/env bash
# One-shot: pull main, build packaged Airside.app, open it.
# Run on a Mac with Unity 6000.3.23f1 installed (Unity Hub).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$root"

echo "== Airside packaged rebuild =="
echo "Repo: $root"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: This script only works on macOS (found $(uname -s))." >&2
  exit 1
fi

echo "== git =="
git fetch origin main
git checkout main
git pull --ff-only origin main
echo "HEAD: $(git rev-parse --short HEAD) — $(git log -1 --oneline)"

unity_default="/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity"
if [[ -n "${AIRSIDE_UNITY:-}" && -x "${AIRSIDE_UNITY}" ]]; then
  :
elif [[ -x "$unity_default" ]]; then
  export AIRSIDE_UNITY="$unity_default"
else
  # Find any 6000.3.* editor
  found="$(ls -d /Applications/Unity/Hub/Editor/6000.3.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
  if [[ -n "$found" && -x "$found" ]]; then
    export AIRSIDE_UNITY="$found"
    echo "Using Unity at $AIRSIDE_UNITY"
  else
    echo "ERROR: Unity 6.3 LTS not found." >&2
    echo "Install Unity 6000.3.23f1 via Hub, or set AIRSIDE_UNITY to the Unity binary." >&2
    exit 1
  fi
fi

echo "== build =="
bash "$root/scripts/build-mac.sh"

app="$root/work/builds/Airside.app"
if [[ ! -d "$app" ]]; then
  echo "ERROR: Build finished but $app is missing. See work/mac-build.log" >&2
  exit 1
fi

echo "== open =="
open "$app"
echo "Opened $app"
echo "For splash/wordmark: Pause → Start new airport (or delete old save)."
echo "Done. HEAD $(git rev-parse --short HEAD)"
