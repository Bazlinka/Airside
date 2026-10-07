# A faster feedback loop for AI tools: ready sessions, targeted tests, lighter CI

Status: accepted
Date: 2026-10-07 (follow-up to `2026-10-07-multi-agent-collaboration`; Bailey asked what else could speed up future AI work)

- **Decision:**
  1. **Sessions start ready.** `scripts/bootstrap-dotnet.sh` installs the .NET 8 SDK without root if it is missing (and links `dotnet` onto the PATH when it can), then pre-restores both NuGet
     sets the harness uses. Claude Code cloud sessions run it from a synchronous `SessionStart` hook (`.claude/hooks/session-start.sh`, remote sessions only); Cursor cloud agents run it
     from `.cursor/environment.json` (that file already existed with an `apt-get install dotnet-sdk-8.0` install; the script keeps that route as a fallback after the no-root download, and the file's
     `name` is unchanged); Codex takes it as the environment setup script.
  2. **Test what you touched.** `scripts/test-quick.py --changed` maps changed C# files to the headless test classes that mention them and runs only those (seconds, not ~3 minutes).
     It is a pre-check; `scripts/test-domain.sh` still runs everything before a push.
  3. **Docs-only PRs skip the heavy CI steps.** `scripts/ci-changes.sh` classifies the changed paths against a strict allow-list (root docs, `docs/{ai,architecture,decisions,history,plans}`, tool
     guidance, issue/PR templates); anything else, every push to `main` and any doubt runs the full set. The single `headless` job still reports success, so the check name is unchanged.
     `docs/data` (read by generators) and `docs/testing` (referenced by a script test) are deliberately not on the list.
  4. **Less to read, fewer hand-made files.** `scripts/new-meta.py` creates a Unity `.meta` (fresh GUID, importer cloned from a neighbour); `docs/ai/RECIPES.md` has the routine tasks step by
     step; `.cursorignore` hides the two ~500 KB archives, bulk data and binaries from Cursor.
- **Reason:** in this session every cloud run lost its first minutes to "dotnet SDK not found", one-line changes cost a 3-minute suite, a docs-only PR waited ~7 minutes for CI it could not affect,
  and `.meta` files were hand-made three times.
- **Affected systems:** tooling, CI workflow and documentation. No game code, assets, saves or tests changed.
- **Evidence:** bootstrap verified from a bare environment (empty HOME, minimal PATH): installs 8.0.425 (and, with the download deliberately broken, falls back to apt and installs 8.0.131), restores, writes the PATH export, is idempotent (2 s on re-run) and a new shell then finds
  `dotnet` through the link; the hook is a no-op locally and installs on a remote run; `test-quick.py --changed` ran 25 tests in ~11 s for a one-file change; `scripts/test-ci-changes.sh`
  covers 14 path sets (it caught a bug that dropped an unterminated last line); `new-meta.py` output passed `audit-unity-assets.py`. Replaying the last 12 merged PRs, 2 would have been docs-only
  (savings are modest, not most PRs).
- **Unverified:** the docs-only path in GitHub Actions itself (this PR changes the workflow, so it runs the full set; the first docs-only PR exercises the shortcut); that Cursor reads
  `.cursor/environment.json` and `.cursorignore` as expected; the Codex setup-script step (a setting in Codex, not a file).
- **Revert:** delete the `.claude/` hook and settings, `.cursor/environment.json`, `.cursorignore`; remove the `What changed` step and the `if:` lines from `headless.yml`.
