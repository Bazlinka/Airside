# 0075 — Authored cloud atlas instead of procedural sphere clusters

Date: 2026-09-21

## Decision

Replace every three-layer procedural sphere cluster with one camera-facing quad selected from a
4 x 4 transparent cumulus atlas. Keep seeded placement, real-wind drift, weather-cover reveal and
moving ground umbras. Add a dedicated transparent URP shader that rejects low-alpha generator fringe,
neutralises saturated edge RGB and accepts the existing day/weather tint through a property block.

Always include the shader in player builds because the runtime constructs the material by name.
Scale cloud cards to their broad-cumulus silhouette and reduce the old opaque-geometry umbra strength.

## Reason

The sphere clusters were inexpensive but visibly read as connected white balls. More procedural
lobes would increase draw/geometry cost without producing the soft irregular boundary and internal
light structure that make a cloud recognisable at strategy-game distance. A small atlas supplies
those cues with one renderer per cloud instead of three.

## Consequences

- Adelaide drops from 48 cloud renderers to 16 while retaining deterministic weather behaviour.
- The approach is a billboard approximation, not volumetric cloud simulation; close fly-throughs
  are outside this camera's intended use.
- The selected generated image and exact prompts are recorded for release review.
- Source is 1254px because image generation did not honour the requested 2048px; no fake upscale is
  shipped.

## Verification

- Domain suite: 553/553.
- Unity EditMode: 787/789, with only the same two unrelated baseline failures.
- Fresh Mac player build completed after explicitly including the custom shader.
- Forced-Overcast 1920 x 1080 capture inspected; a first build caught shader stripping and a second
  caught compressed silhouettes / over-dark umbras before the accepted capture.
