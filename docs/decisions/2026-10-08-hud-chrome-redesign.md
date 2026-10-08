# HUD chrome redesign (stage 1)

- **Date:** 2026-10-08
- **Decision:** Redesign the persistent HUD chrome: the status capsule hugs its four readouts instead of spanning the window; Overview/Radar/Tower/Menu become a separate floating group at the top right; the rail is wider (72 pt) with larger icons and a filled aqua selected state; the milestone card gets a count chip and progress bar; panels get a brighter edge and top lip. The Glass Cockpit palette is richer: Aqua `#7CD6D0`, Amber `#F2C14B` (approved Safety Yellow), Go green `#6FCB8E`, Warn red `#F0786A`, deeper glass `#0E1620`/`#18242F`.
- **Reason:** Bailey reported the HUD was dark, flat, cluttered, generic and hard to read. The full-width bar left a large empty middle and the muted pastels gave no hierarchy.
- **Affected systems:** `HudShell`/`HudShellPainter`, `AirsidePalette`, `AirsideTheme.DrawGlass/DrawCard`, hit-test panels in `AirsidePrototype.Airline.cs`, `scripts/render-hud-mockups.py` palette mirror. `HudShellLayout` rectangles are unchanged (the capsule box is still full width); only what is painted and registered as clickable HUD changed.
- **Migration impact:** none (presentation only). Later stages: Operations/Fleet/Contracts/Career pages, flight-view HUD.
- **Status:** headless draw-list renders and HUD layout tests checked; Unity appearance unverified.
