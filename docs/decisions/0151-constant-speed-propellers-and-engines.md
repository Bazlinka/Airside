# 0151 — Constant-speed propellers, feathering, and engines driven by power

Date: 28 September 2026. Author: Claude, at Bailey's request ("improve propeller animation
drastically for all aircraft as well as engine animation. Realism is important.").

## Context

ADR 0148 fixed the strobing and gave the propellers a real blur disc, but it left the underlying
model wrong in a way that no amount of blur could hide.

- **Shaft speed was being used as the throttle.** A parked engine turned at 420 rpm and takeoff
  power at 1400. A real turboprop does very nearly the opposite: its governor holds the propeller
  at an almost constant speed and the power is carried by **blade pitch**. The game had the one
  variable a player can actually see doing the wrong job.
- **Nothing ever pitched a blade.** The blades are lofted with their twist baked in and were never
  rotated again, so a shut-down aircraft stood at the gate with its propellers in cruise pitch.
  Feathered blades — edged into the airflow — are the single most recognisable thing about a parked
  turboprop, and no aircraft in the game had them.
- **Starts and shutdowns were a ramp.** Speed eased between two numbers at a fixed rate. A real
  start motors on the starter slowly enough to count the blades, lights, accelerates hard, and is
  then caught by the governor. A shutdown coasts down and is braked by the blades feathering.
- **No reverse.** Turboprops go into beta on the landing rollout, blades through flat pitch. A
  landing looked identical to a taxi.
- **Exhaust was a phase flag.** `UpdateEngineHeat` turned a sine-pulsed haze on whenever the phase
  was not `AtStand`, so idle and takeoff looked the same and a start produced nothing at all.
- **Engine audio inferred power from rpm.** Which, under a governor, carries almost no information.
- **Jet fans had an invented rpm** and a flat glass cylinder for a disc, with a symmetrical spool
  that missed the one thing everybody knows about turbofans: how long they take to come up from idle.

## Decision

A new `AirsidePropellerDynamics` holds the model as pure functions; the presentation passes drive it.

- **Constant-speed governor.** Np is a fraction of a governed 1200 rpm: ground idle 63%, taxi 67%,
  cruise 86% (a Q400's reduced setting), approach 92%, takeoff 100%. `AirsideReusableMotion`'s prop
  rpm constants are now derived from these rather than invented, so the spread between taxi and
  takeoff is deliberately small.
- **Power is a separate quantity.** `PowerFractionForPhase` gives shaft power 0..1, and blade pitch,
  exhaust, heat and the engine note all read it. It cannot be recovered from shaft speed.
- **Blade pitch is animated.** Each blade rotates about its own radial axis, by the difference from
  the pitch already lofted into the mesh: feathered at 76° when the engine is stopped, 3° fine for a
  start or a run-down, then coarsening with **both** power and forward speed, and −17° in reverse.
  The radial axis is resolved once per model — from the baked mesh centre for the authored aircraft,
  and from the longest scaled axis of the box for the primitive fallback kit, so every aircraft in
  the game is covered rather than only the three authored turboprops.
- **Pitch follows forward speed, not just power.** A blade meets the air at the sum of its rotation
  and the aircraft's speed, so it needs a bigger angle to keep biting as the aircraft accelerates.
  The blades visibly coarsen through the takeoff roll and are coarsest in the cruise, even though
  cruise uses less power than takeoff. (Keying pitch on power alone got cruise backwards; a test
  caught it.)
- **Staged start and shutdown.** Rate limits differ by stage: 55 rpm/s while the starter motors it,
  340 rpm/s through light-off, 170 rpm/s for governor trims, 75 rpm/s on the run-down and over three
  times that for the last of it, because feathering the blades brakes the propeller.
- **Reverse on the rollout.** Selected at touchdown, held while the speed comes off, stowed before
  the runway exit.
- **The disc fades edge-on.** A propeller disc seen edge-on has almost nothing in the line of sight
  and all but disappears; face-on it is the whole propeller. Coarse blades also thicken it. Both are
  what makes takeoff read differently from taxi, and the edge-on case removes most of the disc's
  transparent fill — which the open render-performance work wants, not more of.
- **A stopped propeller drifts.** Six rpm on the breeze rather than a frozen model.
- **Exhaust follows power.** The plume grows and brightens with power, blooms once at light-off — the
  puff every turbine makes when it starts — and cools to a dull haze at idle. Each stack follows its
  own engine through the start sequence.
- **Turbofans use N1.** Idle 21%, taxi 26%, approach 45%, takeoff 95%, reverse 72%. Spool lag is a
  function of where the spool starts from: slow out of the idle range, quick at high power, slower
  still coming back. Fan blur is judged by what a frame can draw, as the propellers already were,
  and a parked fan windmills rather than standing still.
- **Audio reads power directly.**

## Verification

Unity EditMode **1249/1254**. The four failures are present unchanged on clean `main` at `a98d0359`
(confirmed by a baseline worktree run): `FlightPlanning_UsesEachTypesOwnCruiseAndPracticalRange`,
both `Gate13_*` tests and `BusyDay_NoAircraftDriveThroughEachOther`. They are gate/stand and ground
separation failures unrelated to this change and are open work.

Eight new EditMode tests in `PropellerDynamicsTests` cover feathering when shut down, the start
sequence, the governor holding speed while pitch carries power, reverse on the rollout, the disc
fades, windmilling, jet spool lag and the governor hunt bound.

Still to do: a human playtest of a start, a taxi, a takeoff roll and a landing rollout at overview
and follow cameras, day and night.
