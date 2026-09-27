# 0137 — Pushback times that agree everywhere

Date: 27 September 2026. Author: Claude, at Bailey's request ("aircraft still seeming not to have
correct pushback times").

## Context

Several separate faults made pushback times look wrong.

- **A delay counted twice.** An AI flight's delay was added to its pushback time and also kept as
  "Delayed +N". The board printed the late time next to "Delayed +N", so a 20-minute delay read as
  40.
- **TIME moved after pushback.** Once a departure left its stand, the board's TIME followed each
  phase's start (taxi, takeoff, climb), so the row drifted down the board.
- **Status lines used the starter base's prep time.** The board, the aircraft tag, the Ops summary
  and the engine-start doors all timed prep as if the base had never been upgraded. The simulation
  used the real base level, so after an upgrade the two disagreed.
- **The planner pushed flights later.** Reopening the planner on a booked flight clamped its time
  up to the full prep lead, even when prep was already under way.
- **Odd seconds.** Player bookings kept their seconds, so 10:05:40 printed as 10:05 but was
  "LATE +1" at 10:06:30.
- **No estimate for departures.** Only arrivals showed EST.
- **Midnight rollover.** The board sorted by the printed HH:mm text, so a 00:10 flight sorted before
  a 23:50 one.

## Decision

- **Published time (`ScheduledDeparture.PublishedAt`).**
  - A departure keeps the timetable slot it was published at. `DepartAt` is when it will really
    push back.
  - `DelayMinutes` is worked out from the two times, so it can't disagree with them.
  - AI bookings publish the banked slot and add the drawn delay on top.
  - A delay that would run into the curfew becomes the next morning's flight rather than a
    9-hour delay.
  - No save change was needed. The save already keeps the pushback time and the delay minutes, and
    the published time is rebuilt from them on load.
- **The board's TIME never moves.**
  - A departure shows its published time from booking to climb-out. `FleetAircraft` remembers
    `PublishedDepartureAt` and `PushedBackAt` at pushback. These are not saved; after a reload
    mid-taxi the board falls back to the phase start.
  - Departed history rows show the pushback (the TaxiOut event), not the takeoff.
  - Board rows sort, and are marked as past, by game seconds, not by the printed text.
- **EST for departures (`FlightBoard.EstimatedDeparture`).** EST appears when it differs from the
  published time by a minute or more. It is one of:
  - the delayed time;
  - when the player's prep will finish;
  - while held past its time, the next minute;
  - after pushback, when the flight actually left.
  It is never the destination ETA.
- **One prep time everywhere.**
  - `FleetAircraft.BaseLevel` reads the player's base from the owning operations.
  - The `DeparturePrep.For/IsReady` overloads without a base level now use it, so every status
    line agrees with the simulation.
  - The planner's minimum lead comes from the base too.
- **The planner keeps a booked flight's time.** `FlightPlanner.ClampDelay(seconds, aircraft, now)`
  lets a flight already booked inside the lead keep its time.
- **Whole minutes (`AirlineOperations.WholeMinute`).** Planner bookings, dev auto-schedule and AI
  slots round up to the next whole minute.
  - The simulation still accepts any second, so tests and saves are unchanged.
  - Dev auto-schedule and the soak never book inside the prep lead.
- **Not changed.** `AdelaideDayPlan` is a representative timetable of synthetic flight numbers, and
  no screen shows it. Its disruption key stays as it is. The live fleet's key is now named
  `AirlineOperations.DisruptionKey`.

## Verification

`PushbackTimeTests` covers:

- a delay is counted once, and old saves recover the published time;
- a delayed departure shows the published time with the new time under EST;
- TIME stays put through taxi, takeoff and climb;
- a late pushback shows under EST;
- a held departure's estimate keeps up with the clock;
- prep agrees across the simulation, the board and the Ops summary at the Jet-gate base;
- reopening the planner never makes a flight later;
- whole-minute rounding;
- a 36-hour AI run where every delay equals the published-to-pushback gap.

The headless suite passes and the type-check is clean.
