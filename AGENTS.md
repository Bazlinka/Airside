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
  GAME.md                    Current state only (~170 lines) — read before every task
  CHANGELOG.md               One line per merged change, newest first (older: docs/history/)
  AGENTS.md                  This file — the shared working contract
  CLAUDE.md                  Pointer for Claude Code (defers to this file)
  .cursor/rules/             Pointer for Cursor (defers to this file)
  game/Airside/              The Unity 6.3 LTS macOS game (open THIS in Unity); AGENTS.md here = code-area guidance
    Assets/Airside/
      Domain/                Pure rules: time, ids — no UnityEngine types
      Simulation/            Airport simulation — deterministic, clock-injected
      Presentation/          MonoBehaviours, camera, visuals — Unity-facing only
      Art/                   Approved runtime models, textures, UI, animation and VFX
    Assets/Resources/Airside/Audio/
                             Runtime-loadable approved AudioClips; record every source in the asset register
      Editor/                Editor-only startup helpers
      Tests/EditMode/        Deterministic NUnit tests
      Scenes/AirsidePrototype.unity
    ProjectSettings/
  companion/AirsideCompanion/ SwiftUI iPhone companion (starts after save/sync is stable)
  docs/
    product/                 Design plan (.docx + .md), agreed scope
    architecture/            Technical decisions and data contracts
    decisions/               Numbered decision records (ADRs); README.md is the index and numbering rule
    history/                 Archived handoff log and changelog (verbatim, read-only); docs/README.md maps it all
    ai/                      WORKFLOW.md — how several AI tools and accounts share this repo (claim, branch, verify, merge)
    data/                    Asset and data licence register
    art/                     Canonical art direction, manifest, prompts and references
    testing/                 Acceptance checks and fixtures
  scripts/
    test-unity.sh            Deterministic simulation checks (source of truth; needs a Mac Unity editor)
    index-adrs.py / index-scripts.py
                             Regenerate docs/decisions/README.md and scripts/README.md (generated, never hand-edited, never a CI gate)
    map-presentation.py      Regenerates docs/architecture/PRESENTATION_MAP.md (what each AirsidePrototype partial owns); --check
                             fails on a stale map or an undescribed new partial — add its line in the same commit
    bootstrap-dotnet.sh      Installs the .NET 8 SDK without root and restores the harness packages (cloud sessions run it automatically)
    test-quick.py            Fast loop: runs only the headless tests that mention your changed C# (`--changed`); not a replacement for test-domain.sh
    new-meta.py              Creates the Unity .meta (fresh GUID) for a new file or folder under Assets/
    ci-changes.sh            CI helper: docs-only change lists skip the heavy steps (tested by test-ci-changes.sh)
    check-unity-nunit.sh     Compile-only check against Unity's NUnit 3.5, no implicit usings (CI + test-domain.sh; ADR 0252)
    test-domain.sh           Headless dotnet test mirror of the EditMode Domain/Simulation
                             tests, for machines without Unity — supplementary, not a replacement
    aircraft_skin.py         Shared skin-conforming doors/windows/panels and gear pods for the aircraft
                             generators (curved shells that hug the fuselage, a few mm proud)
    fit-aircraft-doors.py  Audits that every door sits ~1 cm proud of its real hull (CI runs `audit`) and
                             refits a type's door parts to its hull (`fit A320`); stdlib only
    audit-aircraft-geometry.py
                             Floating-part / door-flush audit and multi-view z-buffered renders of the
                             runtime aircraft glTFs; test-aircraft-connectivity.py runs it as a check
    generate-aircraft-title-layout.py
                             Fits each type's fuselage title/registration to its mesh (--check);
                             test-aircraft-paint.py and render-aircraft-paint.py verify the paint
    import-quaternius-people.py
                             Re-exports the CC0 Quaternius passengers/ramp crew (bpy) into
                             Resources/Airside/Characters with the kept clips, forced opaque
    sync-art-streaming-assets.sh
                             Copy runtime glTF/PNG art into StreamingAssets for packaged builds
    audit-unity-assets.py    Check Unity metadata GUIDs and byte-identical packaged art mirrors
    update-harness.py        Derives which Presentation files/tests the headless harness compiles
                             (Harness.Generated.props); CI fails if it is stale
    dotnet-harness/          csproj backing test-domain.sh; the file list is generated
    build-mac.sh             Local macOS application build
    build-mac-notifications.sh
                             Compiles the original universal Notification Centre bundle before Mac builds
    native/                  Original platform bridge sources (macOS notifications); generated binaries are ignored
    capture-game.sh          Screenshot from the built game, unattended (--follow REG, --delay s);
                             keeps the display awake — an asleep display freezes the Unity player
  work/                      Local scratch, downloads, builds — git-ignored, never committed
