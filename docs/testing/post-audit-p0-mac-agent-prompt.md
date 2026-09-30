# Mac agent prompt — post-audit P0 playtest

Paste this into a Cursor agent started **on** Bailey's MacBook Pro
(environment dropdown → **Bailey's MacBook Pro**, not a cloud VM).
Checkout `cursor/p0-auto-landing-follow-709e` at tip `6965106c` or newer (or `main`
after #491 merges) so night-sky yaw 270 + auto-landing upgrade +
`scripts/run-post-audit-p0-remaining.sh` are present. Canonical path: `~/Code/Airside`.

A Linux cloud parent cannot pin this machine via `Task` / `env.machine` (children
land on Linux with `privateWorkerId: null`). Use the agents UI environment picker.
API CreateAgent also needs team **Enable Remote Control for Team** on
(dashboard → Cloud Agents → Self-hosted Machines).

Workers (when online): prefer displayName `~/Code/Airside @ Bailey's MacBook Pro`,
`eligibleForSubagent: true`, idle.

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
   Fast first pass (night-sky + freighter + takeoff tyres): 
   AIRSIDE_P0_ONLY=overview-night-sky-traffic,follow-freighter,follow-jet-takeoff scripts/run-post-audit-p0-remaining.sh
2. Verdict those PNGs in RESULTS.md (do not invent; if still unverified say why)
3. Listen: overview audio, follow, touchdown chirp/reverse/rollout
4. Freighter refit livery + jet tyres on rotation/flare (unblocks P2)
5. Storm final lands; hangar tow; boarding tape; follow feel; zoom-under-cursor; human-ops
6. Fill remaining Verdicts; update GAME.md; push via protected-main PR workflow

Do not invent verdicts. Do not start freight AI / P2–P4. Do not mark the cloud goal complete.
Return: uname, cwd, which rows keep/fix/revert, commit SHA, push status.
```
