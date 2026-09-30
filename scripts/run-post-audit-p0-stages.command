#!/bin/bash
# Double-click in Finder on Bailey's MacBook Pro (or: open this file).
# Checks out #491 tip in ~/Code/Airside and runs Stage A→B→C.
# Keep the display awake. Does not invent RESULTS verdicts.
set -euo pipefail
cd "${HOME}/Code/Airside"
git fetch origin
git checkout cursor/p0-auto-landing-follow-709e
git pull --ff-only origin cursor/p0-auto-landing-follow-709e
exec bash scripts/run-post-audit-p0-stages.sh
