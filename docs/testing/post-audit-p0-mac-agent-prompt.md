# Mac agent prompt — post-audit P0 playtest

Paste this into a Cursor agent started **on** Bailey's MacBook Pro
(environment dropdown → **Bailey's MacBook Pro**, not a cloud VM).

A Linux cloud parent cannot pin this machine via `Task`. Placement needs
`CreateAgent` with `machine: { "type": "self_hosted_worker", "worker_id": "…" }`
(requires team Remote Control) or this UI picker / API v1 `env.type: "machine"`.

Workers (when online): `list-self-hosted-workers` — prefer
`~/Documents/Codex/Airside` or `~/Code/Airside`, `eligibleForSubagent: true`, idle.

---

```
You are on Bailey's MacBook Pro. Confirm with `uname -s` → Darwin. If not Darwin, stop.

Repo: ~/Code/Airside (canonical). Plan ADR 0205. P0 manual close-out only.
Automated stills already recorded in docs/testing/post-audit-p0-2026-09-30/RESULTS.md
(mostly keep). Do not re-run the full matrix unless re-capturing night sky traffic.

Follow docs/testing/post-audit-p0-manual-checklist.md in order:
1. git pull; scripts/build-mac.sh (need the night-sky framing fix)
2. Re-capture overview-night-sky-traffic (9 km / pitch 12 / yaw 210) and verdict it
3. Listen: overview audio, follow, touchdown chirp/reverse/rollout
4. Freighter refit livery + jet tyres on rotation/flare (unblocks P2)
5. Storm final lands; hangar tow; boarding tape; follow feel; zoom-under-cursor; human-ops
6. Fill Verdicts in RESULTS.md; update GAME.md; push via protected-main PR workflow

Do not invent verdicts. Do not start freight AI / P2–P4. Do not mark the cloud goal complete.
Return: uname, cwd, which rows keep/fix/revert, commit SHA, push status.
```
