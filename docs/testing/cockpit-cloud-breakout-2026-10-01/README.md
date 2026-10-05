# Cockpit cloud-breakout consistency — 2026-10-01

Local branch `feature/cockpit-cloud-breakout`, based on `302df560` (#521).
Bailey explicitly authorised #521 merge and continuation. That merge could not be
performed: CLI returns HTTP 401, GitHub connector returns HTTP 403 with account
suspended, and git cannot authenticate. No new remote merge result is claimed.

Focused .NET 8 cloud-envelope and atmosphere tests: 16 passed, zero failed
(`focused.trx`). New regressions cover dense-cloud breakout endpoints, monotonic
smooth visibility through the top, preservation of clear weather, and partial
coverage for broken cloud. Whitespace check passes. Previous #521 full headless
result remains 1,426 passed; that result predates these three new tests.

Runtime uses the tested cloud-cover envelope for cockpit background, fog and
sun/moon/star visibility. The underlying ground/deck weather remains unchanged.
Native Unity compile and visual review have not run: the Mac Unity editor is absent.
Required review: dense/broken/clear cloud, ascending/descending, day/dusk/night,
entry/exit and above-storm fog/celestial visibility. No new asset or save change.

GitHub access restored on 5 October. Both follow-ups are included in the
authorised combined PR #521 integration. Updated evidence and remaining native
acceptance limits: `docs/testing/cockpit-recovery-2026-10-05/README.md`.
