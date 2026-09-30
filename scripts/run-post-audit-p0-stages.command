#!/bin/bash
# Double-click in Finder on Bailey's MacBook Pro (or: open this file).
# Checks out the P0 tip (or main after merge) in ~/Code/Airside and runs Stage A→B→C.
# Keep the display awake. Does not invent RESULTS verdicts.
set -euo pipefail
cd "${HOME}/Code/Airside"
# shellcheck source=p0-checkout-mac-tip.sh
bash scripts/p0-checkout-mac-tip.sh
exec bash scripts/run-post-audit-p0-stages.sh
