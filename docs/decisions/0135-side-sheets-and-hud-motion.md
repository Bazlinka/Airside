# 0135 — Side sheets over the airport, and HUD motion

Date: 27 September 2026. Author: Claude, at Bailey's request ("do all of them").

## Context

Every workspace covered the whole airport with one flat sheet, so the game disappeared whenever
you planned anything. Nothing on the HUD moved: panels popped in, and buttons didn't react to the
pointer.

## Decision

- **Side sheets (`HudShell.SideSheet`).** Fleet, Contracts and the Airline/Career pages open as a
  sheet anchored right.
  - The sheet is about two-thirds of the workspace, and never narrower than 900 px.
  - The live airport stays in view between the rail and the sheet.
  - Ops and the Map keep the full width: the board and the map need it.
  - Workspaces under 1,150 px wide keep the full width. Every layout was already tested at that
    size (the 1024-pixel viewport).
- **Fitting the narrower sheet.**
  - Contract cards narrow their price column below 640 px. Long offer titles shrink to fit (down
    to 12 pt). Chips wrap to a second row instead of dropping the deadline.
  - The shared lock notice uses its short form when the full one won't fit.
  - The roadmap title and the recent-flight notes shrink to fit, via the shared
    `HudShell.FitFontSize` helper.
- **Motion (`HudPainter`).**
  - A newly opened workspace slides 28 px in from the right over 0.18 s (`HudShell.SheetEntrance`).
    The painter's `Offset` shifts the drawing and its click areas together through `GUI.matrix`.
  - An enabled button lifts 1.5 px with a soft aqua glow under the pointer.
  - Clickable rows and cards get a faint wash under the pointer.

## Verification

- `SideSheetTests` covers:
  - the geometry, including small windows keeping the full width;
  - the slide-in settling;
  - fit-to-width shrinking only what doesn't fit;
  - Fleet, Contracts and Stats drawing nothing outside the sheet at every supported window size,
    plus 1920×1080 and 2560×1440.
- Offline renders of the four sheet pages are updated in `docs/testing/hud-game-feel-2026-09-27/`.
- The headless suite passes; the type-check is clean.
- Mac checks:
  - the airport shows beside the sheet, and clicking an aircraft there still selects it;
  - the slide-in feels quick, not laggy;
  - hover lift on buttons;
  - no click lands in the wrong place during the slide.
