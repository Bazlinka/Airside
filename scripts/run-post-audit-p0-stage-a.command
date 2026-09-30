#!/bin/bash
# Double-click in Finder — Stage A only (night-sky + freighter, minutes).
# Prefer the full A→B→C wrapper when you can leave the Mac awake ~45+ min:
#   scripts/run-post-audit-p0-stages.command
# Keep the display awake. Does not invent RESULTS verdicts.
set -euo pipefail
cd "${HOME}/Code/Airside"
# shellcheck source=p0-checkout-mac-tip.sh
bash scripts/p0-checkout-mac-tip.sh
export AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter
exec bash scripts/run-post-audit-p0-remaining.sh
