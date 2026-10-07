# Clean-image pass: full-res SSAO, depth range, mip bias, 16x aniso

- **Date:** 2026-10-07
- **Decision:** Four settings-only changes aimed at flicker, shimmer and blocky/soft edges:
  1. `PC_Renderer` SSAO back to full resolution (`Downsample` 1 → 0, reverting ADR 0246 item 1; ADR 0101 already
     found half-res AO reads flat and blocky on Retina), with `Samples` High → Medium (12 → 8) to claw back cost.
  2. `AirsideCameraFeel.NearClip` grows as 0.2 % of the orbit distance (4.8 m at the 2.4 km overview, 0.3 m at the
     18 m closest follow); the old 0.3 m held to 3 km, a 1 : 100,000 near/far ratio that z-fights coplanar ground layers.
  3. Ground texture `mipMapBias` −0.28/−0.12 → 0 (negative bias samples finer mips than the footprint, which crawls).
  4. `AirsideRuntimeQuality.AnisoLevel` 8 → 16 to keep oblique ground and art textures sharp without the bias.
- **Reason:** Reported flicker, pixelation and wavy edges. Each item targets a known cause; none is measured.
- **Affected systems:** `PC_Renderer.asset`, `AirsideCameraFeel`, `AirsideAdelaideGroundMesh`, `AirsideRuntimeQuality`, `CameraFeelTests`.
- **Migration impact:** none (no save or simulation change). Each item is a one-line revert.
- **Verification:** NOT verified in Unity (cloud session, headless checks only). **Next:** capture overview and follow at
  day/dusk/night; compare marking/road flicker, AO softness and frame time against `main`. Startup time was not changed or measured.
