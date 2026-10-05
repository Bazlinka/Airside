# Passenger/exterior integration — 6 October 2026 (Adelaide)

Bailey authorised merging #518. Integrate main `7194c18f` into the existing
passenger/exterior branch; resolve camera, HUD, audio and documentation conflicts.

Preserve current main's cockpit motion, touchdown/reverse effects, callouts,
cloud breakout, airflow, saved vibration and keyboard/zoom/invert controls.
Passenger/exterior look keeps its wider limits; yaw eases across the wrap along
the short path; exterior wheel and keyboard zoom adjust eased orbit distance.
Cockpit presets apply in cockpit only. Home/1 recenter every mode. Exterior pose
ignores cockpit motion/rumble. All four modes preserve watched registration and
regional terrain. HUD retains view buttons and current route/speed/V/S/distance;
exterior telemetry updates rather than freezing the last interior sample.
Interior filtering applies only to the watched aircraft in an interior mode.
Airflow stops/releases on mode changes and is absent in exterior mode. Crew
callout captions appear in cockpit only. No simulation or save schema changes.

Original branch evidence: focused 35 native/35 headless and 52 rendered stills,
retained in `docs/testing/passenger-flight-views-2026-10-01/`. Those checks predate
this integration and are not claimed as native verification of combined source.
The added native regression exercises exterior immunity to head motion/rumble
and recenter/optic preservation. It has not run here: `scripts/test-unity.sh`
reports the Mac Unity editor absent. Combined native compile and packaged input,
seat/view switching, listening and performance review remain outstanding.
Asset audit reports only inherited Resources/empty Animation folder metadata issues.

Final combined-source focused headless checks: 122 passed, zero failed (`focused.trx`),
covering passenger profile/world, cockpit/audio/atmosphere. Roslyn parsed the four
conflict-sensitive camera/HUD/audio/test files with zero syntax errors
(`syntax-result.txt`); this is not a Unity semantic compile. Whitespace passes.
Original retained native XML was inspected: 35 passed, zero failed/inconclusive.
That native result predates this integration.

Full combined-source headless regression: 1,477 passed, zero failed
(`domain-result.txt`).
