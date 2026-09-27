# 0122 — Glass Cockpit HUD and title screen

Date: 26 September 2026. Author: Claude, at Bailey's request for a completely new in-game
HUD ("it should look nothing like it does now"), a redesigned opening, and the Glass Cockpit
direction Bailey chose from three options.

## Decision

The airline HUD is rebuilt as the **Glass Cockpit**:

- **Layout:** a vertical navigation rail (left), a floating status capsule (top), the
  career ring card (bottom-left), live flight tiles (top-right), the airfield radar
  (bottom-right) and the selected-aircraft card (bottom-centre). Toasts sit under the
  capsule, or in the tiles' corner while a sheet is open. A workspace opens as one glass
  sheet right of the rail and replaces the overview panels. The full-width top bar, tab
  strip and "Today's priority" card are removed.
- **Look:** translucent graphite glass (`#0E1216`) with rounded corners, a soft drop shadow
  and a faint edge; raised glass sub-cards; amber (`#FFB547`) filled pills for the one
  primary action; aqua (`#3FD0C9`) for selection, live state and progress; magenta for
  routes; green/red for good/bad. Captions are small tracked capitals; titles are title case.
  The world palette (Runway Ink, Safety Yellow …) remains the airport's art palette.
- **Primitives:** `HudDrawList` gains `Card`, `Pill`, `Ring`, `Icon`, `Image` and `Gradient`.
  `HudPainter` draws rounded shapes with IMGUI's native rounded `GUI.DrawTexture`
  overload and a few generated 9-slice textures (`AirsideTheme.RoundedTexture`), so every
  raw-IMGUI panel and button (menus, help, dev tools, away summary) inherits the glass look
  through `AirsideTheme` without per-panel rewrites. Icons are the approved Batch E/F line
  icons, turned into white masks at load and tinted.
- **Pure layers:** `HudShell.Layout`, `SelectionCardPainter`, `CareerTrackPainter`,
  `SplashPainter` and `ToastPainter` are UnityEngine-free and tested headlessly; the offline
  mockup renderer draws the same lists.
- **Career page:** the Career tab opens on a tier track (Provisional → Regional → Domestic →
  International → Established) with the current stage's goal cards and a preview of the next
  stage; the older Stats page is kept one click away as the airline profile.
- **Title screen:** the approved UI-ILL-001 dawn illustration and BRD-001 wordmark (both
  already registered and packaged, previously unused) form the opening: live clock, Continue
  with a save summary, New airline (name + livery), Options, Quit. The camera intro plays
  after the choice, as the art dissolves into the live airport.

## Reasons

The previous shell was a dense top bar plus a large right-hand panel with square framed
boxes; Bailey asked for a complete change. A rail and corner panels keep the airport centre
clear; one amber action per card keeps priorities legible; the career ring makes long-term
progression (ADR 0121) visible at all times.

## Affected systems and migration

Presentation only. No simulation, save or asset changes; no new assets. HUD layout tests
(`HudShellTests`, `PresentationLayoutTests`, `FieldMiniMapTests`) were rewritten to the new
invariants: every panel inside the window above the credit footer and pairwise apart at every
supported size, the rail and capsule always present.
