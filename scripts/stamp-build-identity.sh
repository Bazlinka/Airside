#!/usr/bin/env bash
# Write game/Airside/Assets/StreamingAssets/build-identity.txt from git.
# The file is gitignored. Editor Play and scripts/build-mac.sh both call this so a
# running game can show which commit it was built from.
#
# Default: leave the file alone when commit, branch, subject and dirty flag match,
# so opening the Unity project does not churn the asset.
# --refresh: also update stampedAt (packaged rebuild, or entering Play).
set -euo pipefail

refresh=false
if [[ "${1:-}" == "--refresh" ]]; then
  refresh=true
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
out="$root/game/Airside/Assets/StreamingAssets/build-identity.txt"
mkdir -p "$(dirname "$out")"

if ! git -C "$root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'commit=\n' > "$out"
  exit 0
fi

commit_full="$(git -C "$root" rev-parse HEAD)"
commit="$(git -C "$root" rev-parse --short=8 HEAD)"
branch="$(git -C "$root" rev-parse --abbrev-ref HEAD)"
subject="$(git -C "$root" log -1 --format=%s | tr '\r\n' '  ' | sed 's/[[:space:]]*$//')"
committed="$(git -C "$root" log -1 --format=%cI)"

# The stamp file is gitignored. Exclude it anyway so a missing ignore rule cannot
# mark every stamp dirty.
dirty_status="$(git -C "$root" status --porcelain -- . \
  ":(exclude)game/Airside/Assets/StreamingAssets/build-identity.txt" \
  ":(exclude)game/Airside/Assets/StreamingAssets/build-identity.txt.meta" || true)"
if [[ -n "$dirty_status" ]]; then
  dirty=true
else
  dirty=false
fi

# Read a key from an existing stamp. Values may contain '='. Missing key is empty.
stamp_value() {
  local key="$1"
  local file="$2"
  [[ -f "$file" ]] || return 0
  sed -n "s/^${key}=//p" "$file" | head -n 1
}

if [[ "$refresh" != true && -f "$out" ]]; then
  if [[ "$(stamp_value commit "$out")" == "$commit" \
    && "$(stamp_value commitFull "$out")" == "$commit_full" \
    && "$(stamp_value branch "$out")" == "$branch" \
    && "$(stamp_value subject "$out")" == "$subject" \
    && "$(stamp_value committedAt "$out")" == "$committed" \
    && "$(stamp_value dirty "$out")" == "$dirty" ]]; then
    exit 0
  fi
fi

stamped="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
{
  printf 'commit=%s\n' "$commit"
  printf 'commitFull=%s\n' "$commit_full"
  printf 'branch=%s\n' "$branch"
  printf 'subject=%s\n' "$subject"
  printf 'committedAt=%s\n' "$committed"
  printf 'dirty=%s\n' "$dirty"
  printf 'stampedAt=%s\n' "$stamped"
} > "$out"
