# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- Airport **location** + **day/night cycle**. `AirportLocation` (domain) carries
  id/name/region/UTC offset/latitude; ships with Kingscote (default), Port Lincoln
  and Coober Pedy. `DayCycle` derives local time from the sim clock — one
  simulated day per 20 real minutes from an 08:00 start — and drives the sun and
  ambient light and a HUD line (location, day, clock, phase). **Save schema → v2**
  (adds `locationId`); schema-1 saves migrate on load. 37/37 tests; macOS build
  runs. See `docs/decisions/0010-location-and-day-cycle.md`.
- Fair corridor hand-off: when the shared A1/A2 corridor is free and more than one
  fleet aircraft is queued, it goes to the one that has waited longest (fleet
  order breaks ties), instead of fleet order alone. Reuses the traffic monitor's
  wait timestamps — no new state. A forty-cycle soak asserts neither fleet
  aircraft is starved. 30/30 edit-mode tests; macOS build runs.
  See `docs/decisions/0009-fair-corridor-handoff.md`.
- Ground-traffic **fleet**: `AirportSimulation.GroundTraffic` is now a list.
  `GT-201` runs an arrival/stand/departure schedule (parking on whichever stand
  the primary flight is not using); `GT-202` repositions in and out through a
  run-up bay without a stand, starting 25s later. Every fleet aircraft reserves a
  single-file `TAXI-CORRIDOR` lock while on A1/A2, so at most one is on the shared
  taxiway at a time — they queue instead of meeting head-on. The primary flight
  keeps priority and is never blocked (`ReservationConflicts` stays zero).
  Deadlock-free by construction. Presentation renders one model per fleet aircraft
  and lists them in the HUD. 28/28 edit-mode tests; macOS build runs.
  See `docs/decisions/0008-ground-traffic-fleet-and-corridor-lock.md`.
- Earlier the same day: single second aircraft — shared segment reservations
  (`0006`), then an arrival/stand/departure schedule (`0007`).
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
