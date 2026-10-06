# 0246 — Render cost trim: SSAO downsample, MSAA budget, shadow cascades

- **Date:** 2026-10-07
- **Decision:** Three settings-only GPU savings, chosen to be easy to revert:
  1. `PC_Renderer` SSAO `Downsample` 0 → 1 (half-resolution AO; intensity 0.55 hides the softness).
  2. `HighMsaaPixelBudget` 5.0 M → 2.5 M px. 1080p keeps 4x MSAA; 1440p and above use 2x MSAA
     with the existing SMAA High pass.
  3. High ladder shadows: 4 cascades / 140 m → 3 cascades / 110 m (`PC_RPAsset` and
     `AirsideRuntimeQuality` kept in step). Medium ladder unchanged.
- **Reason:** SSAO was full resolution (ADR 0101 kept it), MSAA 4x stacked with SMAA, and one
  shadow-caster pass over the whole scene per cascade. Recorded fps was 37–60 in weather.
- **Affected systems:** `AirsideRuntimeQuality`, `PC_Renderer.asset`, `PC_RPAsset.asset`,
  `PresentationLayoutTests.RuntimeQuality_HighKeepsDocumentedMsaaAndAddsMediumLadder`.
- **Migration impact:** none (no save or simulation change).
- **Verification:** NOT verified. No Unity editor or .NET SDK on the authoring machine; the
  expected saving is unmeasured. **Next:** capture overview/follow cameras at day/dusk/night
  before and after (`scripts/capture-game.sh`) and compare frame time; revert any item that
  shows visible shimmer, AO banding or distant shadow pop-in.
