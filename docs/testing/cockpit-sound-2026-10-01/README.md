# Cockpit sound and controls — 2026-10-01

First pass PR #517 merged by explicit user instruction. This follow-up is on
`feature/cockpit-sound-and-controls`, based on merged main `dd9c6730`.

Focused headless cloud envelope/airflow/aircraft mix checks: 16 passed, zero failed
(`focused.trx`). The waveform is deterministic, finite, bounded, has a normal
filtered-noise wrap and useful quiet speed levels. These checks do not establish
perceptual audio quality. Generated harness includes the airflow model and tests.

Full headless regression: 1,426 passed, zero failed, 3 m 1 s (`domain-result.txt`).
Whitespace check passes. Asset audit repeats only the inherited metadata issues.

Native Unity is unavailable on this Linux worker. The added native emitter test
checks Doppler off inside and restored outside; it has not run here. Unity compile,
keyboard/mouse/trackpad ownership and easing, saved vibration option, muted/rebound
source lifecycle, actual cockpit listening and frame-time acceptance remain open.
Existing metadata audit issues are unchanged; all three new scripts have .meta.

Use the continued-work acceptance packet in `docs/plans/cockpit-immersion.md`.
Verify no terminal PA/airport bed in cockpit; enter during a playing chime and
repeat entry/mute/unmute/rebind/exit. Keep engine voices audible over the new bed
through taxi/takeoff/cruise/landing/reverse. Check exterior sound resumes normally.

GitHub access restored on 5 October. Both follow-ups are included in the
authorised combined PR #521 integration. Updated evidence and remaining native
acceptance limits: `docs/testing/cockpit-recovery-2026-10-05/README.md`.
