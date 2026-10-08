# World lighting pass — 2026-10-08

Task #595. Main base `347ca644`. Decision: `2026-10-08-coherent-world-lighting.md`.

Controlled native Unity before/after views use the real Adelaide ground,
pavement and road meshes. `before-day-*` shows the original lighting/grade;
`after-day-*`, `after-dusk-*`, `after-night-*` use the shared surface treatment.
Day sun/ambient values are taken from the respective runtime pass; one fixed
camera per view makes the comparison repeatable. The fixture uses a flat sky
probe, no fog, no airport lights/aircraft and a solid background; it is not a
live player screenshot or an exact astronomical/Trilight scene reproduction.

The first unified render still washed out the land. Final noon sun is 1.25
(previously 2.05), with a restrained neutral sky and no extra noon EV/saturation
punch. The final apron retains joints and stand detail; runway, roads and land
now use the same diffuse/specular/light/shadow calculation. Water roughness
avoids a broad mirror-like Gulf reflection. Existing satellite resolution and
photographed detail/shading remain a limitation; this pass does not invent
sub-metre scenery from aerial pixels.

Native graphics-on focused suite: **18 passed, zero failures**, 4.57 seconds.
It covers rendered brightness for equal-albedo terrain/road/pavement, native
shader compilation, neutral daytime grading/retained night exposure, existing
road geometry and previous sourced-visual regressions. The offscreen render
check explicitly skips a null graphics device; a headless editor cannot prove
pixel brightness. `native-results.xml` records the actual graphics-on run.

Asset audit: 1876 unique GUIDs, 407 matching runtime mirrors, 70 character
materials. All world shaders were already always included; the shared HLSL
include compiles into them. No downloaded assets or runtime texture changes.

Full suite and packaged Mac build/live performance soak deliberately deferred
under Bailey's request to keep this visual work moving. Night fixture includes
no lamps; actual flood/runway-light pools, weather transitions and flight
performance need a packaged review. The existing night sun/ambient/exposure
floor remains; the common terrain path now receives local lights too.
