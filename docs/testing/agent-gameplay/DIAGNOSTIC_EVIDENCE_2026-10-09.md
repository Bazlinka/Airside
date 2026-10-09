# Real Mac diagnostic evidence — 9 October 2026

Exact game/tool revision: `80f8998d53b994eed033b155c2ccb4b7f234d5b4`. Native Unity 6000.3.23f1 / Apple Silicon / Metal.

## Positive issue reproduction

[Remote weather run](https://github.com/Bazlinka/Airside/actions/runs/37868658513): authenticated manual dispatch → exact-revision checkout → build/reuse → real player → downloaded artifact. Ten steps passed in 117.90 seconds; seven complete 1280×800 PNGs; clean matching build; zero recorded runtime errors. All seven frames inspected: upright HUD, visible sky/horizon, distinct rain/storm/fog and night views. This verifies the diagnostic path, not approval of cloud appearance. Clear/cloudy look similar here; rain clouds include flattened oval silhouettes and storm/fog dark clumps. Those observations are evidence for a separate weather investigation, not a fixed-weather claim.

## Genuine failure evidence

[Intentional failed-state run](https://github.com/Bazlinka/Airside/actions/runs/37868949499): DH8D follow → close camera → require `Flying` while parked. Runtime reports `Expected Flying; observed AtStand`, records the failed step and state, captures `intentional-state-mismatch.png` even though capture was false, and exits 2. Launcher/diagnostic/workflow retain failure; artifact includes report, player log and three real PNGs. Failed-step frame inspected: upright and visibly parked. Expected failure verifies detection, not a game regression.

## Defects found and corrected during verification

- Unity-generated package/project/SSAO-prefilter metadata left a clean build checkout dirty. Restore only the known build-generated files after a clean preflight, including failed builds.
- Checking out a revision after Python started executed old diagnostic code against new source. Workflow now checks out the requested SHA before loading Python; mismatches fail explicitly.
- Metal asynchronous readback PNGs were vertically inverted. Swap managed RGBA rows on the existing background worker; native before/after frames prove upright output. Other rendering backends unchanged/unverified.
- The first 28° weather camera excluded the sky. The corrected 8° / 2400 m camera includes clouds and horizon.
- GitHub concurrency groups replace older pending requests. Remove the group; the single dedicated runner executes queued jobs serially.

## Limits and CI

Mac must be awake, logged in and online. Runner service is under `~/Developer/Airside-DiagnosticsRunner`; personal saves are isolated. Offline, build, capture or state failures remain failures/blockers. Agents still inspect evidence, diagnose the reported cause, fix and retest the same scenario. No automatic visual verdict. Input hit testing, title Continue and an isolated performance benchmark were not covered.

Observed frame p95 was 100.03 ms and worst frame 8965.64 ms (1785 samples). Capture/initial shader work and another Unity job were active: these are non-isolated observations, not a performance pass.

The broad CI result has 11 pre-existing failures: compared all failed test names from [preceding main PR #708](https://github.com/Bazlinka/Airside/actions/runs/37866979108) against [diagnostic PR #709](https://github.com/Bazlinka/Airside/actions/runs/37867220116); all 11 are identical. Focused diagnostic checks pass 12 + 5; native C# compile and Mac build succeeded.

Artifacts retain real PNGs/logs/reports for 14 days. Local downloaded copies live under `work/remote-diagnostics/<request-id>/`; evidence status and hashes remain committed here. To reproduce, use `scripts/diagnose-game.py --issue "cloud weather looks flat" --revision <pushed SHA> --remote`.

## Frame SHA-256

| Scenario/frame | SHA-256 |
| --- | --- |
| weather/day-clear.png | `ad67013a35f9556fc6b468db26f06b3528d1027f9bba2a2d2f1683cea6879767` |
| weather/day-cloudy.png | `13f3ca32514784c3f83ba9ebf3f42814d640e78e6281956b6ecdb78b0c2b0924` |
| weather/day-fog.png | `3690cb36e06517372ebf6975c6c018dbdfbd31872d54a82380e8810af5e0f3da` |
| weather/day-rain.png | `e8cfb36c73bcbe9026f29e19ec8c13a3e1362f0aae934d5e222201facd980504` |
| weather/day-storm.png | `2605d786d98c47edae1e1c2c2210018225ce65051ea82dd4ff23c9b1b76dc711` |
| weather/night-rain.png | `e07c36323624db6ab3f70e90971e9a0fe29fbb3fd58de33afec373dabd3a88ac` |
| weather/night.png | `9393805d9ea5b2d3aa233a6377829f5aed91091a274668e49ffc38b8fd491906` |
| failed-state/close-pose.png | `dda5a674987096d7a50b8a793c2a283c3b8dd52c44ec403bb1d56854bf5e8694` |
| failed-state/follow-subject.png | `7a502c0e1e5c4573f580c532aac4b85d4e99bbc21fbf73b5a581b45fd03c8213` |
| failed-state/intentional-state-mismatch.png | `d2011396801e4df93626b3fe0dfadb34349548e8d8903a53b735d8a01a0d52da` |
