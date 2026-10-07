# ADR 2026-10-07: Inline save absence and native flight-view contracts

Date: 7 October 2026 (Adelaide). Status: accepted for merge by Bailey; native verification pending.

Unity serializes inline custom reference records by value. Treat an exactly
all-default MaintenanceJob or RunwayWakeRecord as absent on restore, just like null.
A partial record still goes through existing validation and fails if invalid. A
Maintenance-state aircraft still requires a real job. This repairs v21/v22 JSON
round trips without adding fields or increasing the schema version. Active jobs,
old timed checks, and actual preceding wake movements retain their existing rules.

The busy-day fixture must represent the runtime's existing ground geometry. Use
HelicopterTrack for pad movements and GroundTraffic for wheels-on-runway movement,
rather than drawing helicopters on a fixed-wing takeoff path. Match the authored
100 m final stand approach when excluding fitted stand neighbours from transit
collisions. Expose that existing distance from AdelaideGroundPolicy as a constant
used by the fixture. Main's native encounter was ~95 m from the moving Q400's own
stand and falls within that approach. The fixture now runs headlessly and asserts
more than 100 rotations, guarding against resolving overlaps by stopping traffic.

An experimental parked-aircraft clearance extension reduced the seed-2026 busy day
from 132 to 15 rotations through taxi deadlocks and was removed. No shared ground
clearance behavior changes are shipped in this repair.

Camera entry and exit glide anchors translate with a floating-origin shift. Test
entry progress with an explicit time step rather than assuming EditMode immediately
finishes the 0.9-second transition. Retain ADR 0242's entry animation and saved optics.

Affected systems: save restore, shared stand-approach distance, flight camera presentation,
and their native/headless fixtures. No runtime art changes. Validation and remaining
native checks: docs/testing/native-editmode-fixes-2026-10-07/README.md.