```

New code goes in the matching folder above. If nothing fits, add the folder and
note it here in the same commit.

## Testing and merge policy — Bailey's standing instruction (8 October 2026)

Prioritise implementation and progress. Bailey will choose when to playtest and
report bugs. This policy replaces earlier default requirements for full test runs
and repeated merge approval; Bailey's latest task instructions take precedence.

- **Ordinary changes:** inspect the diff and use only quick checks relevant to the
  change. Prefer a readily available syntax/compile check for changed code and a
  small existing regression check when useful. Docs-only changes need no game tests.
- **Do not automatically run broad testing:** no full `test-domain.sh` or
  `test-unity.sh`, packaged builds, rendered reviews, gameplay journeys or soak runs
  unless Bailey requests that testing/review. Do not add tests just to mirror a
  simple implementation or repeatedly recheck an unchanged result.
- **Keep checks bounded:** stop a local check after about 60 seconds if it stalls
  or needs lengthy setup; record it as unverified and continue. Do not launch Unity
  or install a runtime solely to satisfy a routine pre-merge checklist. Fix a known
  new syntax/compile error before merging; unavailable verification is not a failure.
- **Merge completed authorised work into `main` by default:** use a narrow branch
  and PR, push, resolve routine conflicts, then merge without asking Bailey again.
  Completed PRs should be ready, not left as drafts awaiting routine permission.
  Keep genuinely incomplete work on a pushed branch/draft with a clear handoff.
- **CI:** respect checks/protections actually enforced by GitHub; do not disable
  tests or bypass protection. Optional pending checks and confirmed pre-existing
  failures do not require waiting or another approval. Investigate new failures
  attributable to the change before merging. Report actual blockers plainly.
- **Agent gameplay:** reserve `scripts/agent-gameplay.py` for agent QA when Bailey asks for gameplay testing. Use selected features or `--profile full`; preview with `--plan`. It is hidden opt-in automation, not a player feature or routine merge gate. See `docs/testing/agent-gameplay/README.md` for build reuse, private saves and coverage limits.
- **Be honest:** state what was checked, skipped or unverified. A merge does not
  establish a Unity compile, rendered playtest, packaged build or performance pass.
  Preserve simulation, save compatibility and other tools' active work.

## Git workflow (all tools follow this)

1. **Start clean:** `git pull --rebase origin main` before touching anything.
2. **One change, one owner.** A single task is owned by one tool at a time. For
   parallel work use a branch (`feature/<short-name>`) or a git worktree, with
   explicit, non-overlapping file boundaries. Never make simultaneous edits to the
   same system from two tools.
3. **Keep commits narrow and reviewable** — one acceptance criterion per commit.
4. **Follow the testing and merge policy above.** Use quick, relevant checks;
   broad test suites and Unity/player verification run only when Bailey requests them.
5. **Update `GAME.md` and `CHANGELOG.md`** in the same commit as the change. `GAME.md` holds current state
   only: edit the single "Where to resume" block in place (never stack a new dated block on top) and keep
   the file under ~250 lines; the changelog entry is one line (~160 chars). Detail goes in the PR, an ADR or
   `docs/testing/<topic>/`.
6. **Commit message:** short imperative subject, then what changed and the
   evidence. Push to `origin` immediately so other tools see it.
7. **Decisions:** a design change adds an entry under `docs/decisions/` named `YYYY-MM-DD-slug.md`
   (date, decision, reason, affected systems, migration impact; ADRs 0001–0252 keep their numbers) and marks any ADR it
   replaces "Superseded by <file>". Do not hand-edit `docs/decisions/README.md`: it is generated (`scripts/index-adrs.py`).
   New ideas go to a backlog, not straight into the active milestone.
8. **Branches:** name them `<tool>/<topic>-<yyyymmdd>` (`claude`, `cursor`, `codex`, `chatgpt`); delete yours once its PR is merged.
   Never delete a branch that has commits not on `main`.
9. **Several tools, several accounts** (fast loop: `python3 scripts/test-quick.py --changed`; routine tasks: `docs/ai/RECIPES.md`): claim the task first (a GitHub issue from the Task packet template, with `Owner:` and your file scope),
   one issue = one branch = one PR, and fill in the PR template. The routine, the rules for generated indexes and a ChatGPT starter prompt are in
   `docs/ai/WORKFLOW.md`.

## Session handoff protocol

Only project state travels between tools — the git repository and the written
docs. The conversation does not. When you hit a session limit on one tool and
continue on another, the new session starts cold and rebuilds context from the
repo. These two checklists keep that reliable.

### Start of session

1. `git pull --rebase origin main`.
2. Read `GAME.md`, starting with its single **"Where to resume"** block (replaced, not stacked,
   each session): latest work, next approved step, what is open or unverified, what to watch for.
3. Skim `CHANGELOG.md` and `git log --oneline -10` for what changed recently.
4. If the handoff block names an unfinished branch, check it out
   (`git checkout <branch>`) instead of starting on `main`.
5. Read the existing validation limits. Do not automatically rerun tests or builds
   at session start; use the testing and merge policy above.

### End of session (before you stop, or before a limit cuts you off)

1. Commit and push the scoped work. Merge completed authorised work through its
   PR into `main` by default. If it is incomplete or has a known new code failure,
   keep it on a named branch/draft with the remaining work recorded.
2. `git push origin HEAD` — unpushed work is invisible to the next tool.
3. **Replace** the **"Where to resume"** block in `GAME.md` (do not stack a new one): latest work, next
   approved step, what is open/unverified, anything to watch, any open question for Bailey. Move anything
   no longer current to `docs/history/`. Commit and push that too.
4. Update `CHANGELOG.md` under "Unreleased" if behaviour changed.
5. If you made a design decision, add a dated record under `docs/decisions/`.

If you are being cut off mid-task with no clean stopping point: commit the WIP to
a branch with message `WIP: <what you were doing>`, push, and write the state
into the handoff block. A messy branch that is pushed beats tidy work that is lost.

## Tool roles (flexible, but one owner per change)

| Participant | Primary role |
|---|---|
| Bailey   | Product owner, final design decisions, playtesting, release authority |
| ChatGPT  | Product design, system specs, planning, cross-system review |
| Cursor   | Focused implementation and interactive work inside the codebase |
| Codex    | Repository inspection, scoped implementation, builds, tests, debugging |
| Claude   | Independent architecture review, large-context review, second opinions |

## Art and asset workflow

- Read `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` before generating, importing,
  modelling or integrating any visual asset. It is the canonical style and path
  contract for ChatGPT, Claude, Cursor and Codex.
- Generate and approve the reference batch before broad production. Use the exact
  asset IDs, filenames, versions and folders in its manifest; do not invent a
  parallel asset tree or silently overwrite an approved candidate.
- Generated reference/source images belong under `docs/art/`. Approved
  runtime-ready assets belong under
  `game/Airside/Assets/Airside/Art/` and ship with Unity `.meta` files.
- Record generator/source, prompt evidence, licence/terms, cost, attribution and
  fallback in `docs/data/ASSET_AND_DATA_REGISTER.md` in the same commit that
  introduces an asset.
- Do not bake interface text into runtime images. Do not use a 2D concept image as
  a substitute for a required 3D aircraft, building or vehicle.
- Simulation controls state and timing. Animation, VFX and audio represent that
  state but never decide resource reservations, task completion or persistence.
- Keep the procedural primitive presentation as a fallback until each replacement
  is integrated and verified at overview/follow cameras and day/dusk/night.

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
