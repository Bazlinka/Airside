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
Plan: docs/plans/post-audit-improvement-plan.md (ADR 0202). P0 only.

1. git fetch origin && git checkout cursor/post-audit-improvement-plan-709e && git pull --rebase origin cursor/post-audit-improvement-plan-709e
2. Keep the display awake. scripts/build-mac.sh
3. scripts/review-post-audit-p0.sh
4. git checkout -b cursor/post-audit-p0-results-709e
5. Fill keep/fix/revert in the RESULTS.md the script wrote under work/captures/post-audit-p0-<date>/, then copy to docs/testing/post-audit-p0-2026-09-30/RESULTS.md (create the date folder if needed). Use docs/testing/post-audit-p0-2026-09-30/CODE_EVIDENCE.md for code-green notes only — do not mark visual/audio rows keep without eyes/ears.
6. Complete the manual rows in docs/testing/post-audit-p0-playtest.md (audio, freighter livery, tyres, hangar tow, follow feel, etc.) as best you can from packaged play + captures.
7. Update GAME.md handoff (P0 done or remaining fixes) and CHANGELOG Unreleased. Commit. git push -u origin cursor/post-audit-p0-results-709e

Do not invent verdicts. Do not start P1/P2/P3/P4. Do not mark the post-audit cloud goal complete.
Return: uname, cwd, build exit, review exit, RESULTS path, commit SHA, push status, any fix/revert rows.
```
