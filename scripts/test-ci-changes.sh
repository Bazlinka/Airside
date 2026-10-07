#!/usr/bin/env bash
set -euo pipefail

# Checks scripts/ci-changes.sh: documentation-only change lists skip the heavy CI steps, anything else (or nothing known) runs them.

root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
fail=0
expect() {
  local want="$1"; shift
  local got
  got="$(printf '%s' "$1" | bash "$root/scripts/ci-changes.sh")"
  if [ "$got" != "heavy=$want" ]; then
    echo "FAIL: expected heavy=$want, got $got for: $(printf '%s' "$1" | tr '\n' ' ')" >&2
    fail=1
  fi
}

expect false $'GAME.md\nCHANGELOG.md'
expect false $'docs/decisions/2026-10-08-x.md\ndocs/ai/WORKFLOW.md\nAGENTS.md'
expect false $'docs/history/branch-inventory-2026-10-07.md\n.cursor/rules/airside.mdc\n.claude/settings.json'
expect false $'.github/pull_request_template.md\n.github/ISSUE_TEMPLATE/task.md'
expect true  ''
expect true  $'GAME.md\ngame/Airside/Assets/Airside/Domain/Airline.cs'
expect true  $'docs/data/osm/ypad-map-2026-09-29.json'
expect true  $'docs/testing/native-editmode-fixes-2026-10-07/README.md'
expect true  $'docs/art/ART_DIRECTION_AND_ASSET_SPEC.md'
expect true  $'.github/workflows/headless.yml'
expect true  $'scripts/test-domain.sh'
expect true  $'GAME.md\n.github/workflows/headless.yml\nCHANGELOG.md'
expect true  $'README.mdx'
expect true  $'xdocs/ai/x.md'

[ "$fail" -eq 0 ] && echo "ci-changes classification OK"
exit "$fail"
