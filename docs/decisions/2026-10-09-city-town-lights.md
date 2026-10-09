# City and town lighting

Status: Accepted implementation of Bailey's request, 9 October 2026.

## Decision
Represent occupied windows on the existing mapped/generated building facades;
add street lamp sources and ground pools to mapped urban roads. Reuse surveyed
OSM lamp coordinates where available; keep the airport's existing fixtures.
Explicit OSM `lit=no`, airside, tunnel, restricted, service and unpaved roads
are excluded from inferred lighting. Lamp spacing and house occupancy are
presentation approximations, not surveyed household or electrical data.

Beyond detailed geometry, show sparse district lights only at ESA WorldCover
built-up samples, on their actual terrain heights. These represent settlements,
not individual surveyed homes or streets. Regional detailed coverage remains
limited to the shipped airport OSM snapshots; unknown land stays dark.

## Reason
Night flights should show the actual distribution of settlement without lights
scattered over fields/water or thousands of real-time Unity lights.

## Affected systems
Adelaide suburbs, roads and outer terrain; streamed national terrain and regional
OSM buildings; existing day cycle. Original shader and tile batches use terrain
depth, fog and geographic transforms. Lights fade with existing presentation daylight.

## Migration impact
None. No layout, simulation, clock, weather, save schema or new external data.
