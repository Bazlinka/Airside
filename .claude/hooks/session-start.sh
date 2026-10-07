#!/bin/bash
set -euo pipefail

# Claude Code cloud sessions only: make the headless checks runnable before the session starts, so nobody
# spends the first minutes discovering "dotnet SDK not found". Synchronous on purpose (no race with the first test run).
# Local sessions already have their own toolchain. Idempotent: safe to re-run.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

# bootstrap-dotnet.sh prints progress on stderr and, when it had to install or locate the SDK, an `export PATH=...` line on stdout.
exports="$(bash scripts/bootstrap-dotnet.sh)"
if [ -n "${CLAUDE_ENV_FILE:-}" ] && [ -n "$exports" ]; then
  printf '%s\n' "$exports" | grep '^export ' >> "$CLAUDE_ENV_FILE" || true
fi
