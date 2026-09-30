#!/bin/bash
# Double-click in Finder — Stage A only (night-sky + freighter, minutes).
# Prefer the full A→B→C wrapper when you can leave the Mac awake ~45+ min:
#   scripts/run-post-audit-p0-stages.command
# Keep the display awake. Does not invent RESULTS verdicts.
set -euo pipefail
cd "${HOME}/Code/Airside"
git fetch origin
git checkout cursor/p0-auto-landing-follow-709e
git pull --ff-only origin cursor/p0-auto-landing-follow-709e
export AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter
exec bash scripts/run-post-audit-p0-remaining.sh
