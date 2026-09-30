#!/usr/bin/env bash
# One-paste Mac path: Stage A → B → C for remaining P0 stills (ADR 0205).
# Prefer this when the display will stay awake for the full soak (~25+ min).
# Stage A lands night-sky + freighter first so an interrupted run still unblocks
# those rows. Does not invent keep/fix/revert — fill Verdicts by eye afterward.
#
# Usage (Darwin, awake display, ~/Code/Airside on tip or main after #491 merges):
#   scripts/run-post-audit-p0-stages.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$root"

uname_s="$(uname -s)"
if [ "$uname_s" != "Darwin" ]; then
  echo "This script must run on Darwin (got $uname_s). Use Bailey's MacBook Pro." >&2
  exit 1
fi

run() {
  local label="$1"
  shift
  echo ""
  echo "========== $label =========="
  "$@"
}

# Stage A: pull + build + short/high-priority stills
run "Stage A (night-sky + freighter)" \
  env AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter \
  bash "$root/scripts/run-post-audit-p0-remaining.sh"

# Stage B / C: reuse the player; tolerate dirty RESULTS/PNGs from Stage A
run "Stage B (takeoff / tyre rotation — ~17 min soak)" \
  env AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 \
      AIRSIDE_P0_ONLY=follow-jet-takeoff \
  bash "$root/scripts/run-post-audit-p0-remaining.sh"

run "Stage C (hangar / boarding / landings)" \
  env AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 \
      AIRSIDE_P0_ONLY=follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing \
  bash "$root/scripts/run-post-audit-p0-remaining.sh"

echo ""
echo "All stages finished. PNGs are under docs/testing/post-audit-p0-2026-09-30/"
echo "and work/captures/post-audit-p0-remaining-*. Fill Verdicts in RESULTS.md by eye/ear"
echo "(do not invent). Then listening/play rows in"
echo "docs/testing/post-audit-p0-manual-checklist.md, update GAME.md, and push."
