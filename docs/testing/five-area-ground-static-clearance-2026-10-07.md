# Ground clearance correction on current main — 7 October 2026

## Current contract

This evidence applies to the integrated working tree after merging main
`1fd4c9fc`, including the accepted decision
[An arrival on final never waits](../decisions/2026-10-07-arrivals-never-hold-on-final.md).
Landing clearance checks the runway and exit; no full taxi-in route check delays
an airborne arrival. Ground control checks its taxi route after landing. Main's
`AdelaideGroundPolicy.StandApproachMetres` is **100 m**, and the parked-neighbour
exception uses that same approach distance. Earlier 90 m/pre-landing evidence in
the original five-area packet is historical, not the final policy.

Player outcome: safe passage past parked regional aircraft without stranding
arrivals overnight, and a positive planning gap around fixed parked aircraft.
Scope: BAY-4's short apron taxi-in section, fixed-obstacle clearance and pure
regressions. Other stand routes, taxi graph edges/restrictions, runway/stand
reservations, save schema, departure timing rules and the approved arrival policy
remain unchanged.

## Correction and measured limits

The original Q400 taxi-in to BAY-4 passed only 25.67 m from a Q400 parked at BAY-2.
This could overlap the airframes. The same route with a Saab 340 also triggered the
3 m planning allowance and held behind a night-stopping Q400 until the morning.
An eight-metre maximum northward offset affects only the short BAY-4 apron section;
a smooth spatial weight fades it out, preserving route endpoints. It stays within
the 23 m apron lane. This is a localized apron correction, not a new taxi graph.

Measured SF34/Q400 nose tracks on all four runways keep more than 33.1 m from BAY-2,
31.9 m from BAY-3 and 40.4 m from BAY-1, including the complete lead-in rather than
excluding it. These exceed the largest Q400 overlap limit plus the full 3 m moving
planning allowance. Dedicated endpoint and clearance regressions accompany the
change; native pavement/paint alignment remains unverified.

A separate old-policy soak exposed a terminal planning hold where the actual gap
was positive: A320 at GATE-19 versus B38M at GATE-20R had 2.24 m spare clearance.
The fixed parked pose cannot drift forward with a queue or change taxi speed.
Fixed AtStand obstacles therefore retain a **positive 1 m planning gap**, while all
moving/queued traffic keeps ADR 0153's **3 m** allowance. This does not reduce the
actual overlap threshold or excuse an overlapping route. Boundary regressions
require a parked 0.5 m gap to be rejected and a 1.5 m gap to be admitted; queue gaps
of 2.5 m remain rejected and 3.5 m admitted.

## Current-policy static audit

The supplementary audit ran against the built current working-tree headless
assembly under `/workspace/Airside`. It:

- covers every shipped fixed-wing type on each compatible stand and permitted
  inbound route, with terminal aircraft on the supported 05/23 runway;
- compares each candidate with the largest compatible parked wingspan at every
  other stand;
- asks production `GroundTraffic.PathClear` for clearance and independently samples
  accepted paths every 0.1 seconds outside the matching **100 m** own-stand approach;
- measures centre distance minus the unchanged airframe overlap limit, requiring
  at least one metre of additional static clearance.

Result: **428 routes; 14,552 worst-compatible stand pairs; all 14,552 accepted;
zero overlaps or gaps below one metre.** Minimum accepted swept gap is
**2.782613 m**, A359 at GATE-20 on Runway05 versus B38M at GATE-20R. Two accepted
pairs have a gap between one and three metres. Stand-pair inputs are conservative:
the audit also includes pairs that reservation rules would forbid simultaneously.

Retained [source](five-area-ground-static-audit-2026-10-07.cs.txt) and
[output](five-area-ground-static-audit-2026-10-07.log). The source runs as a temporary
net8.0 console with implicit usings enabled and a reference to the built headless
`Harness.dll`. It uses reflection only to construct isolated test aircraft; no live
simulation/save is mutated. This samples simulation geometry, not Unity visuals.

Root is running the combined current-policy regressions and final full suite; this
standalone audit does not replace those results. No Unity editor/player, native
EditMode tests, builds or rendered captures were run under the retained restriction.
