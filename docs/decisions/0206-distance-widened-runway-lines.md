# 0206 — Runway lines widen with distance

Date: 2026-10-01. Requested by Bailey (flickering, wavy runway lines).

Cause: the runway edge and centre lines are 0.9 m wide cubes. From roughly a kilometre out that is under one
pixel, so the line aliases into dashes that crawl as the camera moves. SMAA and MSAA cannot recover geometry
that thin. Not depth fighting: the paint sits 3.5 cm clear of the surface and close views were clean.

Decision: draw the long thin lines (05/23 and 12/30 edges, centre dashes) as flat ribbons whose vertices are
widened per node by `AirsidePaintWidening.Width` (about 1.6 px, at most 9x the real width), updated only when
the camera moves a metre or zooms. Names, material and height are unchanged. Wide marks (numbers, aiming points,
touchdown zones, threshold bars) are not touched.

Not done: taxiway centrelines and edge wear are single combined meshes and are still sub-pixel at long range.

Validation: headless 1194 passed; Mac build captures at 250 m / 1 km / 3 km (continuous lines at 3 km vs beaded
before). Shimmer under motion is not yet confirmed by eye in play.
