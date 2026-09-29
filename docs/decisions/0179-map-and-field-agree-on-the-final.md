# 0179 — The route map and the field agree on the final

Date: 29 September 2026. Author: Claude, from Bailey's report: "planes on approach don't match up
to the map option, and stutter across the screen before coming into view on final".

## Player-visible outcome

An arrival is in the same place on the Australia map, the field mini-map and the 3D view. It no
longer appears off the mini-map until it is inside the window, and it cannot leap across the sky.

## Cause found

- The field draws an arrival down the extended final at **approach speed** for its last 32 km
  (ADR 0142/0147: distance out = approach speed × time to clearance). The route map ran the same
  leg at **descent-profile speed** along the great circle, so at the moment the field first
  showed an aircraft the map had it tens of kilometres further out, and on a different bearing.
- The field mini-map dropped any aircraft outside its window, so a 32 km final was invisible on it.
- Nothing bounded how far the drawn arrival could move in a frame when the path under it changed.

The frame-time cost of the tower's landing check was measured (0.07 ms a call on average) and is
not the cause. A wind-driven runway change on the final was probed over a seeded day and did not
occur; the slew limit below still covers it.

## Decision

- `ArrivalMapTrack` (Presentation): the last `ArrivalApproach.ShowMetres` of an inbound leg on the
  map are flown at approach speed, the rest on the descent profile, so distance out is continuous
  and equals what the field draws. The map's ground track follows the field's final and bends onto
  the great circle over 40 km.
- `ArrivalMapTrack.FinalWorldXZ` is the single ground-track function; the field's
  `ArrivalFinalWorld` calls it.
- The drawn arrival may move at most `ArrivalApproach.PoseSlewFactor` (2.5×) approach speed per
  simulated second. Real changes in the path are followed, never jumped.
- Off-map aircraft on the field mini-map pin to the edge they are approaching from
  (`FieldMiniMap.EdgePoint`).

## Not changed

Simulation timing, clearance and separation. The route-map altitude and speed readouts still come
from the descent profile inside the last 32 km. A queue delay beyond the join adds distance on the
field that the map (which only tracks the Inbound state) does not show.

## Migration

None. Presentation only.
