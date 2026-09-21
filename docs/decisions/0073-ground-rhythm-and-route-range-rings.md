# 0073 — Airport-scale ground rhythm and route range rings

Date: 2026-09-21

## Decision

Add low-contrast 34 m mowing bands parallel to Adelaide's main runway inside the existing ground
shader. Disturb their phase with slow procedural noise and suppress them as worn-dirt weight rises.

Draw dashed geodesic range rings at 500, 1,000 and 2,000 km around Adelaide in the Route Map. Label
the rings where their eastern point is visible and keep them below coastlines, routes and aircraft.

## Reason

The terrain stack already blends authored dry grass, green grass, dirt, normals, masks and satellite
context. Replacing it would add cost without fixing the visible problem: at overview scale the infield
still reads as one broad washed surface. Maintained mowing rhythm gives it airport-scale structure.

The Route Map already states route distances, but a player comparing aircraft range cannot turn a
list of numbers into geographic reach at a glance. Real geodesic rings add that planning information
without inventing market share or suggesting unavailable routes.

## Consequences

- Ground stripes are shader-only and add no textures or scene objects.
- Worn service ground remains irregular; pavement continues to cover the terrain beneath it.
- Rings are reference guides, not an aircraft-specific hard limit. Actual availability still comes
  from the selected aircraft and route-band rules.
- `RouteMap.DestinationPoint` is pure, testable spherical geometry shared by the painter.

## Verification

- Domain suite passes, including a new test checking the generated ring point is within 10 m of its
  requested 500/1,000/2,000 km distance.
- Shared Route Map draw list rendered at 1440×900; rings sit behind routes and labels remain legible.
- Unity EditMode and a fresh packaged Mac overview verify the shader compile and ground result.
