#!/usr/bin/env bash
# Checkout the P0 Mac tip in the current repo (Finder wrappers source this).
# Prefers AIRSIDE_P0_BRANCH (default cursor/p0-auto-landing-follow-709e); falls
# back to main when that remote branch is gone after merge.
set -euo pipefail
branch="${AIRSIDE_P0_BRANCH:-cursor/p0-auto-landing-follow-709e}"
git fetch origin
if git rev-parse --verify --quiet "refs/remotes/origin/${branch}" >/dev/null; then
  git checkout "$branch"
  git pull --ff-only origin "$branch"
else
  echo "origin/${branch} missing — using main" >&2
  git checkout main
  git pull --ff-only origin main
fi
