# Mac agent prompt — post-audit P0 playtest

**Prefer Terminal (no agent pin):** see `docs/testing/post-audit-p0-mac-terminal.md`
when the Mac is awake — one paste `scripts/run-post-audit-p0-stages.sh` at
`~/Code/Airside` (or Stage A→B→C separately).

Optional Cursor path: paste the block below into a Cursor agent started **on**
Bailey's MacBook Pro (environment dropdown → **Bailey's MacBook Pro**, not a cloud VM).
Checkout `cursor/p0-freighter-pick-lock-709e` (or `main` after merge) with auto-takeoff ~1330s /
landing batch ~780–786s / boarding batch ~320–323s (or `main` after #491 merges)
so night-sky yaw 270 + multi-shot review + jet TakingOff mid-roll wait +
`scripts/run-post-audit-p0-remaining.sh` are present. Rebuild required for
multi-shot. Canonical path: `~/Code/Airside`. Fast pass jet takeoff alone ~25 min.

A Linux cloud parent cannot pin this machine via `Task` (no `workerId` /
`privateWorkerId` on Task; children land on Linux with `privateWorkerId: null`).
Use the agents UI environment picker, or from any machine with an API key:
`export CURSOR_API_KEY=…` then `scripts/launch-p0-mac-agent.sh`
(`POST /v1/agents` with `env.type=machine`, name `Bailey's MacBook Pro`, and
`env.worker_id` for the `~/Code/Airside` worker). Defaults to Stage A; set
`AIRSIDE_P0_LAUNCH_FULL=1` for A→B→C. API CreateAgent also needs team
**Enable Remote Control for Team** (dashboard → Cloud Agents → Self-hosted).

Workers (when online): prefer displayName `~/Code/Airside @ Bailey's MacBook Pro`,
`eligibleForSubagent: true`, idle. If `list-self-hosted-workers` is empty, run
`cursor worker start` on the Mac first, then pin via the agents UI.

---

```
You are on Bailey's MacBook Pro. Confirm with `uname -s` → Darwin. If not Darwin, stop.

Repo: ~/Code/Airside (canonical). Plan ADR 0205. P0 manual close-out only.
Automated stills already recorded in docs/testing/post-audit-p0-2026-09-30/RESULTS.md
(mostly keep). Do not re-run the full matrix unless re-capturing night sky traffic.

Follow docs/testing/post-audit-p0-manual-checklist.md in order:
1. Prefer one command: scripts/run-post-audit-p0-remaining.sh
   (pull + build + remaining captures + copy PNGs + stamp inventory into RESULTS —
   inventory is not a verdict)
   Stage A (minutes — night-sky + freighter, unblocks P2 freighter evidence):
   AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter scripts/run-post-audit-p0-remaining.sh
   Stage B (jet tyre rotation — ~25 min soak for jet TakingOff ~1330s mid-roll; reuse Stage A player;
   OK if Stage A left RESULTS/PNGs dirty — do not commit between stages unless you want to):
   AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-jet-takeoff scripts/run-post-audit-p0-remaining.sh
   Stage C (rest of remaining stills — hangar/boarding/landings; same SKIP_*):
   AIRSIDE_P0_SKIP_BUILD=1 AIRSIDE_P0_SKIP_PULL=1 AIRSIDE_P0_ONLY=follow-hangar-tow,follow-boarding-tape,follow-human-ops-close,follow-jet-day,follow-jet-close,follow-storm-landing scripts/run-post-audit-p0-remaining.sh
2. Verdict those PNGs in RESULTS.md (do not invent; if still unverified say why)
3. Listen: overview audio, follow, touchdown chirp/reverse/rollout
4. Freighter refit livery + jet tyres on rotation/flare (unblocks P2)
5. Storm final lands; hangar tow; boarding tape; follow feel; zoom-under-cursor; human-ops
6. Fill remaining Verdicts; update GAME.md; push via protected-main PR workflow

Do not invent verdicts. Do not start freight AI / P2–P4. Do not mark the cloud goal complete.
Return: uname, cwd, which rows keep/fix/revert, commit SHA, push status.
```
