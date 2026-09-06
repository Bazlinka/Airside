# Claude Code — Airside

Read [`AGENTS.md`](AGENTS.md) and [`GAME.md`](GAME.md) before any task. They are the
shared working contract for every contributor (Bailey, ChatGPT, Cursor, Codex,
Claude) and take precedence over anything here.

Quick pointers:

- The Unity game is `game/Airside/` (Unity 6.3 LTS). Open that folder in Unity.
- Run checks: `scripts/test-unity.sh`. Local Mac build: `scripts/build-mac.sh`.
- The git repo is the single source of truth. `git pull --rebase` before work;
  update `GAME.md` and `CHANGELOG.md` in the same commit; push to `origin`
  straight after.
- One change, one owner. Use a `feature/<name>` branch for parallel work.
- Claude's standing role here is independent architecture and large-context
  review — inspect the real code and build state, never merge on a claim alone.
