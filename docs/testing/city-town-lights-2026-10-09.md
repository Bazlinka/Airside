# City and town lights — 9 October 2026

Task #760. Original batched presentation, using registered geographic data.

## Result

- Warm varied occupied windows on existing Adelaide and regional building facades.
- Mapped OSM street lamp positions retained; inferred lamps follow eligible urban roads
  at carried 38 m spacing. Airside, tunnels, restricted/service/unpaved roads and elevated
  layers are excluded. All 16 explicit `lit=no` way IDs match the existing OSM snapshot.
- Sparse distant lights use actual built-up land cover; national fine tiles sample at
  stable 500 m intervals independently of terrain LOD, draped on the rendered triangles.
- Local windows/lamps share local terrain horizon fading; district lights use the far ring.
  Emission fades with the existing day cycle. Airport fixtures, simulation and saves retained.
- No individual lamp GameObjects or Unity point lights. Meshes/materials are owned and
  destroyed with their tile/component. No external artwork or data added.

## Native evidence

Final rendered source: **971e4f4a**, including main's sky/tower updates.
Universal Mac build: `Build Finished, Result: Success.` New shader compiled; x86_64/arm64.
Clean stamped identity verified. Final corrected nine-step native scenario **passed**,
zero runtime errors, isolated test save. Six real captured frames inspected across
13:30 / 18:30 / 21:30 and close/wide/opposite city views. Recorded copies are in
[`city-town-lights-2026-10-09/`](city-town-lights-2026-10-09/).

Earlier probe on 4b6dc402: first eight steps passed, final 42 km camera request failed
because the existing diagnostic camera caps at 10 km. No false pass recorded; corrected
scenario uses supported distances. Actual wide views motivated weaker lamp highlights
and the local horizon correction; the final wide captures were inspected again.

Focused Whyalla night probe on **c5894d60**, before local fade refinement: fresh private
soak save, accelerated 40×, reached AtDestination in ~94 real seconds with origin
64,000 / 224,000 m, 49 resident tiles and airport presentation hidden. Forward cockpit
cruise/approach/parked frames inspected, no logged runtime errors. District sources were
visible near the regional horizon. Probe intentionally stopped after destination capture,
not a completed journey or normal-speed performance pass. The local fade change does
not change regional source emission/range. Regional close facade windows were **not**
proved by these forward-looking frames.

## Checks and limits

New shader/helper/folder metadata is committed and unique. Full asset audit reports
missing metadata within the generated notification bundle; not a clean audit pass.
Headless harness discovery exceeded the bounded setup time and was stopped, with
its provisional file changes restored. No full local test suites, soaks or GPU profiles.
Diff whitespace checked after removing imported metadata's trailing spaces.

Household occupancy and unsurveyed lamp spacing are inferred, not surveyed.
No national house-footprint dataset is present: individual house detail is limited to
Adelaide and the shipped regional airport OSM snapshots (18 snapshots; 4,096-feature
runtime cap per airport). Distant built-up sources represent districts. Tiny towns can
be absent at the source land-cover resolution; do not claim exhaustive town/house coverage.
Normal-speed long journeys, regional close windows, wet/fog light pools and GPU cost
remain unverified. Existing geography/terrain detail and satellite coverage limits remain.
