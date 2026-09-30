# 0191 — Far zoom stays where you look

Date: 30 September 2026. Author: Cursor, from Bailey's report: zoomed out, a circular haze crosses the city, and zooming in always returns to the city instead of the point under the cursor.

## Player-visible outcome

Zoomed out over Adelaide, the ground no longer fades to fog in a circle. Scrolling in stays on the place under the pointer. Panning after that does not yank the view back to the airport.

## Cause

The surroundings and suburb shaders fade to fog by distance from the camera. Past the classic 4.5 km zoom that sphere meets the ground as a ring through the view. Separately, the pan leash shrinks as you zoom in (`PanRadius`), and `ClampPanCentre` then dragged the orbit centre back toward the airport. A pointer ray longer than 45 km was also discarded, so the zoom target fell back toward the ground under the camera.

## Decision

- `HorizonScale` still matches the 30 km clip up to 4.5 km out. Further out it pushes the fade start past the far clip. Weather fog, already thinned with height, is the remaining haze.
- Zooming in grandfathers the orbit centre: the shrinking leash does not pull it home. Zooming out is still clamped to the leash for the new distance. A later pan may move closer to the airport, or around where you are, but not further out than that.
- A real ground hit may be up to 400 km along the ray. The sky fallback distance is unchanged.

## Not changed

Close-in overview fade, follow zoom, and the 45 km orbit limit.

## Tests

`CameraFeelTests`: the far fade starts past the far clip; a grandfathered centre is not pulled inside the classic radius; a 40 km-high ray to a point 40 km north hits that point.
