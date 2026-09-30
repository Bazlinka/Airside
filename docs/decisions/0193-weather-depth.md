# 0193 — Weather with depth

Date: 2026-09-30. Owner: Codex. Requested by Bailey.

## Task packet

Player outcome: fog settles over the airport; clouds have rounded, irregular 3D bodies that hold
up when orbiting or flying through them; overcast has a rolling ceiling; rain falls visibly in
wind at follow and overview distances. Existing sky colours, thunder, lightning flash, wetness
and runway-light reflections stay connected to the same weather sample.

Scope: `AirsidePrototype.Sky`, `.Atmosphere`, `.WeatherEffects`, three URP weather shaders,
GraphicsSettings inclusion, Unity integration tests and handoff documentation.

Decision: use sixteen-step bounded cloud density integration inside each of the existing sixteen cloud
clusters and twelve steps through one 120 m ground fog volume. Sample scene depth to stop fog at buildings/terrain.
Shade clouds by height and sun-facing density. The authored cloud atlas remains a shader fallback.
Use one dynamic mesh for 768 seeded, soft, depth-faded rain streaks near the camera rather than
hundreds of scaled cube objects. Soft shadow masks drift with clouds along the sun direction.
The overcast ceiling samples animated world-space noise so panning does not drag its texture.

Invariants: operational weather, simulation time, runway reservations, schedules and saves are
unchanged. Visual motion uses unscaled presentation time and the existing presentation wind.
No external assets, network calls or paid generation. No new settings or save migration.

Acceptance: rounded cloud bodies from overview and follow; fog visible by day/night without
obscuring the entire overhead field; clouds/rain wind alignment; dry startup; no solid rain cubes,
colliders or hundreds of rain draw calls; packaged shaders present; no shader/runtime errors.

Validation: domain precheck, full Unity EditMode, Mac build; packaged clear/cloudy/overcast/rain/
storm/fog captures plus fog at night and cloudy follow. Inspect silhouettes, fog/terrain overlap,
readability and frame statistics. Results go in `docs/testing/weather-2026-09-30/README.md`.
