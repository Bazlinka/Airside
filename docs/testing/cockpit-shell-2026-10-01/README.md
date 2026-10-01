# Saab cockpit shell fix

The previous packaged `v2-game-startup.png` shows apron through an open strip
between the left side lining and instrument panel and beneath the panel. Cockpit
mode hides the exterior fuselage, so its interior must provide an opaque shell.

The floor now reaches the forward nose contour; continuous lower side, forward
footwell and rear side faces close the body below the existing window sills.
The actual window openings remain clear. Original geometry only.

Inspected native images: forward, footwell, left-down and right-down. These use
the runtime SF34 builder and pilot eye. The editor review also retains its existing
layout/window/roof/bank views. Reproduce with `CockpitAppearanceReview.Run`.

Regression uses the actual shell meshes to check seven lower sightlines at level
and banked/rotated aircraft poses, and three window sightlines that must stay open.
Native Unity: 1679 total, 1677 passed, zero failures; two existing inconclusives.
Headless: 1278 passed, zero failures. Diff whitespace check passed.
