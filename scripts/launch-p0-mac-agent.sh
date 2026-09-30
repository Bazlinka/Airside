#!/usr/bin/env bash
# Launch a Cloud Agent pinned to Bailey's MacBook Pro for P0 Stage A→B→C (ADR 0205).
#
# Needs:
#   - CURSOR_API_KEY from https://cursor.com/dashboard → API Keys
#   - Team toggle "Enable Remote Control for Team" (Cloud Agents → Self-hosted)
#   - Mac awake with: cursor worker start --name "Bailey's MacBook Pro" in ~/Code/Airside
#
# Usage:
#   export CURSOR_API_KEY=…
#   scripts/launch-p0-mac-agent.sh
#
# Prefer Terminal/Finder when you are at the Mac:
#   docs/testing/post-audit-p0-mac-terminal.md
set -euo pipefail

api_base="${CURSOR_API_BASE:-https://api.cursor.com}"
machine_name=${AIRSIDE_P0_MACHINE_NAME:-"Bailey's MacBook Pro"}
branch="${AIRSIDE_P0_BRANCH:-cursor/p0-auto-landing-follow-709e}"
repo_url="${AIRSIDE_P0_REPO_URL:-https://github.com/Bazlinka/Airside}"

if [ -z "${CURSOR_API_KEY:-}" ]; then
  echo "Set CURSOR_API_KEY (Dashboard → API Keys). Or use Terminal:" >&2
  echo "  docs/testing/post-audit-p0-mac-terminal.md" >&2
  exit 2
fi

prompt_text="$(cat <<'EOF'
You are pinned to Bailey's MacBook Pro. Confirm `uname -s` → Darwin; stop if not.

Canonical checkout: ~/Code/Airside. ADR 0205 P0 remaining captures only.
Do not invent RESULTS keep/fix/revert.

1. cd ~/Code/Airside
2. git fetch origin && git checkout cursor/p0-auto-landing-follow-709e
3. git pull --ff-only origin cursor/p0-auto-landing-follow-709e
4. Keep the display awake. Run: scripts/run-post-audit-p0-stages.sh
   (caffeinate wraps A→B→C; rebuild required for multi-shot; ~45+ min)
5. After PNGs land under docs/testing/post-audit-p0-2026-09-30/, fill Verdict
   columns in RESULTS.md by eye/ear only. Leave unverified if unsure.
6. Do not start freight AI / P2–P4. Do not mark the cloud goal complete.

Return: uname, tip SHA, which PNGs were written, which verdicts you filled (if any).
EOF
)"

payload="$(CURSOR_PROMPT_TEXT="$prompt_text" \
  AIRSIDE_P0_MACHINE_NAME="$machine_name" \
  AIRSIDE_P0_BRANCH="$branch" \
  AIRSIDE_P0_REPO_URL="$repo_url" \
  python3 - <<'PY'
import json, os
print(json.dumps({
    "name": "P0 Mac Stage A→B→C",
    "prompt": {"text": os.environ["CURSOR_PROMPT_TEXT"]},
    "env": {"type": "machine", "name": os.environ["AIRSIDE_P0_MACHINE_NAME"]},
    "repos": [{
        "url": os.environ["AIRSIDE_P0_REPO_URL"],
        "startingRef": os.environ["AIRSIDE_P0_BRANCH"],
    }],
    "workOnCurrentBranch": True,
    "autoCreatePR": False,
}))
PY
)"

echo "==> POST $api_base/v1/agents → machine '$machine_name' @ $branch"
tmp="$(mktemp)"
code="$(curl -sS -o "$tmp" -w '%{http_code}' \
  -X POST "$api_base/v1/agents" \
  -H "Authorization: Bearer ${CURSOR_API_KEY}" \
  -H "Content-Type: application/json" \
  -d "$payload")"

cat "$tmp"
echo
if [ "$code" != "200" ] && [ "$code" != "201" ]; then
  echo "CreateAgent failed HTTP $code" >&2
  echo "Check: API key, Remote Control for Team, worker name matches, Mac awake." >&2
  exit 1
fi

python3 - "$tmp" <<'PY'
import json, sys
data = json.load(open(sys.argv[1]))
agent = data.get("agent") or data
agent_id = agent.get("id") or data.get("id")
url = agent.get("url") or ("https://cursor.com/agents/%s" % agent_id if agent_id else "")
print("Agent: %s" % (agent_id or "?"))
if url:
    print("URL:   %s" % url)
PY
