# South Australia continuous flight world

Requested by Bailey, 1 October 2026. Branch: `feature/south-australia-flight-world`.

## Player outcome

Remain in the supported aircraft's cockpit through Adelaide departure, regional
cruise, approach, touchdown and rollout. Enter a distant flight's cockpit from its
selection card. Retain the detailed Adelaide field; use modest coloured terrain
and a simple mapped runway at the regional end. South Australia is the first
coverage target; full interstate and international journeys remain later work.

## Scope and invariants

Presentation only: `FlightWorldGrid`, `FlightWorldHeights`, `RegionalRunways`,
`RegionalFlightPath`, `AirsideFlightWorldTerrain` and the prototype/camera hooks.
Map tracking uses the regional 3D track. Satellite UVs retain geographic anchoring
when render coordinates move. Domain, simulation timing, reservations, economy,
saves and aircraft cockpit interiors keep their existing owners.

This branch inherits SF34 cockpit support from main. Other aircraft interiors
are separate tasks. Remote airports have a mapped primary strip, without gates,
taxiways, buildings or remote-airport traffic/resource simulation. The destination
turnaround reverses runway direction for the return; this is a spectator fallback,
not a wind-based runway selection or a validated aircraft performance model.

## Data and loading

- Geographic coverage: 128–142 E, 39–25 S (SA plus surrounding padding; BHQ included).
- Copernicus GLO-90 COG averages, downsampled to 0.02 degrees (about 2 km).
  Runtime SATG file: 982,842 bytes; no runtime network calls or service keys.
- Existing MAP-001 Natural Earth coastline determines land/sea. Adelaide keeps its
  existing detailed terrain. Regional height fields flatten to sourced airport
  elevation around the mapped strip, to avoid the coarse DEM burying the runway.
- OurAirports primary-runway thresholds, widths and airport elevation are recorded
  in `docs/data/sa-flight-runways-v01.json`. MGB lacks thresholds in that source:
  its endpoints are explicitly estimated from airport centre, length and heading.
- At most 49 resident tiles, 16 km each, 1 km mesh cells, 28,322 triangles maximum
  before excluding the existing Adelaide terrain. One new tile is built per frame;
  retained tiles remain visible while new tiles arrive. Initial distant selection
  fills the landscape over up to 49 frames. No scene-loading screen is introduced.
- Above 80 km displacement, an 8 km snapped double-precision origin keeps the
  watched aircraft within 4 km of render origin in each horizontal axis. The
  airport root, ordinary fleet, sky/live traffic and satellite coordinates shift;
  speed readout corrects its prior sample for the shift.

## Acceptance and verification

Automated: signed tile indexing, fixed resident cap after long travel, origin
precision, state coverage, parser corruption/out-of-bounds handling, interpolation,
shipped data bounds, destination rollout on runway and matching return start.
Full Presentation source compilation is a supplementary check, not Unity testing.

Required packaged playtest (still open):

1. SF34 ADL→KGC and ADL→CPD, plus return; keep cockpit active through all phase
   boundaries and origin changes. Check gear, heading, instruments and engine audio.
2. Enter an already distant SF34 through My Flights/map selection, then Esc/R.
   Airport scene, sky, weather and normal camera must return correctly.
3. Inspect the ±96 km landscape join from both directions and the first distant
   selection while terrain fills. Check all regional strips and MGB estimation.
4. Verify local airport actors (GSE, boarding, aerobridges, clouds/weather) are not
   incorrectly visible near the distant render origin. Their individual dynamic
   coordinate setters need runtime inspection; this branch is not visually accepted.
5. Compare 10-minute graphics-on cockpit runs with main, including frame-time
   percentiles, terrain-build peaks, memory and resident tile count. The previous
   cockpit stall remains unresolved; bounded tile counts do not prove good FPS.

Current evidence and blockers: `docs/testing/sa-flight-world-2026-10-01/RESULTS.md`.
