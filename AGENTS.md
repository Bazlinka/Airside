# Airside working rules

This file is the shared contract for every contributor — Bailey, ChatGPT, Cursor,
Codex and Claude. Read it and `GAME.md` before making any change.

## Where the truth lives

- **The git repository is the single source of truth.** Not a chat transcript, not a
  local copy on one machine. If it is not committed and pushed, it does not exist.
- **GitHub remote:** `origin` (private). Every tool and device syncs through it.
- **`GAME.md`** is the living status board: current milestone, invariants, controls,
  evidence, and the next approved work. Update it in the same commit that changes
  behaviour.
- **`docs/product/Airside Project Plan.docx`** (and `docs/product/PROJECT_PLAN.md`)
  hold the agreed long-term design. Change these only with Bailey's sign-off.

## Repository map

```
Airside/
  README.md                  Orientation and first-run steps
  GAME.md                    Living status board — read before every task
  CHANGELOG.md               One line per merged change, newest first
  AGENTS.md                  This file — the shared working contract
  CLAUDE.md                  Pointer for Claude Code (defers to this file)
  .cursor/rules/             Pointer for Cursor (defers to this file)
  game/Airside/              The Unity 6.3 LTS macOS game (open THIS in Unity)
    Assets/Airside/
      Domain/                Pure rules: time, ids — no UnityEngine types
      Simulation/            Airport simulation — deterministic, clock-injected
      Persistence/           Save schema, load/catch-up
      Presentation/          MonoBehaviours, camera, visuals — Unity-facing only
      Editor/                Editor-only startup helpers
      Tests/EditMode/        Deterministic NUnit tests
      Scenes/AirsidePrototype.unity
    ProjectSettings/
  companion/AirsideCompanion/ SwiftUI iPhone companion (starts after save/sync is stable)
  docs/
    product/                 Design plan (.docx + .md), agreed scope
    architecture/            Technical decisions and data contracts
    decisions/               Numbered decision records (ADRs)
    data/                    Asset and data licence register
    testing/                 Acceptance checks and fixtures
  scripts/
    test-unity.sh            Deterministic simulation checks
    build-mac.sh             Local macOS application build
  work/                      Local scratch, downloads, builds — git-ignored, never committed
```

New code goes in the matching folder above. If nothing fits, add the folder and
note it here in the same commit.

## Git workflow (all tools follow this)

1. **Start clean:** `git pull --rebase origin main` before touching anything.
2. **One change, one owner.** A single task is owned by one tool at a time. For
   parallel work use a branch (`feature/<short-name>`) or a git worktree, with
   explicit, non-overlapping file boundaries. Never make simultaneous edits to the
   same system from two tools.
3. **Keep commits narrow and reviewable** — one acceptance criterion per commit.
4. **Run the checks** in `scripts/test-unity.sh` and confirm the project compiles
   in Unity 6.3 LTS before committing behaviour changes. No commit rests on an
   agent's claim alone that a build passed.
5. **Update `GAME.md` and `CHANGELOG.md`** in the same commit as the change.
6. **Commit message:** short imperative subject, then what changed and the
   evidence. Push to `origin` immediately so other tools see it.
7. **Decisions:** a design change adds a dated entry under `docs/decisions/`
   (date, decision, reason, affected systems, migration impact). New ideas go to a
   backlog, not straight into the active milestone.

## Tool roles (flexible, but one owner per change)

| Participant | Primary role |
|---|---|
| Bailey   | Product owner, final design decisions, playtesting, release authority |
| ChatGPT  | Product design, system specs, planning, cross-system review |
| Cursor   | Focused implementation and interactive work inside the codebase |
| Codex    | Repository inspection, scoped implementation, builds, tests, debugging |
| Claude   | Independent architecture review, large-context review, second opinions |

## Design invariants (do not break)

- Domain and simulation code stay independent of Unity scenes and presentation.
- Time comes from an injected clock; random choices from a seeded source.
- Runways, taxiways and stands must be reserved before use.
- Frame rate must not change simulation outcomes.
- Every state-changing command is identifiable and safe to apply exactly once.
- Preserve save compatibility: any persisted schema change needs an explicit
  version and a migration path.
- No external data, code, image, audio or 3D asset enters the project without a
  recorded licence, attribution, cost and fallback in `docs/data/`.
- Add systems in the order set by the project plan. Do not start broad content
  production before the first aircraft loop is stable.
- Keep changes narrow, reviewable and tied to an acceptance criterion.

## Required task packet

Every implementation task states: the player-visible outcome; files or module in
scope; relevant decisions and invariants; acceptance criteria; tests or playtest
steps; and what must remain unchanged.
