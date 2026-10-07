# Multi-tool collaboration: one routine, generated indexes, date-named ADRs

Status: accepted
Date: 2026-10-07 (requested by Bailey: make the codebase easier and more efficient for improvement by several AI tools — Cursor, Claude, ChatGPT — on several accounts)

- **Decision:**
  1. **One rulebook, thin pointers.** `AGENTS.md` is the only place rules live. `CLAUDE.md` (imports `AGENTS.md`), `.cursor/rules/*.mdc` and
     `game/Airside/CLAUDE.md` are pointers plus a few tool-specific notes; they no longer restate rules. `game/Airside/AGENTS.md` holds the
     Unity-project guidance (layering, pitfalls, checks) outside `Assets/` so it needs no `.meta` file.
  2. **A claim protocol.** One task = one GitHub issue (Task packet template with `Owner: tool + account` and a declared file scope) = one branch
     `<tool>/<topic>-<yyyymmdd>` = one PR (PR template). Details and a ChatGPT starter prompt: `docs/ai/WORKFLOW.md`.
  3. **ADRs are named `YYYY-MM-DD-slug.md` from now on.** Existing ADRs 0001–0252 keep their numbers (ten were claimed twice; nothing is renumbered
     because code and docs cite them). A date and slug cannot collide the way a shared counter did.
  4. **Indexes are generated, never hand-edited, never a CI gate:** `scripts/index-adrs.py` → `docs/decisions/README.md`, `scripts/index-scripts.py` →
     `scripts/README.md`, `scripts/map-presentation.py` → `docs/architecture/PRESENTATION_MAP.md`. A stale index blocks nobody, and no two PRs fight
     over the same index line.
- **Reason:** parallel work had produced ten colliding ADR numbers, a 537 KB status file and a 392 KB changelog that every tool edited, 75 branches with
  no owner, three files restating rules that drifted, and 182 scripts with no index. Each cold session spent tokens rediscovering structure.
- **Affected systems:** documentation, templates and generator scripts only. No game code, assets, saves or tests changed.
- **Not decided here (Bailey):** real Unity feedback for cloud agents (a self-hosted runner on the Mac or a Unity licence secret in CI); Git LFS for the
  large FBX and image assets (rewrites history); GitHub branch protection and who merges. They are the larger levers and need your sign-off.
- **Unverified:** that each tool reads its pointer file as documented (Codex → `AGENTS.md`, Cursor → `.cursor/rules`, Claude Code → `CLAUDE.md` with `@` imports);
  confirm once per tool. ChatGPT reads nothing automatically; the starter prompt covers it.
- **Revert:** restore the previous pointer files and delete the templates; the generators are independent scripts.
