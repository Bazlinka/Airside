# 0175 — Per-type aircraft layout and ground routing

Date: 29 September 2026

## Context

Ramp crew (ADR 0174) and service vehicles were placed at the same fixed metres from every stand
stop. Jet art is rooted at the nose stop and turboprop art mid-airframe, so on a Saab 340 the
marshaller stood at the nose, the cone placer stood about 2 m from a running propeller, and the
baggage team stood behind the tail rather than at the hold door. The crew placement also turned
"right" the wrong way, so every worker stood on the opposite side to their job.

The ATR 42 mesh has its passenger door forward left and hold door aft right; the real aircraft
boards through an aft-left airstair door and loads bags forward left. Turboprops were catered by
the scissor-lift truck, which they do not use.

Every ground vehicle and walker moved in a straight line between two points (`MoveTowards` or a
`Lerp` on sim time), so trucks, buses and passengers passed through parked aircraft, nacelles,
propellers, buildings and the terminal.

## Decision

- `Simulation/AircraftLayout` holds each type's ground geometry in its art/stand frame (x right,
  z forward), measured from the runtime glTF meshes: fuselage, nacelles, propeller discs, wings and
  tailplanes with their underside heights, passenger/hold/catering doors. It derives the fuel,
  catering and baggage vehicle stops and pushes every anchor out of the footprint and propeller
  zones. Jet passenger doors are the aerobridge L1 doors (`AircraftDoors`).
- `RampCrew.ForActivity` places crew from the layout. Turboprops are catered by hand at the
  passenger door; `CateredByTruck` is false for them and the catering truck stays at its depot.
- Presentation moves the ATR's passenger-door parts aft and turns its hold-door parts to the
  forward left before the airstair conversion; `PropTurnaroundTests` checks the built model.
- `Simulation/GroundRouter` plans a route on a local grid round `RouteObstacle` outlines grown by
  the traveller's clearance (A*, blocked cells costly rather than forbidden, then string-pulled).
  Obstacles: parked aircraft footprints, YPAD buildings, terminal outlines, aerobridge rotundas.
- Service trucks, AI turnaround sets and the showcase set follow planned routes and replan every
  few seconds; buses follow one planned route parameterised by sim time; passenger walk paths are
  planned once per aircraft round the airframe and its propeller arcs. Vehicles do not enter the
  span or nose/tail corridor of a taxiing or pushing-back aircraft; they wait.
- The frontage-road legs of `GroundServiceRun` are unchanged: the road, including the terminal
  undercroft, is still the route.

## Consequences

- One table decides where people and vehicles go round every type; a new type needs one entry.
- Routing is presentation only and never gates the simulation; nothing is persisted.
- Pushback tugs, stair trucks (a short, straight approach to the door) and aircraft themselves are
  not routed. Vehicles yield to aircraft, not to each other.
- `scripts/review-turnaround.sh` renders each type's crew, vehicle stops and routes for review.
