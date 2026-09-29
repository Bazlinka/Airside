# 0182 — Road edge lines and lane lines

Date: 29 September 2026. Author: Claude, from Bailey's request for more accurate road detail around the airport.

## Player-visible outcome

Roads around the airport carry solid white edge lines just inside the kerb. Wide roads are marked as multi-lane:
a solid double centre line and dashed lane lines each side. Two-lane roads keep their dashed centre line.

## Decision

- `RoadMarkingPlan` (pure) turns a road's width into painted lines. The map data holds a width, not a lane count or
  a one-way flag, so lanes are derived (≈ 3.4 m each): 4+ lanes get a double centre line and dashed lane lines;
  roads of 8 m and up get edge lines. Landside laneways under 8 m keep the single dashed centre line.
- `AirsideAdelaideRoads.BuildLaneMarkingMesh` walks each road once per line, on a run offset sideways
  (`OffsetRun`, mitred at bends), skipping the operational core as before. The mesh is 120,000 vertices for the
  whole area.

## Assessment of the rest of the road and building request

Checked before building: the 78 airport buildings already have facade and roof detail (ADR 0124: parapets,
window bands, hangar doors, roof monitors, plant, a glazed tower cab), and the suburbs already have hipped roofs,
an Adelaide wall palette and satellite-matched roofs (ADR 0159). The current OSM extract of ~4,400 buildings near
the airport has almost no roof or material tags (`roof:colour` on 16, `building:levels` on 493), so more
"realism" cannot come from data; it would be new procedural art (gabled and barrel hangar roofs, houses'
windows) that needs looking at in a running game before choosing. Left for Bailey to direct once the fence,
markings and labels have been seen.

## Not changed

Road geometry and widths, junctions, one-way flags (not in the data), street furniture.

## Migration

None.
