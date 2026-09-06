# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- Second aircraft (`GroundTrafficAircraft`, `GT-201`) now runs a repeating
  schedule — taxi in on A1, taxi in on A2, park on Stand 2 for a dwell, taxi out,
  depart, gap — as a data-driven leg list, instead of a fixed A1/A2 shuttle. It
  reserves every segment and stand through the same `ReservationTable` and still
  yields the whole airfield to the primary flight, so `ReservationConflicts`
  stays zero. HUD shows its phase and any hold. 24/24 edit-mode tests pass;
  macOS build succeeds. See `docs/decisions/0007-second-aircraft-schedule.md`.
- Second aircraft first introduced: shared taxi-segment reservations with the
  primary flight, priority-and-yield rule, per-tick reservation time so segments
  are correct during offline catch-up.
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
