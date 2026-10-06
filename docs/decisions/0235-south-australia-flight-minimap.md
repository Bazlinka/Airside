# ADR 0235: Select South Australian flights from the mini map

Date: 6 October 2026. Requested by Bailey.

Player outcome: switch the corner map between Airport and South Australia, click
any simulated flight in the South Australian scope, inspect its status and journey,
then choose the available cockpit, window-seat or exterior camera. Interstate
services are included during their South Australian segment. Repeated clicks cycle
coincident markers so overlapping aircraft remain selectable.

Scope: mini-map projection and selection, geographic journey camera eligibility,
distant status readout and exterior availability. Reuse the existing stable
registration, selection card and flight-view systems. Distant positions come from
the same journey path as the camera, independently of active 3D actor visibility.
The regional scope includes SA coastal routes (129–141 E, 26–38.5 S); it is a
geographic viewing scope, not a legal state-boundary polygon.

External live-traffic decoration remains outside the simulated fleet and does not
gain game status/camera access. Helicopters can use the exterior camera; their
cockpit and passenger interiors remain unavailable. Cargo has no passenger cabin.
Airport clicks/drags retain local camera movement; empty regional map clicks do
not recenter the airport camera kilometres away. Mini-map selection keeps the
status card on the HUD rather than opening the route planner.

Acceptance: all served SA destinations fit the scope; hidden aircraft have stable
geographic markers across render-origin changes; interstate viewing is available
inside the scope; clicking a distant registration exposes its journey and available
camera actions; overlapping registrations can all be selected; native compilation,
regression and packaged presentation evidence are recorded separately.

Schedules, reservations, save schema, flight timing and aircraft art are unchanged.
No new external data or assets. Scope is session-local; no saved-setting migration.
