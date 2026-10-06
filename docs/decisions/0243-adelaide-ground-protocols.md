# ADR 0243: Adelaide ground clearances and wake separation

Status: accepted for implementation, 6 October 2026. Merge awaits native validation.

## Decision

Use the current ERSA YPAD and Australian MOS 172 to constrain ground clearances and wake minima. A stationary aircraft remains a blocker indefinitely. Manual stand selection reserves the choice and waits for the same route/crossing clearance as automatic taxi-in. The tower checks stationary traffic before clearing an arrival, including the first 30 seconds beyond its exit. Taxi movements reserve crossing windows expanded by the full aircraft envelope. Adelaide's intersecting strips share physical occupancy; departures on the other strip wait through the initial climb. Each strip retains its own queue.

Wake classification uses published MTOW bands, with the 25-tonne medium distinction, rather than wingspan. Minima depend on both aircraft and on departure/arrival. Persist the previous movement and its airborne/touchdown time per physical strip. Full-length departure spacing is airborne-to-airborne, and arrival spacing touchdown-to-touchdown. Same-runway landing behind a departure uses the MOS exception. Cross-runway wake is conservatively applied to the intersecting flight paths. The existing 90-second physical-strip scheduling buffer remains a gameplay assumption, independent of mandatory wake minima.

Code C terminal pushbacks use the same eastbound apron push for either runway. The graph excludes prohibited named taxiways for the airframe and departure bay. Unknown/unlabelled map connections remain the authored apron connectivity; above-D named routes use the ERSA allow-list. The whole OSM A2 is conservatively unavailable above Code C because the snapshot does not identify the G1 division. OSM's combined L is treated as L1 for the bay restriction. Typed graph searches fail closed; no straight-line fallback across grass. Preserve the authored apron connection to the graph and the painted final stand lead-in.

Above-C arrivals no longer use E2: 05 continues to L2; 23 uses F4 for D and F5 for E. Rollout continues forward, without a runway backtrack/180-degree turn. Compatible exit joins determine the actual arrival waiting point and inbound route. For the existing Code C 23 rollout, E2 remains an explicit ATC-directed exit variation; the published default D1 and runway-specific early rollout profiles are deferred rather than inventing a backtrack to D1 after it has been passed.

Holds are route-release reservations at the stand or exit, not a new intermediate-taxi pause state. This intentionally conservative implementation does not claim radio/readback interaction, a taxi-node queue/recovery controller or full aerodrome procedural certification. Service vehicles in the shared movement helper obey 25 km/h on apron, 15 km/h on terminal-road legs and 10 km/h within 15 m of a ground aircraft. Other specialised vehicles have separate movement systems.

## Migration and validation

Save version 21 adds nullable main/cross wake records; records are copied on capture/restore. Versions 1–20 preserve existing free-at deadlines and receive no invented historical wake leader. Current in-progress phase duration handling remains compatible. Native compilation/visual checks remain unverified because the owner prohibited Unity execution. Headless regression and static evidence live in `docs/testing/adelaide-ground-protocols-2026-10-06/README.md`.

Sources and exact regulatory/gameplay distinctions: `docs/data/ADELAIDE_GROUND_PROTOCOLS.md`.
