# 0173 — Final approach without the slowdown, a line-up that doesn't swivel, and parked aircraft that look parked

Date: 28 September 2026. Author: Claude, at Bailey's request. Bailey reported:
- propellers turn slowly even when parked;
- the passenger door is always open at the stand;
- taxiing sometimes stutters;
- the turn onto the runway still isn't right;
- planes on final stutter and the whole game slows down.

## Context

**The slowdown on final was the landing estimate.** The drawn final (ADR 0142/0147) placed each
arrival using `AirlineOperations.ExpectedLandingClearance`. It was called every frame for every
inbound aircraft, often two or three times a frame, because nothing was cached for an arrival not
yet drawn. Each call rebuilt the runway queue (lists, sorts). It then ran the tower's ground check:
up to 144 five-second steps, each testing the vacate and taxi-in paths against the whole fleet.

On a busy Adelaide day, measured in Unity EditMode (1,200 samples, 13,817 calls):
- 3.0 ms per call on average;
- 34 ms per sweep of the inbound aircraft;
- 105–114 ms for the worst single call, and 613 ms for the worst sweep.

That cost the whole frame, so taxiing aircraft hitched too, whenever arrivals were inbound. 91% of
the calls were for arrivals still too far out to be drawn. The estimate also shifts slightly each
simulated second, and the old code snapped the drawn distance to it (limited only to 55–160% of
approach speed), so every shift showed as a lurch.

**Line-up.**
- The runway 05 and cross-runway line-up paths left the holding point about 8° off the heading the
  taxi route arrives on, so every departure swivelled on the spot as it was cleared.
- The ground-flow pace (ADR 0144) also ramped up from zero at 0.25 per second as line-up began, on
  top of the plan's own acceleration from rest. That left the drawn aircraft up to 2 s behind
  through the turn. It was still 0.1–1.1 s behind when the take-off roll began, and was then pulled
  onto the runway.

**Parked.**
- A stopped, feathered propeller was given a 6 rpm "breeze drift", and a stopped jet fan 45 rpm, so
  engines never looked off.
- The passenger door opened 90 s after parking and stayed open until just before the next departure,
  which could be overnight. An aircraft that had never flown was open permanently.

## Decision

**Landing estimate** (`AirsidePrototype.ArrivalFinal`, `AirlineOperations`):
- `ExpectedLandingClearance` is split into `ExpectedLandingQueueTime` (the cheap queue part) and
  `LandingGroundClear` (one ground-check step). The whole estimate is unchanged and still built
  from the two, and a test proves the stepwise walk gives the same answer.
- The drawn final keeps one search per arrival. It refreshes the queue time every 5 simulated
  seconds, or at once when the arrival's state changes, and keeps flying on the last estimate
  meanwhile. The ground check runs at most 4 steps per frame across all arrivals.
- An inbound whose queue join, at approach speed, is still beyond the 32 km show distance gets no
  estimate at all.

**Speed along final:** `ArrivalApproach.FlyFinal` eases the speed toward the pace that closes the
gap to the estimate over 25 s. It stays within 55–160% of approach speed and changes by at most 4%
of approach speed per second, and never passes the hold point.

**Line-up entry:**
- `LineupGeometry.AlignEntry` bends the first 45 m of the built line-up so it leaves the hold point
  along the arriving taxi heading (`AdelaideGround.TryHoldApproachDirection`), easing onto the
  authored line with a matching tangent.
- It is applied after the fillet is built, so the turn, the roll-in and the take-off point are
  unchanged.

**Ground-flow pace:** an aircraft exactly on plan in the previous frame keeps following the plan
exactly (up to 1.6× pace), because the plan already accelerates from rest. Catching up after a
queue move still ramps.

**Parked engines:** shut-down propellers and fans stop (`AirsidePropellerDynamics.ParkedRpm = 0`).
The spool snaps its last half rpm to zero, so the easing does not leave a crawl.

**Doors:** `BoardingFlow.PassengersAtDoor`. A parked aircraft's passenger door is open only:
- from the moment it opens until 60 s after the last arrival is off;
- from 30 s before the first boarder until it closes for the push.

`EngineStartSequence.For` combines this with the bridge and stair-truck timing. Between rotations,
overnight, and on an aircraft with nobody to move, the door is shut.

## Affected systems

Drawn arrivals on final, the ground-flow chain, the line-up leg's first 45 m, propeller and fan spin,
parked doors and airstairs. Simulation timing is unchanged: runway occupancy, the line-up's length
and the take-off point are the same, and the landing estimate returns the same times.

## Evidence

- Unity EditMode 1302/1307. The 3 failures fail the same way on `origin/main` a4b7be62: pavement
  clearance, ground separation, and the gate reservation (whose gate differs from run to run on
  main).
- New tests:
  - `ParkedAircraftAndMotionTests`: line-up heading at every runway for every type, `AlignEntry`,
    `FlyFinal`, stepwise estimate equals the whole estimate, and parked rpm.
  - `EngineStartSequenceTests.ArrivedAircraft_ShutsItsDoorOnceEveryoneIsOff_AndStaysShutOvernight`.
- The measurements in Context came from a temporary EditMode probe, removed before commit.

## Not verified

**Not seen in the running game.** The frame-rate gain is inferred from the measured cost, not
profiled in the player. Only the new `AlignEntry` blend changes the line-up path itself. If the turn
still reads wrong in the game, the next suspect is the 10 kt line-up speed cap, which makes line-up
take 40–70 s.
