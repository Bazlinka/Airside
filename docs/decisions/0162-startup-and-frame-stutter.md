# 0162 — Startup and frame stutter

Date: 28 September 2026. Author: Cursor, after Bailey said the rebuilt game stutters a lot on open.

## Context

The player built from `a2d305cc` (lineup steering plus the new Adelaide surroundings) printed its
identity line and then sat. That build was the first time the 4096 satellite, the suburb meshes and
the Hills ring ran inside Unity. The player log also said punctual shadow maps had been scaled down
so six of them would fit in the 2048 atlas.

SSAO stays full resolution (ADR 0101). Half-resolution ambient occlusion made the field look flat,
and this pass does not change that.

## Decision

- **No main-thread texture compress.** `AirsideArtTextures.Load` no longer calls
  `Texture2D.Compress` on the 4096 satellite. That encode ran on the main thread inside Awake,
  before the first interactive frame. The image stays uncompressed, about 85 MB of VRAM with mips.
  This replaces the memory bullet in ADR 0157.
- **Suburbs are built across frames.** Houses and trees are no longer all meshed inside Awake.
  Each slice is a few milliseconds, then one kilometre tile is uploaded per frame, so the airfield
  appears first and the suburbs fill in. The suburb root stays out of `StaticBatchingUtility.Combine`.
- **World shaders supply their own depth and normals.** `Airside/AdelaideGround`,
  `Airside/Surroundings` and `Airside/SuburbBuildings` had no DepthNormals pass and fell back to
  URP Lit. SSAO's depth-normals prepass was therefore redrawing the airfield, the coastal plain,
  the roads and the suburbs with the full Lit shader every frame, and compiling that shader the
  first time they were seen. Each shader now has a cheap DepthOnly and DepthNormals pass, and the
  Lit fallback is off.
- **Suburb triangles do not sample sun shadows.** They already did not cast. They also no longer
  receive: the shader drops the cascade variants and `GetMainLight` is sampled without a shadow
  coordinate. The sun still shadows the airfield and the plain.
- **Apron floods do not cast shadows.** The four corner spots were intensity 0.05 and still
  `LightShadows.Soft`, which is what pushed the shadow atlas over its budget. They stay as fill
  light. The sun still shadows the apron. Landing lamps still cast soft shadows only at night
  (ADR 0101).
- **Dynamic batching is off.** The SRP batcher is on. Dynamic batching was also on, so every frame
  walked renderers on the CPU and split batches the SRP batcher had already made. The player
  pipeline apply and `PC_RPAsset` now leave it off. Mobile was already off.

## What stays

Frame pacing (60 focused, 30 unfocused), full-resolution SSAO, and night-only landing-lamp shadows.
Reflection probes stay scripted and time-sliced per face; they are not re-rendered every frame.

## Evidence

Code review of the load path, the three world shaders, apron light setup, and the player log from
the `a2d305cc` session (identity line, then the six-shadow-map atlas warning, then shutdown).
Filtered EditMode for the static-batch exclusion is the check to run with this change.
