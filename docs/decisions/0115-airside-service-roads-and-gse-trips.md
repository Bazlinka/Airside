# 0115 — Airside service roads, and ground vehicles that drive them

Date: 23 September 2026. Bailey asked for catering, baggage and fuel trucks, and for each
vehicle to be able to drive under the terminal as at the real Adelaide.

## Decision

### The vehicles already existed; the trips did not

`_fuelTruck`, `_cateringTruck` and `_baggageCart` have been in `AirsidePrototype` for some
time, with authored models. What they did was appear beside the aircraft when their prep
stage began and vanish when it ended, shuffling between two points a few tens of metres
either side of the stand. There was no service-road network in the project at all.

Each vehicle now leaves a depot ahead of its stage, drives a real road to the stand, works
while the stage runs, and drives home afterwards.

### The roads are real (DAT-YPAD-SERVICE)

`scripts/generate-ypad-service-roads.py` reads a committed Overpass snapshot
(`docs/data/osm/ypad-service-roads-2026-09-23.json`, ODbL, OSM base 2026-09-23T05:40:02Z)
and bakes `Simulation/AdelaideServiceRoads.cs`, in the same runway frame and by the same
method as the taxiways, buildings and coastline. Landside parking aisles and driveways are
dropped by tag; what remains is the airside network:

- the **apron frontage road** (OSM way 230893941), 1,122 m along the Terminal 1 face —
  this is the road the vehicles drive;
- **Airside Access Road**, **Security Road** and **Localiser Road**, kept for context.

### The undercroft is authored, and says so

Bailey asked for vehicles to pass under the terminal. Two facts from the real data decide
how that is honoured:

- OSM carries **no `tunnel`, `covered` or `layer` tag** anywhere in the extract; and
- the real frontage road runs about **91 m off the terminal's airside wall** (road z≈346,
  wall z≈437). A fully extended aerobridge reaches 47.5 m (3.5 m rotunda stand-off plus a
  44 m tunnel, ADR 0113), so its tip stops at z≈390 — a further **44 m clear of the road**.

So the real road passes under neither the building nor the bridges, and marking an
undercroft on it would have been a fiction. Instead a short **authored spur** leaves the
frontage at x≈1250 and runs inward beneath the terminal into the baggage hall. It is
generated alongside the real road but labelled in the generated file, in the register and
here as a design decision rather than imported geometry.

`AdelaideServiceRoadPath.IsUndercroft` therefore returns **false for every frontage point**,
and a test asserts exactly that, so nobody can later mistake the frontage for a covered road.

### Who drives where (Simulation/GroundServiceRun.cs, UnityEngine-free)

| vehicle | depot | route |
|---|---|---|
| Fuel | frontage, south-west end (fuel farm) | frontage only |
| Catering | frontage, north-east end | frontage only |
| Baggage | **baggage hall, under the terminal** | spur out through the undercroft, then frontage |

Baggage is the vehicle that uses the undercroft, and it does so on every trip — which is
both what the real hall's position implies and the most legible way to show the feature.

Vehicles set off `ApproachLeadSeconds` (90 s) before their stage and travel at 7 m/s, about
25 km/h. `DeparturePrep.SecondsUntilStage` was added so a vehicle knows when its own stage
starts and ends; it is a pure read over the existing stage arithmetic and changes no timing.

**Presentation only.** Nothing in the simulation waits for a vehicle, exactly as with
`BoardingFlow` and the aerobridges.

## Evidence

- `scripts/test-domain.sh` — **766 passed**, 16 of them new in `GroundServiceRunTests`.
- Mutation-checked: making baggage skip the undercroft fails 2 tests.
- The generator is reproducible — a second run leaves the baked file byte-identical.
- Tests assert the road has no teleporting gaps, that a trip follows the polyline rather
  than the straight line, that drive times are plausible for an apron vehicle, that the
  baggage trip starts under the terminal and emerges exactly once, and that fuel and
  catering never go under it.

Not verified: the Unity compile and the in-engine look. `scripts/test-unity.sh` cannot run
on this Mac (batchmode licensing loop, `docs/build-mac-batchmode-dead-end`), and the harness
does not compile `AirsidePrototype.cs`, where `DriveServiceVehicle` was added.

## Original open issue (resolved below)

The undercroft is a route, not yet a hole: the terminal shell in
`AdelaideTerminalArchitecture` has no modelled opening, so a vehicle on the spur currently
drives beneath an unbroken building. Cutting the void — and deciding whether the frontage
should also gain an authored covered section under the aerobridges — is the next step.

## 24 September 2026 follow-up

The undercroft facade issue above is resolved: the Domestic & International
Terminal's procedural shell now cuts a 9 m × 3.4 m portal around the authored
baggage spur and retains its wall above. The real OSM frontage remains open-air;
nothing is claimed to pass under the aerobridges. Ground-service routing, timing
and saves are unchanged. A Unity mesh regression checks the opening and all
other terminal walls; packaged close-view inspection of a moving baggage tug
is still required.
