#!/usr/bin/env bash
# Headless guard: Mac CreateAgent launcher stays pinned by machine name.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
launch="$root/scripts/launch-p0-mac-agent.sh"

test -x "$launch" || { echo "missing executable $launch" >&2; exit 1; }
grep -Fq 'https://api.cursor.com' "$launch" || {
  echo "launcher must POST api.cursor.com" >&2
  exit 1
}
grep -Fq '"type": "machine"' "$launch" || {
  echo "launcher must set env.type machine" >&2
  exit 1
}
grep -Fq "Bailey's MacBook Pro" "$launch" || {
  echo "launcher must default to Bailey's MacBook Pro" >&2
  exit 1
}
grep -Fq 'workOnCurrentBranch' "$launch" || {
  echo "launcher must set workOnCurrentBranch" >&2
  exit 1
}
grep -Fq 'run-post-audit-p0-stage-a.command' "$launch" || {
  echo "launcher prompt must default to Stage A .command" >&2
  exit 1
}
grep -Fq 'run-post-audit-p0-stages.sh' "$launch" || {
  echo "launcher must still offer full stages via AIRSIDE_P0_LAUNCH_FULL" >&2
  exit 1
}
grep -Fq 'worker_id' "$launch" || {
  echo "launcher must support env.worker_id pin" >&2
  exit 1
}
grep -Fq 'CURSOR_API_KEY' "$launch" || {
  echo "launcher must require CURSOR_API_KEY" >&2
  exit 1
}

# Missing key fails closed (exit 2).
if CURSOR_API_KEY= "$launch" >/dev/null 2>&1; then
  echo "launcher should exit non-zero without CURSOR_API_KEY" >&2
  exit 1
fi

echo "P0 Mac agent launch lock passed"
