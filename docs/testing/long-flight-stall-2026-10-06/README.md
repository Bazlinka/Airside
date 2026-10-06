# Recorded frame-stall investigation — 6 October 2026

Base: `6dfa3e8b` (`origin/main`). Branch: `fix/long-flight-stall-20261006`.
Isolated worktree: `/workspace/Airside-stall`.

## Outcome and scope

Remove the synchronous GPU readback, PNG encoding and disk write from both
packaged QA screenshot paths. A pending screenshot now yields frames; a failed
or timed-out capture exits QA with code 2 and cannot report a successful shot or
completed journey. One caller capture stays in flight until its write finishes.
Finished-frame screenshots still include IMGUI/HUD via Unity's built-in
ScreenCapture module, newly enabled in the package manifest and lock file.
No simulation, terrain, schedules, save schema, normal camera or gameplay changes.

## What the evidence establishes

- `docs/testing/sa-flight-world-2026-10-01/RESULTS.md` records a 172,182.1 ms
  frame in an accelerated, screenshot-producing regional review. No stack sample
  or exact per-stage capture timings from that run are committed.
- `docs/testing/cockpit-mode-2026-10-01/game-startup.log:42` records a
  **196,372.1 ms frame with the watched aircraft still AtStand at Adelaide**,
  before the South Australia terrain feature. Regional terrain is therefore not
  a sufficient explanation for both recorded freezes.
- That heartbeat reports 760 GUI calls, **3.67 ms average Update**, **0.07 ms
  HUD**, and **0.64 ms operations / 8 calls**, while only rendering about 253
  frames. A 196-second stall inside a timed Update would alone imply roughly
  776 ms per frame, far above the recorded average. The evidence points outside
  those measured methods. The heartbeat runs before the current Update's stage
  accounting, so this is an inference, not a historical stack trace.
- Both QA capture coroutines previously ran `Texture2D.ReadPixels`, `Apply`,
  `EncodeToPNG` and `File.WriteAllBytes` synchronously after
  `WaitForEndOfFrame`, outside the Update/HUD stage measurements. These force a
  GPU-to-CPU fence and block rendering for encoding/disk I/O. The old cockpit
  log's `local-flight.png` completes shortly after the huge-frame heartbeat.
- Weather and live-traffic fetches already use background tasks and 8-second
  HTTP timeouts. Terrain creates at most one small tile per frame. Simulation
  and arrival-ETA loops were inspected; no demonstrated 172-second offender
  was found there. Autosaves remain synchronous: a possible independent disk
  stall, but not established as this capture-associated historic stall.

## Fix

`ReviewFrameCapture` captures the backbuffer to a temporary render texture,
requests asynchronous RGBA GPU readback, copies the returned managed pixels,
and passes only pixels to a worker. `BackgroundCaptureWrite` performs the
thread-safe array PNG encoder and filesystem write on that worker. No Unity
texture or scene object crosses onto the worker. The render texture survives
until its GPU callback, including after a timeout, then releases exactly once.
The coroutine yields until completion with a 15-second real-time limit; it logs
request, readback and written stages so any future capture hang has attribution.
Unsupported async readback fails explicitly; there is no blocking fallback.
Unity's API documentation explicitly calls
[`ImageConversion.EncodeArrayToPNG`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ImageConversion.EncodeArrayToPNG.html)
thread safe, and recommends
[`ScreenCapture.CaptureScreenshotIntoRenderTexture`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshotIntoRenderTexture.html)
with AsyncGPUReadback to reduce main-thread work after rendering completes.
Both API pages were read during this source-only investigation; they do not
substitute for compiling against this project's Unity version.

## Acceptance and validation

- A deliberately blocked encoder returns control to its caller and executes
  on a different thread; after release, its copied payload reaches disk.
- A missing output directory faults the worker and is observable rather than
  being reported as a successful frame.
- Both callers use the shared asynchronous pipeline; neither retains
  `ReadPixels` or `EncodeToPNG`.
- Harness derivation, full headless suite, C# 9 syntax parsing and asset audit
  are run below. These do not prove native Unity API type resolution, GPU
  driver behaviour, screenshot orientation/colour fidelity or player FPS.

Validation completed: full Unity-free suite **1,724 passed / 0 failed** (3m22s);
focused worker checks **2/2** (34 ms); five C# files parse under C# 9 without
syntax errors; asset audit passes (1,771 GUIDs / 386 mirrors / 70 character
materials); harness derivation and whitespace checks pass. The full runner also
prints the two existing unmet-precondition tests; its summary reports zero skipped.
`validation.txt` records the commands and limits. Soak camera metadata is sampled
when the frame is submitted, rather than after its asynchronous write finishes.

No Unity editor, player, build, native test or full-flight reproduction was run,
as requested. The historic 172-second cause remains unproven; this fixes the
concrete blocking capture path most consistent with the available evidence.

## Rebase validation — current main `3f8d0969`

Rebased onto the merged unified fleet/cockpit changes; only top-of-file
GAME/CHANGELOG handoffs conflicted, with both sets retained. Rebased headless
suite **1,779 passed / 0 failed** (3m21s). Harness derivation is current; the
five capture C# files parse without syntax errors; asset audit passes with
**1,784 GUIDs / 386 mirrors / 70 character materials**; whitespace is clean.
No Unity or journey run. Original capture investigation above remains unchanged.
