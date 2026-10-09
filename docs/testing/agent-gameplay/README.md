# Agent gameplay runner

Hidden QA automation for Codex/agents. Agents decide when checks are necessary and
run them automatically under AGENTS.md; further permission is not needed. No
player menu or control server. Explicit launch flags gate it; this is a workflow
restriction, not authentication of an AI identity. Ordinary launch behaviour is
unchanged. Do not make this a routine merge gate or add full runs to CI.

## Automatic selection

Use the smallest relevant selection for changed interactions, cameras, booking,
cancellation or save restoration. A runtime bug that pure/compile checks cannot
resolve also warrants a scenario. For rendered appearance, use a matching native
review/capture and inspect its actual frames; the generic runner does not cover
every aircraft/art feature. Use full journeys for departure/flight/arrival/return
continuity risks or a concrete regression that focused checks cannot resolve.
Build once if needed for those checks. Do not run every profile for each change,
repeat unchanged results, or substitute this runner for performance/long soaks.
Docs-only changes need no gameplay run. Record selected coverage and blockers.

## Commands

```sh
python3 scripts/agent-gameplay.py --plan
python3 scripts/agent-gameplay.py
python3 scripts/agent-gameplay.py --features booking,save,views
python3 scripts/agent-gameplay.py --profile full
```

Default smoke batches 17 steps in one session: Fleet/Operations/Contracts/Stats,
planner, booking/cancellation, disk-save restoration before and after cancellation,
follow, left/right passenger and exterior views, overview restoration and menu.
Each camera/workspace capture is a real finished frame, with state checked before
capture. These are production commands/controllers, not simulated mouse clicks;
input hit-testing, first-time setup and title-screen Continue are not covered.
The menu does not pause the live airline clock. Optional `weather` checks visual
rain/clear overrides with their full 15-second settle, not operational weather.

`full` adds weather and a separate accelerated ADL–KGC round trip using existing
journey hooks (40×, with existing frame-step limits). Departure, outbound, inbound,
landing, round-trip completion and the final overview frame must all be observed.
It does not run every feature or establish 100–150-hour career pacing. Select a
covered regional airport with `--destination`. Use `--timeout` (default 90 seconds)
and `--journey-timeout` (default 600 seconds) to bound each scenario. It may time
out under poor frame pacing; incomplete evidence is failure, never a partial pass.

## Build reuse and isolation

Build once after committing source with `bash scripts/build-mac.sh`, then reuse
that exact build across feature selections. The runner never builds automatically,
launches Unity or installs dependencies. It refuses missing, unstamped, stale and
dirty builds and uncommitted game/scripts changes. `--plan` writes nothing and
needs no build. When runtime evidence is necessary and the build is stale/missing,
the agent builds once automatically, then reruns the selected scenario. The Python
runner itself keeps build and execution separate. The runtime also verifies the requested clean commit before a
feature scenario starts. Full journey reuses the same preflighted executable.

Each run has a fresh directory under `work/agent-gameplay/<timestamp>-<id>/` with
separate feature/journey saves, logs and PNGs. Neither personal career saves nor
settings are changed by the scenario commands. Soak auto-dispatch and menu resetting
are suspended for the feature session; real simulation/background traffic remain.
Full journey retains existing soak dispatch rules and isolated save storage.

On timeout/interruption, stop only the process group this runner launched; never
kill an existing Airside or Unity session. A Mac display wake assertion keeps
finished-frame capture progressing. No external dependencies or network required.

## Results

`summary.json` records verdict, identity, features, elapsed time and scenario
completion. `features/gameplay-report.json` records each action, result, live
simulation timestamp and screenshot. Missing report/steps, wrong identity/save,
invalid or missing PNG, nonzero exit, known runtime exception and timeout fail the
run. Earlier passes remain visible when a later scenario fails.

A command pass says the checked state and capture completed. An agent must still
open the PNGs to judge appearance. Accelerated journeys never establish FPS,
real-time animation pacing or performance. Report those separately. No coverage
claim is made for unselected features or live services. Existing native review
scripts remain available for aircraft/art/lighting/audio-specific investigations.

## Implementation validation — 9 October 2026

Nine focused runner regressions pass: stale/dirty build refusal, incomplete or
mismatched step refusal, private-save check, missing/invalid frame check, incomplete
journey check, owned-process timeout cleanup, exit/runtime-error refusal, and
booked/cancelled save sequencing. Current Domain/Simulation/Presentation sources
compile using Unity 6.3.23's Roslyn compiler and cached Unity references; one existing
unreachable-code warning in AirsideAdelaideSurroundings. This is a compile check,
not a Unity import or player run. No packaged build/full suite/native gameplay run
was performed. First use requires a fresh stamped build containing this hook.
