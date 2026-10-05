# GitHub recovery and cockpit integration — 5 October 2026

**Current acceptance (6 October 2026): all existing playtests complete; Bailey is
happy with the game.** See [owner sign-off](../playtest-acceptance-2026-10-06.md). Pending manual acceptance
below is historical and superseded. Recorded agent-run evidence remains unchanged.

GitHub API and git access are restored. Bailey's previous explicit merge approval
for #521 remains in effect. Integrate main `84b30159` before merging #521: retain
the shared glance targets, head/body/landing/reverse effects, callouts, attitude,
lighting and wipers. Retain #521 airflow lifecycle, apron/PA isolation, own-aircraft
Doppler removal, saved vibration preference, inverted drag and keyboard zoom/Home.
Use one smoothed view rotation, composed with CockpitMotion; when taking over a
preset with manual input, continue from the displayed gaze. Remove superseded
duplicate vertical-speed telemetry and retain main's gear-height speed source.

Add missing metadata for seven newly merged cockpit scripts/tests so Unity uses
stable cross-machine GUIDs. Asset audit still reports the inherited Resources
folder metadata omission and three orphan empty Animation folder metadata files.
No new external assets or simulation/save changes.

Native Unity test command reports the Mac Unity executable is absent. Native
compilation and packaged visual/input/listening/performance acceptance remain
outstanding; merge approval does not establish passing native evidence.

The original local cloud-breakout patch (`a9c21462`) is also included under
Bailey’s 5 October instruction to merge all. Combined final focused tests cover
cockpit motion/callouts/glances, airflow, cloud breakout, atmosphere and aircraft
mix: 102 passed, zero failed (`combined-focused.trx`). The existing full regression
run started before adding the three cloud-breakout cases; the 102-test focused
run compiled and exercised the final combined source. No broad final count is
inferred from adding the three cases to the earlier full run.

Full integration checkpoint: 1,474 passed, zero failed, 2 m 55 s (`domain-result.txt`).
Final combined focused source: 102/102. Whitespace and generated-harness checks pass.

Merged: PR #521 at `2554fea0722db9b6e962930015090ef6a404e044` on
2026-10-05 23:05 UTC, under Bailey’s explicit merge-all instruction. Local and
remote main were synchronised afterward. GitHub headless CI was queued at merge;
its success is not claimed here. Native acceptance remains outstanding.
