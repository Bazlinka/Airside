# 0195 — Sky traffic drawn at readable speed

Date: 30 September 2026. Author: Cursor, finishing the unfinished cloud run
"Aircraft speed discrepancy" after Bailey's report.

## Context

Bailey: aircraft that look like they are flying to other airports move in very slight
intervals — slowly — when they should be moving faster. Night-sky screenshots showed
authored and fleet sky traffic crawling.

Two presentation bugs caused it:

1. **Radial compression.** `SkyTraffic` mapped a **260 km** true disc into a **7.5 km**
   draw radius (~1/168). A Melbourne–Adelaide cruise closed at about **1 m/s** on screen,
   then snapped to real speed when it entered the near field. `LiveTraffic` had the same
   squeeze past 5 km, including a height squash that made distant jets crawl under a
   compressed ceiling.
2. **One-hertz stepping.** Fleet airborne sky legs used
   `new SimulationTime((long)_preciseTime)`, so positions updated once per simulated
   second instead of every frame.

Secondary defects found while measuring:

- Opposite-direction corridor callsigns stepped by one (`QF7` slot 1 → `QF8`), so
  outbound and inbound shared a `_skyViews` model and audio key.
- Arrivals already on extended final were still drawn as a sky copy as well as the
  approach aircraft (ADR 0142 owns that leg).

## Decision

- Draw sky traffic **1:1 out to 12 km**, ease **12–70 km** true onto a **26 km** draw
  radius (inside the bare-field 30 km far clip), and cull beyond 70 km. Authored
  corridors are only pairs that actually cross that region (PER–SYD, PER–CBR, MEL–DXB).
- Match live traffic to the same near field, draw radius and cull; keep true height
  (no distance height squeeze).
- Place en-route progress with `EnrouteProfile.DistanceFractionAt` so climb and descent
  match the route map instead of a flat time share.
- Fill fleet airborne sky traffic from the precise presentation clock, and skip inbounds
  already inside `ArrivalApproach.ShowMetres`.
- Step corridor callsign numbers by two so opposite-direction services never collide.

Simulation timing, reservations and saves are unchanged. The live feed still fetches
250 NM for the Route Map; only the 3D sky cull shrinks.

## Verification

Headless Domain/Simulation tests cover corridor closest-approach, callsign uniqueness,
near-field 1:1 projection, a minimum drawn closing speed, live cull/height, precise-time
fleet sky advance, and final-handover skip. Playtest: night overview with authored sky
traffic visible and live feed off — overflights should cruise, not crawl.
