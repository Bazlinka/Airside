# Recipes — the routine tasks, step by step

Short, checked against how this repo actually works (Unity 6.3 project in `game/Airside`). Each ends with what to run. If a recipe is wrong, fix it here in the same PR.

## Fast loop first
Follow the root `AGENTS.md` testing and merge policy. Use only quick relevant checks
by default; `python3 scripts/test-quick.py --changed` is optional when useful. Agents
automatically choose necessary gameplay/native checks and build once if runtime
evidence needs a current player. Select relevant features first; full journeys only
when lifecycle risk warrants them. Broad suites/performance/long soaks remain on request.
Report unverified Unity behaviour. Merge completed authorised work without asking again.

## Add or change a C# file
1. Put it in the layer that fits: `Domain` / `Simulation` (no `UnityEngine`), `Presentation` (may use it), `Editor`, `Tests/EditMode` (see `game/Airside/AGENTS.md`).
2. New file → `python3 scripts/new-meta.py <path>` (every file and folder under `Assets/` needs a `.meta`).
3. If it is pure (no `UnityEngine`) or is a test: `python3 scripts/update-harness.py` so the headless run compiles it (CI fails if the list is stale).
4. A new `AirsidePrototype.*.cs` partial also needs its line in `scripts/map-presentation.py` (`OWNERS`), then `python3 scripts/map-presentation.py`.
5. Use a quick syntax/compile check if available. Add a regression only when useful for the bug/change; do not require a new test or full suite for every edit.

## Add a runtime art or data file
1. Put it under `game/Airside/Assets/Airside/Art/…`, then `python3 scripts/new-meta.py <path>`.
2. Mirror it into `StreamingAssets` with `bash scripts/sync-art-streaming-assets.sh`, and `new-meta.py` the copy.
3. Record source, licence, attribution, cost and fallback in `docs/data/ASSET_AND_DATA_REGISTER.md` in the same commit (no asset without a recorded licence).
4. `python3 scripts/audit-unity-assets.py` must pass. If a generator makes the file, give the script a `--check` and run it.

## Add a field to the save
Bump `AirlineSaveData.CurrentVersion`, add a migration for older versions, and handle `JsonUtility`'s blank record: an absent nested object comes back
all-default, so restore must treat blank as "none". The headless suite cannot see this: add a test that runs in Unity and say so.

## Record a design decision
New file `docs/decisions/YYYY-MM-DD-slug.md` (title, `Status:`, date, decision, reason, affected systems, migration impact; mark any ADR it replaces
`Superseded by <file>`). Do not edit the index by hand: `python3 scripts/index-adrs.py`.

## Add a script
Give it a one-line docstring (Python) or first comment (shell); `python3 scripts/index-scripts.py` refreshes `scripts/README.md`.

## Open a PR
Claim the task first (`docs/ai/WORKFLOW.md`), branch `<tool>/<topic>-<yyyymmdd>`, `git pull --rebase origin main`, quick checks under the root policy, fill in the PR template
honestly (Verification / Not verified), replace the single "Where to resume" block in `GAME.md` if state changed, add one line to `CHANGELOG.md`.

## Look something up without burning context
Maps first: `docs/README.md` (docs), `docs/architecture/PRESENTATION_MAP.md` (the 39 `AirsidePrototype` files), `scripts/README.md` (scripts),
`docs/decisions/README.md` (ADRs). Do not open `docs/history/*-through-2026-10-07.md` (500 KB archives) unless asked.
