# Coherent world surface lighting

Date: 2026-10-08. Task #595. Owner: Codex / Bazlinka.

Bailey identified mismatched brightness/colour across roads, runway, apron and
terrain. Native baseline views confirmed near-black runway surfaces against
pale land, washed-out apron and bright road overlays.

Use one URP PBR lighting function for pavement, airport ground and surrounding/
far/flight terrain. It supplies matching diffuse energy, sky probes, shadows,
local lights, Forward+ and screen-space occlusion. Retain each surface's normal,
roughness, original texture resolution and geographic coordinates.

Vertex palettes already stored linear values; in this Gamma project convert them
back to sRGB when blending with texture/material colour. Explicit vertex surfaces
keep roads and solid props out of the satellite/airport-edge blend and water sheen.
The previous alpha-based exclusion did not exclude the airport-edge overlay.

Pavement scans supply relative grain around the authored BaseColor rather than
multiplying that colour by a second dark scan mean. Asphalt and concrete retain
separate material values; matching lighting does not make them identical.

Lower noon sun from 2.05 to 1.25, use a restrained neutral daylight sky/equator,
and remove extra noon exposure/saturation/contrast punch. Night sun, ambient
floor and exposure lift remain unchanged. Shared terrain lighting now also sees
local lights instead of only the main sun. Water is rough enough to avoid an
oversized smooth reflection across the whole Gulf.

Keep Gamma colour space and existing save/simulation/airport geometry. No assets
are redownloaded. Always-included world shaders compile the shared include into
the build; no runtime shader/source download is required.

Acceptance: controlled native before/after overview and apron views at day,
dusk and night; equal-albedo offscreen render regression; neutral day/night
exposure regression; shader compilation and existing road geometry checks.
These are editor renders, not a packaged player performance or live night-lamp
acceptance. Full Mac rebuild/soak deliberately deferred under Bailey's request
for a quick scoped visual pass.
