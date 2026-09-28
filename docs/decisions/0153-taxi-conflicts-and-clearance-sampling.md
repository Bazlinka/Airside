# 0153 — Aircraft that no longer taxi through each other

Date: 28 September 2026. Author: Claude, at Bailey's request (taxi collisions; aircraft passing
through and impeding each other).

## Context

`GroundTraffic.PathClear` clears a taxi leg by walking it and comparing the candidate's position
against every other aircraft's predicted pose. The design is right — an aircraft does not start a leg
until the whole route is clear — and it took ground conflicts from about 36 a simulated day down to
two. Those last two survived, and `BusyDay_NoAircraftDriveThroughEachOther` had been failing on `main`
because of them.

The scan stepped **2 s at a time**. At taxi speed that is about 30 m between samples, and the
clearance being tested is about 25 m. Two aircraft crossing could therefore pass each other *between*
two samples and never be compared at the moment they were closest.

The evidence said exactly that. Both surviving conflicts were marginal — 24 m and 25 m against a 25 m
limit — and both were mid-taxiway crossings rather than anything at a stand or a holding point:

```
VH-ZRC (SF34 TaxiOut Runway12) @(444,310)  vs VH-VZV (B738 TaxiIn Runway05) @(423,297)  d=24
VH-ZRE (SF34 TaxiOut Runway12) @(1479,294) vs ZK-NNA (A21N TaxiOut Runway05) @(1485,319) d=25
```

The planner also used the *same* clearance figure as the check for whether two airframes actually
overlap, so a leg cleared with a metre to spare became a conflict as soon as anything drifted — a
queue moving back a place, a leg scaled to a slightly different duration.

Sampling finely enough everywhere to close the gap cost nearly three times as much, which this
simulation cannot afford: it timed `Inbound_ExpectsClearanceNoEarlierThanItArrives` out at 180 s, and
the field already has open render-performance work.

## Decision

- **Coarse scan, fine where it matters.** The walk stays at 2 s, but each sample now asks only
  whether the two aircraft come within `EncounterProbeMetres` (70 m) of each other. That is wide
  enough to bracket any real conflict — two aircraft closing head-on cover about 60 m between coarse
  samples, so a pair that will breach clearance is already inside it on the sample before or after.
  Only then is the ±2 s window around that sample re-walked at 0.25 s, finely enough to find the
  closest approach. Cost stays near the old coarse scan, because refinement only happens near an
  actual encounter.
- **The controller plans with a margin.** `TooClose` takes an optional margin; `PathClear` uses 3 m,
  kept deliberately small: the aliasing above is what actually caused the conflicts, and
  aircraft legitimately pass close on the apron. At 12 m only one aircraft could taxi on an apron at
  a time, which is not how the apron works. The check for whether airframes actually overlap still
  uses no margin at all.

## Also fixed here

Two Gate 13 tests were failing on `main` for an unrelated reason: they compared poses and legs from
`AdelaideGround` — which reads the **aligned** gates — against the raw pre-ADR-0141
`AdelaideLayout.TerminalGates` coordinates. ADR 0141 deliberately moved every contact gate's stop to a
common setback from the T1 wall, so the tests were measuring the alignment rather than the gate. They
now read the aligned gates, as the game does.

## Verification

Unity EditMode **1254/1257**, against **1241/1246 with four failures** on clean `main` at `a98d0359`
(baseline worktree run).

- `BusyDay_NoAircraftDriveThroughEachOther` — **now passes**. This is the taxi-collision fault.
- `Gate13_NeverResolvesAsARegionalBay`, `Gate13_NoseInToTheStop_TailFirstPushback_ThenForwardTurnout`
  — **now pass**.
- `Inbound_ExpectsClearanceNoEarlierThanItArrives` — still passes, so the two-pass scan has not cost
  the simulation its throughput.

Two remain, both Gate 13 **allocation** questions rather than movement:

- `FlightPlanning_UsesEachTypesOwnCruiseAndPracticalRange` — "A Boeing 787-10 cannot park on
  GATE-13". Pre-existing on `main`, untouched by this change. A code E aircraft is being offered a
  code C gate somewhere in flight planning.
- `Reservations_GateLeadInAndRunwayHeldBeforeMovementAndReleased` — **caused by this change**, and
  honestly so. Changing taxi timings means an AI 737 can now take Gate 13 while the Virgin 737 is
  away, so Virgin comes home to Gate 23 and the test's `jet.Stand == GATE-13` fails. No aircraft is
  ever double-booked onto the gate; the `gateHolders <= 1` assertion in the same loop still passes.

  This is a design decision for Bailey, not a bug to paper over: **should a scheduled operator's gate
  be held for it while it is away?** `SuggestStand` already sends an operator home when its gate is
  free, so the intent exists; nothing stops somebody else taking it meanwhile. A ranking penalty in
  `SuggestStandFor` that avoids another away operator's home gate does restore the test and reads
  well, but it shifted stand allocation enough to break
  `ResumedGame_ContinuesExactlyLikeOneThatNeverStopped` — a resumed game's runway free-at differed by
  12 s, because `ReconcileRunwayFreeAt` recomputes it on restore rather than trusting the saved
  value. That is a separate, pre-existing save-fidelity gap. The ranking change was reverted rather
  than destabilise saves for a test.

## Still open from the same report

Give-way priority, and a longer visible approach (`ApproachStartX` is −4 200 m, about 38 s of visible
final; lengthening it changes derived approach timing across the schedule and needs the terrain extent
and the camera far plane checked).
