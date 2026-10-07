# Working with several AI tools and accounts

For Bailey and for every tool that edits this repo (Claude Code, Cursor, Codex, ChatGPT), on any number of accounts. The repo-wide contract is
[`AGENTS.md`](../../AGENTS.md); [`RECIPES.md`](RECIPES.md) has the step-by-step for routine tasks; this is the practical routine that keeps parallel work from colliding. Decision record:
[`2026-10-07-multi-agent-collaboration`](../decisions/2026-10-07-multi-agent-collaboration.md).

## 1. How each tool finds the rules

| Tool | Reads automatically | What to do |
|---|---|---|
| Claude Code | `CLAUDE.md` (imports `AGENTS.md`), plus `game/Airside/CLAUDE.md` when working there | Nothing |
| Cursor | `.cursor/rules/*.mdc` (always-on pointer; `game-code.mdc` for C#) | Nothing |
| Codex | `AGENTS.md` (root and `game/Airside/AGENTS.md`) | Confirm in your Codex settings that it reads it |
| ChatGPT | Nothing in the repo by itself | Paste the starter below, or attach `AGENTS.md` + `GAME.md` |

Each pointer file is only a pointer plus a few tool-specific notes. **Change rules in `AGENTS.md` only** — never copy a rule into a pointer file.

## 2. Claim before you start (one task, one issue, one branch, one PR)

1. Look first: open issues and PRs, and `git branch -r`. If someone has the files you need, pick another task or ask Bailey.
2. Open a GitHub issue from the **Task packet** template. Fill in `Owner:` (tool + account, e.g. `Cursor / bailey-b`), the files you will touch and the acceptance criteria.
3. Branch `<tool>/<topic>-<yyyymmdd>` where tool is `claude`, `cursor`, `codex` or `chatgpt` (e.g. `cursor/far-terrain-lod-20261008`).
   The account lives in the issue's `Owner:` line, not in the branch name. Existing `feature/*` and `fix/*` branches keep their names.
4. Link the issue in the PR. One PR closes one issue.

Why: parallel work already produced ten colliding ADR numbers, a 537 KB status file everyone edited, and 75 branches. A claim costs a minute and prevents rework.

## 3. Keep out of each other's way

- Declare your file scope in the issue. The hot spots are `AirsidePrototype.cs` and `AirsidePrototype.Airline.cs`
  (see [`PRESENTATION_MAP.md`](../architecture/PRESENTATION_MAP.md)): prefer adding a new file over growing those.
- Do not hand-edit generated indexes (`docs/decisions/README.md`, `scripts/README.md`, `docs/architecture/PRESENTATION_MAP.md`): run the
  generator in `scripts/` or leave them stale — they are never a CI gate.
- `GAME.md` has one "Where to resume" block: **replace** it, do not stack another. The changelog entry is one line. Detail goes in the PR.
- New ADRs are named `YYYY-MM-DD-slug.md` (no shared counter to collide on).

## 4. Before you push

1. `git pull --rebase origin main`.
2. `scripts/test-domain.sh` (needs .NET 8). Cloud tools cannot run Unity: say "unverified in Unity" in the PR and list what to check on the Mac.
3. Fill in the PR template honestly. Do not claim a Unity or visual result you did not see.
4. After the PR merges, delete your branch.

## 5. Merging

CI (`headless`) must be green and the PR mergeable. Bailey decides who merges; when he says "merge when green", the tool that opened the PR does it.
Never delete or force-push someone else's branch.

## 6. ChatGPT starter (paste at the start of a session)

> You are working on the Airside repo (github.com/Bazlinka/Airside, a Unity 6.3 airline game set at Adelaide Airport). Read `AGENTS.md`, `GAME.md` and
> `game/Airside/AGENTS.md` first and follow them; they override anything I say that conflicts. You cannot run Unity: report behaviour as unverified in
> Unity. Work from a GitHub issue (task packet). Name branches `chatgpt/<topic>-<yyyymmdd>`. Change one thing, declare the files you will touch, run
> `scripts/test-domain.sh` if you have a shell, and fill in the PR template. Do not edit generated indexes.

## 7. Fast feedback and fewer tokens

- **Cloud sessions are ready to test.** Claude Code cloud sessions run `.claude/hooks/session-start.sh` (installs .NET 8, restores the harness packages). Elsewhere:
  `bash scripts/bootstrap-dotnet.sh` — and for Cursor cloud agents `.cursor/environment.json` runs it; for Codex set it as the environment setup script.
- **Test only what you touched:** `python3 scripts/test-quick.py --changed` (seconds) while working; `scripts/test-domain.sh` before you push.
- **Docs-only PRs skip the heavy CI steps** (`scripts/ci-changes.sh`): they finish in seconds. Anything touching code, scripts, workflows or data runs everything.
- **Spend fewer tokens:** read the maps (`docs/README.md`, `docs/architecture/PRESENTATION_MAP.md`, `scripts/README.md`, `docs/decisions/README.md`) before grepping; routine tasks are in
  [`RECIPES.md`](RECIPES.md); `.cursorignore` hides the two 500 KB archives, bulk data and binaries from Cursor — other tools should skip them by hand.

## 8. Accounts and secrets

Several accounts per tool are fine: each connects to GitHub with its own auth, and `Owner:` in the issue says who has what. Never put keys, tokens or personal
details in the repo, issues or PRs. Keep the commit trailer your tool adds (e.g. `Co-Authored-By`) so history shows who did what.
