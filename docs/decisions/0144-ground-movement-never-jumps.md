# 0144 — Ground movement that never jumps

Date: 28 September 2026. Author: Claude, at Bailey's request ("some planes when multiple are due for
taxi freeze and teleport to runway when waiting").

## Context

Three faults made aircraft freeze and then jump:

- **Hold-ups banked time.** A taxiing aircraft was drawn at its simulated leg time. While it
  waited behind traffic (`BrakedSeconds`), it was drawn stopped at a queue point, but its leg
  time kept running. When the queue ahead moved, the stop point stepped forward and the aircraft
  appeared far down the taxiway. Bailey saw this most just off the stand.
- **Long queue moves snapped.** `QueueShuffle` eased queue moves only up to 150 m, and anything
  further snapped.
- **The tower and the drawn queue disagreed.** The tower clears the longest-waiting departure per
  runway strip, but the drawn queue was ordered per runway end. The aircraft cleared could be one
  drawn at the back of the queue, and its lineup starts at the holding point, so it jumped to the
  runway.

## Decision

- **Ground chains (`AirsidePrototype.GroundFlow`, presentation only).** Each aircraft drives one
  continuous chain of legs:
  - out: taxi-out (with its pushback), then holding short, then lineup;
  - in: vacate, then awaiting a stand, then taxi-in.

  The simulation's position on that chain is the target: the leg time, a queue place
  (`GroundTraffic.QueuedSeconds`), or taxi-out end plus lineup time. The drawn aircraft keeps its
  own place on the chain:
  - it follows the target exactly while in step;
  - when the target jumps ahead, it speeds up from where it is, gaining 0.25× plan pace per second
    up to 1.6×, and catches up along the taxiway;
  - when the target steps back, it eases to a stop and never reverses.

  Its reported speed follows its pace, so wheels and engine sound match what is seen. This
  replaces `QueueShuffle` and its 150 m snap.
- **Queue order.** `FleetVisual.QueueSlot` orders holding-short aircraft per runway strip, the
  same way the tower picks. The aircraft cleared is always the one drawn at the front.
- **The simulation is unchanged.** Clearances, separation checks and timings are the same. Only
  the drawing is continuous.

## Verification

Bailey asked to skip new tests this round. The type-check is clean.

Mac checks:

- a busy departure bank: aircraft queue, move up and line up with no jumps;
- an aircraft held just off the stand sets off smoothly when released;
- arrivals waiting at the exit roll on to their stand without a jump.
