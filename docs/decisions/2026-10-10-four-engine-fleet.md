# Passenger 747-8 and A380-800 support

Status: Accepted for implementation at Bailey's request
Date: 2026-10-10

Add B748 passenger 747-8 (selected by Bailey) and A388 A380-800 through the existing catalogue, purchase/progression, performance, live-feed, save and original aircraft kit pipeline. Both are code F; 747 Heavy and A380 Super wake. International base leases mapped 18R without increasing fleet capacity; shared-pier exclusivity remains. Historical AAL published code F right-hand lines guide stand limits; current real operational approval is not represented.

Original distinct four-engine geometry and four independent main trucks reuse established articulation, materials/glazing and audio sources. Two-side startup channels represent the two engine pairs; no new simulation machinery. Main-deck cabin and representative reused cockpit display families are explicit simplifications.

Affected: catalogue/acquisition, stands/base, ground service footprints, performance/economics, live sky model selection, visual/audio profiles, original generator, asset/spec registers. Ambient airline schedules and project plan stay intact.

Migration: none. Existing stable string type IDs and save format already support additional catalogue entries. Existing saves keep old fleet/stands; new IDs survive capture/restore. No schema bump.

Integration: newly merged economy v2 is retained. New market values live in Domain/LeaseTerms; acquisition deposits and FlightCostModel profiles derive from that shared table. No additional migration; existing v24 conversion remains untouched.

Code F aircraft may use clear compatible shared F overflow lines when 18R is held; this prevents seasonal operators from indefinitely stranding an arrival. Narrowbody/E allocations remain unchanged. Private QA type injection grants only the required fixture base; full journeys respect the selected type and retain normal engine-start timing.

Player code F aircraft reserve their existing departure line/pier through a rotation, including shared overflow, until taxi-in holds the assigned return stand. Existing persisted DepartureStand/state derive the reservation; candidate-aware stand queries exclude the owner. Native A380 review found background B738 arrivals claiming its overflow gate while it was away; this closes that capacity loss without changing other aircraft classes.
