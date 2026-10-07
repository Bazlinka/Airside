# Claude Code — Airside

@AGENTS.md

`AGENTS.md` (imported above) is the shared contract for every tool and takes precedence over anything here. Then read `GAME.md`
(current state, ~170 lines). How several tools share this repo: `docs/ai/WORKFLOW.md`. Where code lives: `game/Airside/AGENTS.md`.

Claude-specific:

- Cloud sessions have no Unity editor. Run `scripts/test-domain.sh` before every push and say plainly that behaviour is "unverified in
  Unity"; never present a headless pass as a Unity pass.
- Claude's standing role is independent architecture and large-context review: inspect the real code and build state, never merge on a claim alone.
