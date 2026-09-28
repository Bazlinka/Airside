# 0152 — Honest en-route speeds, and motion paced by the frames that draw it

Date: 28 September 2026. Author: Claude, at Bailey's request (aircraft seen at 1700 kt; jitter on
takeoff and during taxi).

## Context

### An aircraft reported 1 700 kt

`EnrouteProfile` solves its cruise speed from the leg — `LegMetres / weighted seconds` — so that the
distance flown comes out exactly right and the map position stays honest. Nothing capped the result,
and `TryEnroute` handed it the length of the aircraft's **current state** as though that were the
whole leg.

Two places set an inbound state far shorter than the flight actually takes:

- **The opening arrival bank** (`SeedOpeningTraffic`) seeds aircraft already most of the way home, so
  there is traffic on final as soon as the game starts: Port Lincoln 3 minutes out, Melbourne 12,
  Auckland 30, Singapore 40.
- **A delivery flight** (`RegisterDelivery`) is given a flat 8 minutes from wherever it is bought.

Melbourne is about 640 km. Six hundred and forty kilometres in twelve minutes is 1 730 kt — the
number Bailey saw. Singapore in forty minutes came out near 5 000 kt.

The aircraft are not wrong to be minutes from landing; they are near the end of a long leg. The
profile was simply being told the remainder was the whole thing.

### Jitter on takeoff and taxi

`_preciseTime` — the clock every drawn aircraft position is a direct function of — was read straight
off `DateTime.UtcNow` every frame. There is no smoothing anywhere after it: `view.position` is
assigned from it outright. So each frame moved an aircraft by however long that frame happened to
take. The field currently runs with p95 frame times over 33 ms (open performance work), and a 60 ms
frame moved a departing aircraft the whole 60 ms in one step — at 70 m/s, a four-metre snap. Worst on
the takeoff roll, where the aircraft is fastest, and plainly visible on a taxi.

It was also mixing time bases: position came from the wall clock while the attitude damping used
`Time.unscaledDeltaTime`, so the airframe twitched against its own heading.

## Decision

- **Shape a leg by the time the aeroplane needs for it.** `TryEnroute` builds the profile from
  `LegTiming.AirborneSeconds(distance, type)` and places the aircraft by how long it has left, so a
  seeded or delivery arrival is drawn at the right point of a correctly shaped leg — right speed,
  right height, right distance to go. A flight with longer left than the leg takes (a delay) waits at
  the far end rather than being dragged along too slowly.
- **Cap the solved speed at what the airframe can fly.** `EnrouteProfile` clamps to the published
  maximum cruise where there is one, otherwise the planning cruise plus a small tailwind allowance.
  When a leg time is shorter than physically possible the position is allowed to lag; the aeroplane
  does not report a fiction. This is a floor under every caller, not only the two found here.
- **Pace the visual clock with the frames that draw it.** `LivePresentationTime` advances
  `_preciseTime` by `Time.unscaledDeltaTime` and slews it onto the wall clock, easing drift out over
  about a second. A difference past 0.75 s — a sleep, a load, a long stall — still snaps, because
  that is a real jump in time rather than a pacing wobble. It never runs backwards, since simulated
  seconds are floored from it. Position and attitude now share one time base.

Simulation timing is untouched: whole simulated seconds are still floored from `_preciseTime`, which
stays within the resync bound of real time, so scheduling and outcomes are unchanged.

## Verification

Unity EditMode **1252/1257**. The four failures are present unchanged on clean `main` at `a98d0359`
(baseline worktree run): `FlightPlanning_UsesEachTypesOwnCruiseAndPracticalRange`, both `Gate13_*`
and `BusyDay_NoAircraftDriveThroughEachOther` — gate/stand and ground-separation faults, still open.

Three new `EnrouteProfileTests` cover the clamp across Melbourne, Sydney, Auckland, Singapore and
Port Lincoln at 1 s, 8, 12 and 40 minute leg times for a turboprop and a jet; that the ceiling is the
published figure and not an open-ended allowance; and that a leg flown in its planned time still
cruises near the planning speed, so the clamp has not flattened the normal case.

The jitter fix is a pacing change and needs a human playtest to confirm: a takeoff roll and a taxi at
both the overview and follow cameras, watched for snapping.

## Still open from the same report

Taxi conflicts and aircraft passing through each other, give-way priority, and a longer visible
approach. `BusyDay_NoAircraftDriveThroughEachOther` is the same fault as the taxi conflicts and both
surviving episodes involve runway 12 traffic crossing the 05/23 flow.
