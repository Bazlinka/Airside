# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- Second aircraft (`GroundTrafficAircraft`, `GT-201`) shuttles along shared taxi
  segments A1/A2 through the same `ReservationTable`. The primary flight has
  priority — the second aircraft yields any segment the flight needs and a hold
  beyond ten seconds is explained by the traffic wait monitor. Reservation
  requirements now use per-tick simulation time so segments are correct during
  offline catch-up. Rendered as a second aircraft on the taxiway with a HUD line.
  23/23 edit-mode tests pass, including a fifty-cycle soak with it active.
  See `docs/decisions/0006-second-aircraft-priority-and-yield.md`.
- Repo set up for shared work: added `.gitattributes` (Unity merge/binary rules),
  expanded `AGENTS.md` into the shared contract, added `CLAUDE.md` and Cursor
  rules, this changelog, and `LICENSES.md`. Removed a stale duplicate
  `AirsidePrototype 2.cs`. Pushed to a private GitHub `origin`.
- Segment clearance and traffic wait diagnostics: taxiing aircraft reserve only
  the segment they occupy and release it before moving on; a monitor explains any
  aircraft blocked on one resource for ten seconds or more
  (`docs/decisions/0005-segment-clearance-and-wait-diagnostics.md`).

## 2026-09-06

- Add named taxi routes and operations history.
- Add persistent saves and offline catch-up.
- Add operational turnaround and economy loop.
- Complete deterministic movement milestone.
- Open the Airside prototype by default.
- Add first playable Airside prototype.
- Target current Unity 6.3 LTS patch.
- Create Airside project foundation.
