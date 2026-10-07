# 0251 — Wide overview: zoom to 450 km and stream South Australia from the camera

Status: accepted (Unity look and performance not yet verified)
Date: 2026-10-07 (increment 2 of Bailey's approved request; increment 1 is ADR 0250)

- **Decision:**
  1. `AirsideBareField.MaxOrbitDistance` 45 km → **450 km**. Far clip (2.6 × distance), near clip, horizon haze, fog and the pan leash
     (0.9 × distance) already derive from the orbit distance (ADR 0185), so they scale with it. The pointer-to-ground ray reach
     (`GroundRayReachMetres`) is raised 400 km → 1,500 km so zoom-toward-pointer still finds the ground at 450 km.
  2. `FlightWorldGrid.WideMap(distance)` is true past **60 km**. Only then does `UpdateFlightWorld` tick `AirsideFlightWorldTerrain`
     from the overview camera's focus point (no floating origin: outside the cockpit the render origin is the world origin; at
     ≤ ~700 km float precision is ~6 cm, far below a pixel at that zoom).
  3. `AirsideFlightWorldTerrain.Tick(..., wide: true)` keeps the 49-tile fine ring (16 km tiles, 1 km cells) and adds a **coarse ring of
     64 km tiles, 32 × 32 cells of 2 km, radius 5 (121 resident, ±352 km)**, built after the fine ring, one tile per frame in total,
     **40 m under** the fine tiles (so the fine tile wins where both exist), tucked under Adelaide's rings with the same rule. The
     coarse builder takes known dry land from the land-cover map and runs the costly coast test only for water. Cockpit callers
     never pass `wide`, so cockpit behaviour is unchanged, and leaving wide mode drops the coarse tiles.
- **Reason:** Bailey: "drag the map over the Adelaide Hills and it doesn't go crappy", and to drag/zoom across all of South Australia.
  The old 45 km limit never showed anything past Adelaide's ±96 km rings.
- **Affected systems:** `AirsideBareField` (one constant), `AirsideCameraController` (one constant), `FlightWorldGrid` (pure ring maths),
  `AirsideFlightWorldTerrain`, `AirsidePrototype.FlightWorld`. No save, simulation, schedule or schema change. Up to 45 km zoom nothing changes.
- **Cost (estimates, unmeasured):** up to 121 coarse tiles × 1,089 vertices ≈ 132 k vertices + the existing 14 k, ~5 MB mesh memory, built
  at one tile per frame (≈ 2 s to fill from empty); shadows off. Keyboard pan reaches 15.6 km/s at full zoom-out.
- **Evidence:** headless `FlightWorldTests` (coarse tile floor indexing on both sides of zero, exactly 121 resident, no overflow, ring
  reaches > 300 km with ≤ 2 km cells; `WideMap` false at and below the classic limit, true past 60 km); full headless suite 1842 passed /
  0 failed; the changed terrain/prototype code type-checks against Unity stubs (a deliberately injected error was caught).
- **NOT verified (needs Unity):** the look at 100–450 km (coastline blockiness at 2 km cells, a 40 m step at the join with the fine ring and
  the 96 km rings), sky/horizon/star/sun behaviour at a 1,170 km far clip, depth precision, frame time while tiles stream, the camera
  feel of dragging at 450 km, and whether any other system assumed the 45 km limit.
- **Limits:** the coarse ring is a backdrop (2 km cells, land-cover colours, no slope shading); the detail pan reaches 0.9 × distance
  from the overview, so reaching the far west means zooming out, panning, then zooming in (the leash already keeps a zoomed-in view).
- **Revert:** set `MaxOrbitDistance` back to 45,000 and `GroundRayReachMetres` to 400,000; `WideMap` then never triggers.

**Amendment 2026-10-07 (Cursor, run on the Mac):** at 450 km the camera stands ~345 km behind its focus,
outside a 5-tile ring, so the near edge showed. The coarse ring now grows with the orbit distance
(`FlightWorldGrid.CoarseRadiusFor`, 5 to 8 tiles, at most 289) and its material's horizon fade is pulled in
(`WideHorizonFade`: clear to 1.1x the orbit distance, gone before the nearest ring edge in view); cockpit callers
get the old 40/55 km fade back. Coarse and fine vertices take the mean land-cover colour of the cells round them
(point samples read as speckle). ADR 0210's baked haze was removed: it tinted only the Adelaide ±96 km ring and
showed as a pale square. Measured: tiles ≤70 ms, terrain creation 12 ms; the soak's long frame is the first render.
