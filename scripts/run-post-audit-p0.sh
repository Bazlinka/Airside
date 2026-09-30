#!/usr/bin/env bash
# One-command Mac path for post-audit P0 (ADR 0203).
# Checks out the RESULTS branch, builds the player, runs the capture matrix,
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

branch="cursor/post-audit-p0-results-709e"
fallback="cursor/post-audit-improvement-plan-709e"

git fetch origin
if git show-ref --verify --quiet "refs/remotes/origin/$branch"; then
  git checkout "$branch"
  git pull --rebase origin "$branch"
else
  echo "Results branch missing; falling back to $fallback" >&2
  git checkout "$fallback"
  git pull --rebase origin "$fallback"
  git checkout -b "$branch"
fi

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
echo "  git commit -m 'Record Mac P0 playtest RESULTS'"
echo "  git push -u origin $branch"
echo
echo "Do not mark visual/audio rows keep without eyes/ears."
echo "Manual rows still needed: audio, freighter livery, tyres, hangar tow, follow feel."
