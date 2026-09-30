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

Repo: prefer ~/Documents/Codex/Airside, else ~/Code/Airside.
Plan: docs/plans/post-audit-improvement-plan.md (ADR 0205). P0 only.

1. Prefer one command (keeps display awake yourself): `scripts/run-post-audit-p0.sh`
   Or manually: from clean `main`, `scripts/build-mac.sh`,
   `scripts/review-post-audit-p0.sh`.
2. Fill keep/fix/revert in the RESULTS.md under `docs/testing/post-audit-p0-<date>/`
   (capture inventory is auto-stamped — PNG/log status is not a verdict). Use CODE_EVIDENCE.md
   for Notes only; do not mark visual/audio rows keep without eyes/ears.
3. Complete the manual rows (audio, freighter livery, tyres, hangar tow, follow feel, etc.).
4. Update GAME.md handoff (P0 done or remaining fixes) and CHANGELOG Unreleased.
   Publish completed results through the protected-main PR workflow in AGENTS.md.

Do not invent verdicts. Do not start P1/P2/P3/P4. Do not mark the post-audit cloud goal complete.
Return: uname, cwd, build exit, review exit, RESULTS path, commit SHA, push status, any fix/revert rows.
```
