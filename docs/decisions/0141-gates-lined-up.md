# 0141 — Terminal gates lined up along T1

Date: 27 September 2026. Author: Claude, at Bailey's request ("inside aircraft gate boxes are lined
up and not too far out on the apron").

## Context

- **Ragged stops.** The terminal gate stops came from OpenStreetMap (ADR 0047) and sat anywhere
  from 10 m to 38 m off the T1 wall. Gates 12L, 22L, 25, 26L, 27 and 29 were 30 m or more out.
  Parked jets therefore made a ragged row half out on the apron.
- **Tilted headings.** Headings wandered up to 3°, and the bus stands 20R/22R by 7°.
- **One box size.** Every gate painted a 30 × 44 m box whatever its code, so the boxes of paired
  L/R lines (16L/16R and others) overlapped.

## Decision

- **`Simulation/AdelaideGateAlignment`** builds the gates everything else uses. `AdelaideGround`,
  the aerobridges, the paint and the lights all read `AdelaideGateAlignment.Gates`. The generated
  layout file is unchanged.
  - **The wall.** It is read from the T1 footprint's own apron-side vertices (z 437.7 → 435.8)
    and clamped at both ends, so gates 27–29, west of the building, share its line.
  - **Stops.** Each contact gate's nose stop moves along its own lead-in to a common setback: code C
    11 m and code E 14 m. Code E means the MARS centre lines 18, 20, 22L, 25, 26L and 28L. The
    list is kept in step with `AirlineOperations.CodeEGates` by a test, and is held locally so
    building the gates never waits on that class's static fields. Rows of noses now differ by
    under 5 m, instead of 28 m.
  - **Headings** are squared to the wall, within 0.25°.
  - **Remote stands.** The bus stands 20R and 22R, more than 60 m out, keep their place and are
    squared up.
  - **Paths.** The last 40 m of each taxi-in and the start of each pushback are rebuilt: truncated
    or extended along the lead-in, then eased onto the stand centre line with a smoothstep, so the
    final approach is straight and square with no kink. Taxi-outs and runway routes are untouched.
  - The simulation's stand rules (pier pairs, code letters, wingtip checks) use stand ids, not
    coordinates, so they are unchanged.
- **Paint (`AdelaideStandMarkings`).**
  - Code E gates get a 42 × 72 m box and code C a 30 × 44 m box. 42 m is as wide as gates 25 and
    26L, 42.8 m apart, allow.
  - A box never extends past halfway to the next gate on the row. For example, 15 and 16R are only
    25.5 m apart.
  - The contact pier pairs 16L/R, 18/18R and 28L/R paint one shared box with both stop bars, like a
    real MARS stand.
  - Remote partners keep their own box, because sharing would swallow the next gate's.
- **Aerobridges** follow automatically, since they measure from the stop. Every contact gate
  that had a bridge still has one.

## Verification

- `GateAlignmentTests` covers:
  - each contact gate at its code's setback and square to the wall, all in one row;
  - the remote stands unmoved but squared;
  - the code E list matching operations;
  - taxi-in and pushback meeting the new stop with no jumps and a square final approach;
  - the taxi-in leg ending on the stand pose for a 737 and an A350;
  - painted boxes never crossing except on one pier, shared boxes for 16, 18 and 28, and a
    widebody box on 25.
- The stand-marking test now reads the aligned gate. The headless suite passes and the
  type-check is clean.
- `GroundSeparationTests` needs a Unity presentation class, so it runs only on the Mac. Mac
  checks:
  - `GroundSeparationTests` and `TerminalGateOperationsTests`;
  - parked jets in one row with bridges docking;
  - taxi-in and pushback at 12L, 22L, 25 and 26L, the biggest moves;
  - the shared boxes.
