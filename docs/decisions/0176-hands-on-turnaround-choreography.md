# 0176 — Hands-on turnaround choreography

Date: 29 September 2026

## Context

Ramp crew (ADRs 0174, 0175) stood still beside the aircraft playing a looped clip while the
turnaround stage advanced: nothing visibly moved into the aircraft, the baggage train stayed full,
the fuel hose trailed from a standing worker, the hi-loader never rose and turboprop passengers'
roller bags went up the airstair with them.

## Decision

- `Simulation/ServiceChoreography` turns each job into item-by-item work, a pure function of the
  stage clock (elapsed and length), like `BoardingFlow`:
  - **Baggage:** a handler walks bags one at a time from the train to the hold — by hand into a
    turboprop hold, onto a belt loader for a jet. The number of bags is what fits the stage at a
    realistic trip time (`BagCycle`: walk both ways at 1.3 m/s plus 3 s handling, at least 6.5 s),
    capped at the seat count. The train visibly empties.
  - **Fuel:** the nozzle is walked out from the truck's reel on its hose (first 14% of the stage),
    connected, and walked back and stowed (last 14%).
  - **Catering:** a jet's hi-loader drives cab-first to the aft service door, its box and platform
    rise to the sill, and 2–6 trolleys are pushed across; a turboprop's trolley is pushed to the
    airstair and 2–6 galley boxes are carried up it.
  - **Planeside bags:** turboprop passengers with roller bags leave them on a cart by the stairs;
    a handler loads them into the hold in arrival order (`Queue`).
- `DoorSills` gives each type's passenger, hold and service-door sill heights from its mesh.
- Presentation draws the actions (walk clip while moving), what each worker is carrying, loose
  items in transit, a primitive belt loader, the planeside cart, and drives the vehicles' own
  parts: train bags, the fuel truck's stowed nozzle, the hi-loader's box/platform/scissor.
- `scripts/review-service-work.sh` renders mid-job frames with the game's own code.

## Consequences

- Nothing is simulated or saved; pause, acceleration and reload stay exact.
- AI jet turnarounds have no catering truck, so their catering crew stays on the ground.
- The belt loader is a primitive (the authored belt-loader kit's boom has a fixed angle).
