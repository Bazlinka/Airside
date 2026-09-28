# 0166 — Ground queue and second-Saab regressions

Date: 28 September 2026. Author: Codex, after Bailey asked for mechanics refinement and bug fixing.

## Player-visible outcome

Aircraft no longer overlap while feeding the runway 23 queue during a busy day. A large aircraft
waits on its stand when the occupied holding queue's offset authored paths cannot keep its wings
clear. The second Saab remains an affordable expansion choice without making the existing Saab
cheaper to maintain.

## Decision

- Route clearance predicts the same queue braking used by the visible aircraft, rather than
  checking an unbraked path and assuming the final queue zone is safe.
- A taxi-out remains moving traffic until its state end. It is only treated as an established
  queue member after that point.
- Runway holders are never ignored by the long-wait escape intended for an arrival that can remain
  indefinitely without a stand.
- The runway/exit queue interval is 80 metres. Mixed queues with a combined half-span above 45
  metres do not feed another aircraft into an occupied hold because their type-specific paths can
  approach runway 23 on different curves.
- The geometric drive-through threshold is 84 percent of the combined half-spans plus two metres.
  This retains the deliberately tolerant parallel-taxi/adjacent-stand model while avoiding a
  sub-metre false positive at the Q400 stand lead-in.
- Saab 340B routine checks remain $400. Acquisition price is not an operating-cost balance input
  for the promotional second-aircraft offer introduced by ADR 0164.

## Scope and invariants

`GroundTraffic`, `AdelaideGround`, `Maintenance` and their EditMode regressions are in scope.
Runway, taxiway and stand reservations remain simulation-owned; frame rate does not affect the
outcome. Routes, aircraft performance, flight economics apart from the restored Saab check price,
and visual models remain unchanged.

## Acceptance

- The seeded full-day ground-separation test reports no overlap episodes.
- Same-lane pushbacks wait and are released after the swept path clears.
- Separate-apron movements, runway sequencing, gate reservations and hold explanations still pass.
- A Saab 340B routine check costs $400 at the starter base.

