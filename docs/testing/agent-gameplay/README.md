# Agent gameplay runner

[Verified real Mac evidence](DIAGNOSTIC_EVIDENCE_2026-10-09.md): remote success/failure,
upright frames, source identity and remaining limits.

Hidden QA automation for Codex/agents. Agents decide when checks are necessary and
run them automatically under AGENTS.md; further permission is not needed. No
player menu or control server. Explicit launch flags gate it; this is a workflow
restriction, not authentication of an AI identity. Ordinary launch behaviour is
unchanged. Do not make this a routine merge gate or add full runs to CI.

## Report an issue: real diagnosis, including Linux chats

```sh
python3 scripts/diagnose-game.py --issue "clouds look flat in the rain"
python3 scripts/diagnose-game.py --issue "Dash 8 wing join looks detached" --aircraft-type DH8D
python3 scripts/diagnose-game.py --issue "my precise reproduction" --scenario work/repro.json
```

On Linux, the command automatically dispatches the private Mac workflow and returns
its actual artifacts; `--remote` also works from Mac. It tests the exact committed,
pushed SHA. On Mac it builds/reuses that revision and executes locally. A missing
current build is handled automatically for this issue workflow. The direct smoke
runner remains a lower-level tool.

An issue generates related starting probes, not an AI verdict. Unknown/ambiguous
issues require the agent to provide a custom scenario rather than run unrelated
smoke checks. Custom protocol-1 plans can sequence existing feature actions plus
`snapshot`, `time` (HH:mm) and `camera` (pitch,yaw,distance); optional `aircraftType`
selects/adds a subject, `expectState` checks its state after settling. Each step has
safe unique `id`, action, value, capture bool and 0.1–15 s settleSeconds. Camera ranges:
5–85° pitch and 10–10,000 m distance. No arbitrary command/script action is allowed.

The agent must inspect PNGs and the returned state/errors, identify the cause, then
rerun the same JSON scenario on the fixed SHA and compare. `diagnostic.json` status
`captured` means evidence is ready, not that the reported problem is absent/fixed.
Reports include all fleet states, subject, camera/weather/workspace, runtime errors
and observed frame timings. Failed actions/state checks also attempt a real-frame capture
and retain the failed subject/state; a crashed/unresponsive renderer can still block
capture. Timings include host contention/capture overhead.

The Mac bridge uses authenticated private GitHub workflow_dispatch and the registered
`airside-mac-diagnostics` runner. Only that manual workflow targets it; normal CI stays
on Linux. Its dedicated checkout avoids this working tree/active Unity project; jobs
are serial and use disposable saves. Evidence uploads on failure too. It requires
an awake, logged-in, connected Mac. Offline/licensing failures must be reported as
blocked; never replace real evidence with source-only visual approval.

Provisioning: `python3 scripts/setup-mac-diagnostic-runner.py --install-service`.
Installed under `~/Developer/Airside-DiagnosticsRunner` because launchd services in
Documents are blocked by macOS privacy controls. Stop/remove with that folder's
`svc.sh stop` / `svc.sh uninstall`; repository registration can be removed in Actions
runner settings. Registration credentials remain local, private and uncommitted.

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
