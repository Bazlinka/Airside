#!/usr/bin/env bash
# One-command Mac path for post-audit P0 (ADR 0205).
# Updates the current main checkout, builds the player, runs the capture matrix,
# and leaves RESULTS.md ready for keep/fix/revert (does not invent verdicts).
#
# Usage (on a Mac with Unity 6.3 LTS and an awake display):
#   scripts/run-post-audit-p0.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

uname_s="$(uname -s)"
if [ "$uname_s" != "Darwin" ]; then
  echo "This script must run on Darwin (got $uname_s). Use Bailey's MacBook Pro." >&2
  exit 1
fi

if [ "$(git branch --show-current)" != "main" ]; then
  echo "Run P0 from the canonical checkout on main." >&2
  exit 1
fi
if [ -n "$(git status --porcelain)" ]; then
  echo "Save or commit local changes before running P0." >&2
  exit 1
fi
git pull --ff-only origin main

echo "==> Building Mac player"
bash "$root/scripts/build-mac.sh"

echo "==> Running P0 capture matrix (display must stay awake)"
bash "$root/scripts/review-post-audit-p0.sh"

stamp="$(date +%Y%m%d)"
docs_results="$root/docs/testing/post-audit-p0-$stamp/RESULTS.md"
echo
echo "Captures done. Fill keep/fix/revert in:"
echo "  $docs_results"
echo "Then:"
echo "  git add docs/testing/post-audit-p0-$stamp GAME.md CHANGELOG.md"
echo "  Submit the completed review results through the PR workflow in AGENTS.md."
echo
echo "Do not mark visual/audio rows keep without eyes/ears."
echo "Manual rows still needed: audio, freighter livery, tyres, hangar tow, follow feel."
