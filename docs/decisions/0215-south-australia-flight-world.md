# ADR 0215 — Streamed South Australian cockpit world

Date: 2026-10-01. Status: implemented candidate; native/visual acceptance pending.

Bailey requested a separate branch expanding the Adelaide map enough for cockpit
flights across South Australia, with loading transitions acceptable if needed.

Keep authoritative flight timing and saved coordinates unchanged. Draw the watched
regional journey beyond the local visibility boundary, using the existing great
circle/enroute profile and Adelaide arrival track, with modest mapped strips for
regional approach/rollout and return departure. The route map shares this track.

Use a compact geographic Copernicus DEM and the already approved Natural Earth
coastline. Stream a fixed 7×7 window of 16 km meshes around the aircraft, at one
new mesh per frame. Keep the existing Adelaide meshes and satellite imagery.
Use a double precision geographic frame with an 8 km render origin beyond 80 km.
Explicitly register imported data, derivative format, licence and fallback.

Reason: increasing a single detailed scene to state size scales mesh/actor memory
with area and loses cockpit transform precision. This implementation bounds terrain
work and preserves authored airport detail. It does not prove frame rate: native
licensing and real-player gates must be resolved before merge acceptance.

Affected: presentation/world rendering, regional map tracking, cockpit availability,
terrain shaders and data generators. Migration: none; save and simulation unchanged.
Limits: SF34 cockpit inherited from main; simplified remote strips/turnaround;
MGB threshold estimation; interstate coverage later; dynamic local actors and
weather must be reviewed against origin changes in a packaged game.
