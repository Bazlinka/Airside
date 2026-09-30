#!/usr/bin/env bash
# One-command Mac path for remaining P0 stills after #490 (ADR 0205).
# Pulls the tip that has review helpers, builds the player, runs
# scripts/review-post-audit-p0-remaining.sh, and copies PNGs into the dated
# RESULTS folder for verdict fill-in. Does not invent keep/fix/revert.
#
# Usage (Darwin, awake display):
#   scripts/run-post-audit-p0-remaining.sh
#   AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/run-post-audit-p0-remaining.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$root"

uname_s="$(uname -s)"
if [ "$uname_s" != "Darwin" ]; then
  echo "This script must run on Darwin (got $uname_s). Use Bailey's MacBook Pro." >&2
  exit 1
fi

branch="$(git branch --show-current)"
case "$branch" in
  main|cursor/p0-auto-landing-follow-709e) ;;
  *)
    echo "Run from main or cursor/p0-auto-landing-follow-709e (got $branch)." >&2
    exit 1
    ;;
esac
if [ -n "$(git status --porcelain)" ]; then
  echo "Save or commit local changes before running remaining P0." >&2
  exit 1
fi

git fetch origin "$branch"
git pull --ff-only origin "$branch"

echo "==> Building Mac player (need auto-landing upgrade + review helpers)"
bash "$root/scripts/build-mac.sh"

stamp="$(date +%Y%m%d)"
capture_out="${AIRSIDE_P0_OUT:-$root/work/captures/post-audit-p0-remaining-$stamp}"
docs_dir="$root/docs/testing/post-audit-p0-2026-09-30"
mkdir -p "$capture_out" "$docs_dir"

echo "==> Remaining P0 captures (display must stay awake)"
AIRSIDE_P0_OUT="$capture_out" bash "$root/scripts/review-post-audit-p0-remaining.sh"

echo "==> Copying new PNGs into $docs_dir (overwrites prior stills of the same name)"
copied=0
for png in "$capture_out"/*.png; do
  [ -f "$png" ] || continue
  cp -f "$png" "$docs_dir/$(basename "$png")"
  copied=$((copied + 1))
done

echo
echo "Copied $copied PNG(s) into $docs_dir"
echo "Fill Verdict columns in:"
echo "  $docs_dir/RESULTS.md"
echo "  (remaining-capture inventory + manual rows — do not invent)"
echo "Then update GAME.md and push via the protected-main PR workflow."
echo
echo "Optional subset next time: AIRSIDE_P0_ONLY=shot,shot $0"
