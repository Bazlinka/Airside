# Australian cruise terrain and mapped airport approaches

Date: 2026-10-08. Owner: Codex cloud Agent 1. Task: #613.

Bailey requested a major visible improvement throughout Australian journeys, particularly
Hobart, and accurate airport environments that become detailed near landing while cruise
stays economical. The previous streamed world allowed only South Australian journeys,
coloured/elevated only South Australia and rendered seven unmarked runway rectangles.

## Decision

Expand presentation coverage to 112–155°E / 45–9°S. Keep the existing double precision
YPAD frame, floating origin and simulation flight timing. Reuse the already licensed
OurAirports airport/runway catalogue for every Australian destination: the longest
strip determines the visual regional approach, while all actual strips appear.

Ship a small offline Australia elevation/class grid; the existing SA files and Natural
Earth shoreline remain safe fallbacks. Load at most one airport approach elevation,
WorldCover class grid and OSM airport footprint map within 70 km, retaining it to 80 km
for hysteresis. Detailed airport terrain uses 250 m mesh cells across at most nine
16 km tiles. Other near terrain uses 1 km cells; cruise uses 2 km cells. Enter cruise
above 4,500 m and exit below 3,500 m, so altitude noise cannot continually rebuild tiles.
Stream one mesh per frame, alternating fine and coarse builds to bring the high-altitude
horizon in promptly. Cruise retains 49 near and 49 coarse tiles; wide overview retains
its existing bounded 121–289 coarse ring. Airport models build 24 features per frame,
with no colliders, per-building objects or simulated resources. Owned meshes/materials
are destroyed when their airport leaves the retained region.

Runway paint uses accurate mapped strip position, orientation, length and width;
centreline dashes, edges, threshold bars and aiming marks remain geometric at follow
range. OSM taxiways/aprons, terminal/hangar/building footprints and major roads retain
real coordinates. Concave polygons use ear clipping rather than invented rectangular
terminals. Taxiway centreline paint and solid-surface materials distinguish pavement.
Airport roads/buildings are lower detail stylised geometry rather than photographic
textures. The detailed Adelaide scene continues to own its original area.

## Sources and licensing

Mapzen/AWS Terrain Tiles, WorldCover v200 and OSM; exact source URLs, bounds, processing,
licences, attribution and file hashes are in `docs/data/australia-flight-world-*-v01.json`
and the asset register. No paid service, runtime HTTP, streamed network dependency or
external SDK is introduced. Source fetching preserves the cloud HTTP proxy and verified
TLS; the COG range adapter uses Python's trusted HTTPS client for GDAL reads.

## Migration and limits

Presentation only: no Domain/Simulation state, reservations, commands, saves or schema
changes. Airport selections are not persistent. Missing/corrupt approach assets preserve
national/SA terrain and painted runway geometry. Native Unity compilation, appearance,
frame time, long-flight performance and transition quality remain unverified. Verification
is bounded per Bailey's standing policy; do not infer a native playtest from offline data
or syntax checks. Long-haul outstation presentation still uses its existing timetable
track; this decision does not introduce outstation resource simulation.
