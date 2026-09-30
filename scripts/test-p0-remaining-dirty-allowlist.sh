#!/usr/bin/env bash
# Headless guard: Stage B tolerates dirty RESULTS/PNGs from Stage A only.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
check="$root/scripts/p0-remaining-check-dirty.sh"
runner="$root/scripts/run-post-audit-p0-remaining.sh"

grep -q 'p0-remaining-check-dirty.sh' "$runner" || {
  echo "runner must call p0-remaining-check-dirty.sh" >&2
  exit 1
}

# Empty tree OK
printf '' | bash "$check"

# Stage A RESULTS / PNG copies OK
printf '%s\n' \
  ' M docs/testing/post-audit-p0-2026-09-30/RESULTS.md' \
  '?? docs/testing/post-audit-p0-2026-09-30/follow-freighter.png' \
  | bash "$check" | grep -q 'Allowing dirty P0 RESULTS'

# Unrelated dirt still blocks
reject_out="$(printf '%s\n' ' M GAME.md' | bash "$check" 2>&1 || true)"
printf '%s\n' "$reject_out" | grep -q 'Unexpected dirty path' || {
  echo "expected Unexpected dirty path for GAME.md; got: $reject_out" >&2
  exit 1
}

# Mixed: RESULTS OK + GAME.md bad → fail
mixed_out="$(printf '%s\n' \
  ' M docs/testing/post-audit-p0-2026-09-30/RESULTS.md' \
  ' M GAME.md' | bash "$check" 2>&1 || true)"
printf '%s\n' "$mixed_out" | grep -q 'Unexpected dirty path' || {
  echo "expected mixed dirtiness to fail; got: $mixed_out" >&2
  exit 1
}

echo "P0 remaining dirty allowlist passed"
