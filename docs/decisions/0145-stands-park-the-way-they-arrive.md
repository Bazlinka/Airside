# 0145 — Stands park the way they arrive

Date: 28 September 2026. Author: Claude, at Bailey's request ("aircraft don't sit at the gate or parks
facing the right way").

## Context

A top-down render of every stand (`docs/testing/ground-2026-09-28/stands-before.png`) showed:

- **Terminal gates** point at T1, as they should (ADR 0141).
- **Walk-out bays 10A and 10C** had a parked heading 9–11° off the lead-in they arrive on, so the
  aircraft turned on the spot after stopping.
- **Every bay** read its heading from the baked layout, not from the lead-in it is driven along.

## Decision

- `AdelaideGround.ParkedHeadingDegrees(bay)` sets a bay's parked heading to the direction of its
  lead-in's last metres, so the aircraft stops the way it is travelling.
- On the walk-out stands (10A–10D, 2A) the aircraft also leaves along a painted line. There, the
  heading splits the difference between the arrival and departure lines, which halves both turns.
- `StandPose` uses it for every bay. Gates keep ADR 0141's squared headings.
- **Not changed.** The walk-out stands' painted U-turn lines are what the AIP stand markings show,
  so they stay. Leaving every other stand is now a tug push (ADR 0146).

## Verification

The type-check is clean. Bailey asked to skip new tests this round.

Mac checks:

- aircraft stop on 10A and 10C without turning on the spot;
- every regional bay parks along its painted line.
