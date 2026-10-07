#!/usr/bin/env bash
set -euo pipefail

# CI helper: read changed file paths on stdin (one per line) and print `heavy=false` when EVERY path is documentation or tool guidance
# that no CI step reads, otherwise `heavy=true`. An empty list, or any path not on the allow-list below, is heavy: when in doubt run everything.
# `docs/data` (read by the generators) and `docs/testing` (referenced by a script test) are deliberately NOT on the list.
# Used by .github/workflows/headless.yml; tested by scripts/test-ci-changes.sh.

light='^(GAME|CHANGELOG|AGENTS|CLAUDE|README|LICENSES)\.md$|^docs/(ai|architecture|decisions|history|plans)/|^docs/README\.md$|^\.cursor/|^\.claude/|^\.github/ISSUE_TEMPLATE/|^\.github/pull_request_template\.md$'

count=0
# `|| [ -n "$path" ]` keeps a final line that has no trailing newline (read would otherwise drop it and a code change could hide).
while IFS= read -r path || [ -n "$path" ]; do
  [ -z "$path" ] && continue
  count=$((count + 1))
  if ! printf '%s\n' "$path" | grep -Eq "$light"; then
    echo "heavy=true"
    exit 0
  fi
done

if [ "$count" -eq 0 ]; then echo "heavy=true"; else echo "heavy=false"; fi
