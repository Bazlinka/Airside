# Cockpit immersion — 2026-10-01

Candidate branch `feature/cockpit-immersion`, based on main `2d97ab24`.

- Focused .NET 8 NUnit cloud envelope: 7 passed, 0 failed (`cloud-tests.trx`).
- Generated headless harness includes the new pure model and its tests.
- `git diff --check`: clean.
- Native `scripts/test-unity.sh`: unavailable; no Unity editor at the configured
  macOS path. No Unity compile, player capture, input, listening or performance
  result is claimed.
- Asset audit reports inherited missing `Assets/Resources.meta` and orphan metadata
  for Animation/Aircraft, Vehicles and World directories. This change adds two
  C# files with metadata and no external assets.

Playtest the task packet in `docs/plans/cockpit-immersion.md`: SF34, ATR42, DH8D
and representative narrow/wide jets, entry/exit, menu ownership, wheel/trackpad,
all five viewpoints, zoom extremes, vibration off/on, touchdown/reverse sound,
floating-origin travel, clear/cloudy/rain/storm ascents and descents, day/night.
The 900–1600 m deck is stylised, not a surveyed cloud-base/top model. The exterior
weather palette/simulation remain authoritative; clear sky above is a cockpit
background/horizon presentation blend. Model-specific recorded interior voices,
interactive avionics and head translation remain outside this candidate.
