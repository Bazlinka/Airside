#!/usr/bin/env bash
# Launch a Cloud Agent pinned to Bailey's MacBook Pro for P0 Stage A→B→C (ADR 0205).
#
# Needs:
#   - CURSOR_API_KEY from https://cursor.com/dashboard → API Keys
#   - Team toggle "Enable Remote Control for Team" (Cloud Agents → Self-hosted)
#   - Mac awake with: cursor worker start --name "Bailey's MacBook Pro"
#     (either ~/Code/Airside or ~/Documents/Codex/Airside — same checkout)
#
# Usage:
#   export CURSOR_API_KEY=…
#   scripts/launch-p0-mac-agent.sh
#
# Optional pin by worker id (preferred when known — unique; machine name is not):
#   AIRSIDE_P0_WORKER_ID=122eb692-14f4-5844-a844-517776b831ca scripts/launch-p0-mac-agent.sh
#
# Prefer Terminal/Finder when you are at the Mac:
#   docs/testing/post-audit-p0-mac-terminal.md
set -euo pipefail

api_base="${CURSOR_API_BASE:-https://api.cursor.com}"
machine_name=${AIRSIDE_P0_MACHINE_NAME:-"Bailey's MacBook Pro"}
# Default to whichever Airside worker was last listed online (override if stale).
# 4566aff1 = ~/Documents/Codex/Airside (symlink to Code); 122eb692 = ~/Code/Airside.
worker_id="${AIRSIDE_P0_WORKER_ID:-4566aff1-0678-5a57-9d15-895db6b35c1c}"
branch="${AIRSIDE_P0_BRANCH:-cursor/p0-freighter-pick-lock-709e}"
repo_url="${AIRSIDE_P0_REPO_URL:-https://github.com/Bazlinka/Airside}"
# Stage A only by default (minutes). Set AIRSIDE_P0_LAUNCH_FULL=1 for A→B→C (~45+ min).
full="${AIRSIDE_P0_LAUNCH_FULL:-0}"

if [ -z "${CURSOR_API_KEY:-}" ]; then
  echo "Set CURSOR_API_KEY (Dashboard → API Keys). Or use Terminal:" >&2
  echo "  docs/testing/post-audit-p0-mac-terminal.md" >&2
  exit 2
fi

if [ "$full" = "1" ]; then
  run_cmd='scripts/run-post-audit-p0-stages.sh'
  run_note='Full A→B→C (~45+ min; caffeinate wraps).'
else
  run_cmd='scripts/run-post-audit-p0-stage-a.command'
  run_note='Stage A only (night-sky + freighter; rebuild required). Then B/C with SKIP_BUILD.'
fi

prompt_text="$(cat <<EOF
You are pinned to Bailey's MacBook Pro. Confirm \`uname -s\` → Darwin; stop if not.

Canonical checkout: ~/Code/Airside (~/Documents/Codex/Airside is the same tree).
ADR 0205 P0 remaining captures only. Do not invent RESULTS keep/fix/revert.

1. Prefer cd ~/Code/Airside; if missing, use ~/Documents/Codex/Airside or \$PWD
   when it is already an Airside checkout (worker cwd).
2. git fetch origin && git checkout ${branch}
3. git pull --ff-only origin ${branch}
4. Keep the display awake. Run: ${run_cmd}
   (${run_note})
   If .command is awkward from this shell, run the same body:
   bash scripts/p0-checkout-mac-tip.sh && export AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter && bash scripts/run-post-audit-p0-remaining.sh
5. After PNGs land under docs/testing/post-audit-p0-2026-09-30/, fill Verdict
   columns in RESULTS.md by eye/ear only. Leave unverified if unsure.
   Night-sky log must show pose pitch≈8 (fail-closed overview framing on tip).
6. Do not start freight AI / P2–P4. Do not mark the cloud goal complete.

Return: uname, tip SHA, which PNGs were written, which verdicts you filled (if any).
EOF
)"
payload="$(CURSOR_PROMPT_TEXT="$prompt_text" \
  AIRSIDE_P0_MACHINE_NAME="$machine_name" \
  AIRSIDE_P0_WORKER_ID="$worker_id" \
  AIRSIDE_P0_BRANCH="$branch" \
  AIRSIDE_P0_REPO_URL="$repo_url" \
  AIRSIDE_P0_LAUNCH_FULL="$full" \
  python3 - <<'PY'
import json, os
worker_id = os.environ.get("AIRSIDE_P0_WORKER_ID", "").strip()
env = {"type": "machine", "name": os.environ["AIRSIDE_P0_MACHINE_NAME"]}
# API accepts worker_id for a unique pin when the machine has multiple workers.
if worker_id:
    env["worker_id"] = worker_id
full = os.environ.get("AIRSIDE_P0_LAUNCH_FULL", "0") == "1"
print(json.dumps({
    "name": "P0 Mac Stage A→B→C" if full else "P0 Mac Stage A",
    "prompt": {"text": os.environ["CURSOR_PROMPT_TEXT"]},
    "env": env,
    "repos": [{
        "url": os.environ["AIRSIDE_P0_REPO_URL"],
        "startingRef": os.environ["AIRSIDE_P0_BRANCH"],
    }],
    "workOnCurrentBranch": True,
    "autoCreatePR": False,
}))
PY
)"

echo "==> POST $api_base/v1/agents → machine '$machine_name' worker_id='${worker_id:-none}' @ $branch"
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
  echo "Check: API key, Remote Control for Team, worker name/id matches, Mac awake." >&2
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
