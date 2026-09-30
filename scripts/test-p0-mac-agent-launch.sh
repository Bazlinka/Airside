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
grep -Fq 'run-post-audit-p0-stages.sh' "$launch" || {
  echo "launcher prompt must run stages wrapper" >&2
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
