#!/usr/bin/env bash
# Exit 0 when porcelain is empty or only docs/testing/post-audit-p0-* paths
# are dirty (Stage A RESULTS/PNGs). Exit 1 when any other path is dirty.
# Usage: git status --porcelain | bash scripts/p0-remaining-check-dirty.sh
set -euo pipefail

dirty="$(cat)"
if [ -z "$dirty" ]; then
  exit 0
fi

bad=0
while IFS= read -r line; do
  [ -z "$line" ] && continue
  path="${line:3}"
  path="${path#\"}"
  path="${path%\"}"
  # git status --porcelain renames: "R  old -> new"
  case "$path" in
    *' -> '*) path="${path##* -> }" ;;
  esac
  case "$path" in
    docs/testing/post-audit-p0-*/*|docs/testing/post-audit-p0-*/) ;;
    *)
      echo "Unexpected dirty path before remaining P0: $path" >&2
      bad=1
      ;;
  esac
done <<< "$dirty"

if [ "$bad" -ne 0 ]; then
  echo "Save or commit non-RESULTS changes before running remaining P0." >&2
  exit 1
fi
echo "==> Allowing dirty P0 RESULTS/PNG paths from a prior stage"
exit 0
