# ADR 0238 — continuous takeoff and shared flight information

Date: 2026-10-06. Status: implemented in code; native visual verification excluded.

Bailey reported a step in aircraft altitude at takeoff and requested more flight
status/location information in every aircraft view. Bailey explicitly excludes
Unity tests, builds, editor/player execution in this session.

## Decision

A watched Outbound flight follows the same local Departed curve for the first
DepartedSeconds. Outbound begins at TakeoffEndHeight; EnrouteProfile begins at
DepartedEndHeight, so switching directly formerly skipped roughly 174–218 m
of climb. Reuse the mapped runway/departure turn rather than hiding the jump with
frame-dependent camera smoothing. After local climb-out, a timed Hermite offset
matches the local exit position/velocity to the geographic route, then fades to
zero before regional terminal handling. The leg clock and scheduled arrival stay
unchanged. Rotation/takeoff/climb-out height curves also share endpoint vertical
rates. Preserve authored body pitch while joining the enroute flight angle.

Cockpit, left/right passenger and exterior views consume one read-only snapshot
of the watched registration. Show route/status, nearest catalogue airport and
coordinates, field-relative height, true heading, GS/V-S, direct distance to the
leg target and supported time/progress to the arrival area. These are model
estimates, not pilot navigation instruments or touchdown promises. Derive location
from render position plus floating origin; derive height and movement from the
actual view rather than the profile that formerly teleported. Ground and holding
states show explicit status without fabricating ETA. Keep the central sightline
clear and use a narrower two-column panel when necessary.

Scope: Presentation departure curves/world handoff/attitude and flight-view
telemetry/painter; two pure regression fixtures and generated harness. Simulation,
save schema, schedules, weather, aircraft identity, cameras and assets are unchanged.
No migration required. No external data/assets added.

Acceptance: all 13 fixed-wing types share height/vertical-rate endpoints; pure
route offset matches supplied endpoint tangents and disappears after joining.
All four HUD views show the same flight details; true heading/floating origin and
route direction/ground status are checked. Painter boxes/buttons stay inside
supported viewport panels. Native partials, transforms, camera switching and font
metrics require later owner playtesting and are unverified here.
