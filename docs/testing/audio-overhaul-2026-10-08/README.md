# Aircraft and soundscape overhaul — 8 October 2026

Task #614, cloud Agent 2. Runtime bank selection is v02. 68 new clips (~23 MiB PCM):
14 family signatures × four beds (idle/exhaust/rotating core/reverse), three starters,
three cabins and APU/airflow/gear/flap/door/apron. Registered CC0 family recordings
plus original sound design; new CC0 richwise cabin source acquired and retained.

## Relevant bounded checks

- `python3 scripts/audio/generate_audio_overhaul.py --check`: **68/68 byte-identical**;
  finite waveform, levels/headroom and circular join versus ordinary adjacent-step
  checks pass. Maximum waveform peak 0.533247, levels −25 to −18 dBFS RMS, maximum
  loop endpoint step 0.000004. PCM quantisation can contribute one final LSB.
- `python3 scripts/audio/check_audio_overhaul.py --dotnet <preinstalled-dotnet>`:
  **67 package-free numeric assertions passed**. This compiles pure Domain/Simulation
  dependencies and the changed pure audio math, waveform synthesis and limiter;
  checks each type's loaded/reverse/cold mix, starter/shutdown distinction, core
  sweep, insulation/airflow, deterministic/tail-faded UI waveforms and click attack,
  linked stereo ceiling under 3.5× overload, nonfinite input handling and recovery.
  Source runner: `PureAudioCheck.cs`. This does **not** compile Unity components.
- C#9 Roslyn syntax parsing of changed source/test files: **15 files, zero errors**.
- `git diff --check`: passed.
- Unity metadata generated for all 68 clips and three new C# files. Asset audit
  has one existing failure: byte mismatch between source/runtime
  `Textures/Environment/tx_adelaide_sentinel2_l2a_v02.jpg`. The audio changes do not
  modify that imagery. No missing/duplicate new audio GUID reported.
- Harness source/test list serialized with the existing generator, preserving
  its exclusion list and adding `AudioPeakLimiter`/tests. Its compiler convergence
  pass could not run: NuGet packages unavailable behind proxy 503. Package-free
  check compiles the newly included source. Native tests remain unexecuted.

## Runtime changes worth assessing on the Mac

The default real-metre Adelaide release now enables the local terminal/apron
soundscape even when the legacy BareWorld flag is true. Its source position is
computed from mapped TerminalGates, not the miniature reflection probe; apron
range spans 2.4 km while the PA remains local. Compact QA retains its own origin.

Listen to Saab/ATR/Dash 8, a classic 737/A320, a geared/neo jet, a widebody and Bell:
independent starts, taxi/governor, takeoff core/exhaust, cruise, approach, touchdown,
reverse taper and shutdown. Compare cockpit/window/exterior from the same aircraft;
verify pack/airflow instead of apron/Adelaide waves, stereo engine separation,
filtered rain near the surface and camera-switch/mute cleanup. Follow a return
regional takeoff and Hobart landing; check gear/tyre/reverse cues match those poses.
Network fleet flights now use the same engine and interior mix. Check nearby
terminal PA placement, busy six-aircraft scenes, celebration ducking and device
changes, and verify no CPU/audio-thread underruns from the 64-real-voice budget.

Unity compile, native NUnit audio tests, actual headphone/speaker listening, runtime
filter order/limiter invocation, device switching, memory/voice/performance and
packaged flight journeys are **unverified**. No full test suite, Unity editor,
packaged build, rendered review or gameplay soak was run under Bailey's standing
quick-check policy. Numerical fidelity does not prove perceived audio quality.

Licences/cost/fallback: `docs/data/ASSET_AND_DATA_REGISTER.md` AUD-014–016,
`docs/data/audio/audio_overhaul_sources.json`; output hashes/levels:
`docs/data/audio/audio_overhaul_manifest_v02.json`.
