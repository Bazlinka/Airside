# Claude Code — Airside

@AGENTS.md

`AGENTS.md` (imported above) is the shared contract for every tool and takes precedence over anything here. Then read `GAME.md`
(current state, ~170 lines). How several tools share this repo: `docs/ai/WORKFLOW.md`. Where code lives: `game/Airside/AGENTS.md`.

Claude-specific:

- Follow the testing and merge policy in `AGENTS.md`; cloud sessions must report unavailable Unity verification honestly.
- Claude's standing role is independent architecture and large-context review: inspect the real code and build state, never merge on a claim alone.
