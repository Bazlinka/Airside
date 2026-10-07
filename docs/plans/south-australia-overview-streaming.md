# Task packet — pan and zoom the overview across all of South Australia

Status: **proposed; the land-cover data and tile colouring are done (ADR 0250); the camera/streaming work below needs Unity to verify.**
Drafted 2026-10-07 by Claude. Approved by Bailey as the next far-map step ("drag/zoom the overview across all of South Australia,
streaming `AirsideFlightWorldTerrain` tiles in as you pan, coloured by real land cover, no new satellite imagery").

## Player-visible outcome
Zoomed far out in the overview, dragging pans well beyond Adelaide; the ground streams in under the cursor as the real state
(Eyre and Yorke peninsulas, gulfs, Flinders, Nullarbor, salt lakes) coloured by land cover, joining the Adelaide rings without a visible ring.

## What exists (read from the code, 2026-10-07)
- `AirsideFlightWorldTerrain` builds 16 km tiles (16 cells, 1 km), 49 resident (radius 3 = ±56 km), one built per frame — but is only
  ticked from the cockpit/journey position in `AirsidePrototype.FlightWorld.UpdateFlightWorld`, never from the overview camera.
- Overview pan is leashed: `AirsideCameraFeel.PanRadius(distance) = max(3800 m, distance × FarPanFractionOfDistance)` via `ClampPanCentre`.
- Far clip is `max(30 km, 2.6 × distance)`; `HorizonScale` pushes the haze past the clip; the flight material fades at 40–55 km.
- Adelaide's rings own ±96 km (outer ring 500 m cells); flight tiles tuck 12 m under and skip triangles inside 95 km.
- A floating origin (8 km steps, airfield root shifted) is used only in cockpit when the aircraft is > 80 km out.

## Proposed approach (each step independently revertible)
1. **Unlock the pan leash by zoom:** allow the orbit centre up to the covered extent (~±700 km from YPAD) only while
   `distance` ≥ a threshold (e.g. 60 km); below it the current leash applies. Pure function in `AirsideCameraFeel`, headless-testable.
2. **Tick the tiles from the camera:** when zoomed out beyond the threshold, call `Tick(centreX, centreZ, 0, 0)` from the overview
   camera's ground point. No floating origin in the overview: at ≤ 700 km float precision is ~6 cm, invisible at that zoom
   (verify there is no camera/vertex jitter; if there is, reuse the cockpit origin step).
3. **Coarser outer tiles for wide views:** 49 × 16 km tiles cover only ±56 km but a 200 km view sees ±500 km. Add a second resident
   ring of 64 km tiles with 4 km cells (same builder, parameterised) beyond the 16 km ring, same land-cover colouring (sampled at
   cell centres), with the same residency cap logic in `FlightWorldGrid`. Budget: ≤ ~100 resident tiles total, one build per frame.
4. **Hand-offs:** keep the existing ±95 km tuck under Adelaide's rings; check the horizon fade/fog values for the wider view.

## Acceptance
- Headless: leash function (clamped near, unlocked far, continuous at the threshold), tile residency cap after a long pan, second-ring
  tile indexing (signed), no tile built twice.
- Mac/Unity (required): pan from Adelaide to the Nullarbor and back at 100–400 km zoom; no hitch > a frame budget while tiles stream;
  no ring or colour step at ±96 km; shadows/post effects unchanged; frame time and memory with 100 tiles; the 24 existing native failures
  stay the only failures.

## Must remain unchanged
Simulation, saves, cockpit/flight views, Adelaide's detailed meshes and rings, near zoom behaviour (3.8 km leash).
