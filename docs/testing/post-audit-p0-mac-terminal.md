# Mac Terminal — post-audit P0 Stage A→B→C (no Cursor agent)

Cloud Linux cannot pin My Machines. If the Mac is awake (`cursor worker` may be
running), you can run the remaining stills **directly in Terminal.app** — no
agents UI pin required. Keep the display awake. Canonical checkout: `~/Code/Airside`.

Verdicts (keep/fix/revert) still need your eyes/ears afterward — inventory is not a verdict.

```bash
cd ~/Code/Airside
# Tip branch while #492 is open; falls back to main after merge:
bash scripts/p0-checkout-mac-tip.sh
# Or: git fetch && git checkout cursor/p0-freighter-pick-lock-709e && git pull --ff-only

# One paste — Stage A → B → C (~45+ min with jet takeoff ~1330s + landing/boarding batches;
# caffeinate -d -i wraps the whole run so builds/gaps cannot sleep the display;
# rebuild so multi-shot review lands; macOS notification when finished)
scripts/run-post-audit-p0-stages.sh
# Or double-click in Finder: scripts/run-post-audit-p0-stages.command
# Short path (minutes only — night-sky + freighter): scripts/run-post-audit-p0-stage-a.command

# Or run stages separately:
# Stage A (minutes — night-sky + freighter; unblocks P2 freighter evidence)
# AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/run-post-audit-p0-remaining.sh
# Stage B (~25 min soak — jet TakingOff ~1330s mid-roll; reuse player; dirty RESULTS OK)
# AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-jet-takeoff scripts/run-post-audit-p0-remaining.sh
# Stage C (hangar / boarding / landings)
# AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing scripts/run-post-audit-p0-remaining.sh
```

Then fill Verdict columns in `docs/testing/post-audit-p0-2026-09-30/RESULTS.md`
(do not invent), finish listening/play rows in
`docs/testing/post-audit-p0-manual-checklist.md`, update `GAME.md`, and push via
the protected-main PR workflow.

Cursor-agent paste path (optional): `docs/testing/post-audit-p0-mac-agent-prompt.md`.

API pin (optional, when not at the Mac): Dashboard → API Keys, then
`export CURSOR_API_KEY=…` and `scripts/launch-p0-mac-agent.sh`. Posts
`POST https://api.cursor.com/v1/agents` with
`env: { type: machine, name: "Bailey's MacBook Pro" }` on tip
`cursor/p0-freighter-pick-lock-709e` (or `main` after merge). Needs team **Enable Remote Control for
Team** and an awake worker named exactly `Bailey's MacBook Pro`. Cloud `Task`
cannot do this pin (confirmed; no `workerId` on Task).

GitHub trigger (optional): only works when **Bailey** posts as a trusted
commenter linked to the Mac's Cursor account. A `cursor[bot]` cloud comment on
#492 (or any cloud bot comment) does not claim My Machines (tried 2026-09-30 on
#491; workers stayed idle). Prefer Terminal / Finder above.
