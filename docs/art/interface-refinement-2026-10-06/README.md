# Airside interface refinement study

6 October 2026. Concrete refinement of Bailey's accepted maintenance/interface proposal. Design-only branch `docs/refined-interface-maintenance-20261006`.

- Open `preview.html` in a browser. Default view is the selected aircraft's maintenance journey. Navigation opens the overview, fleet, operations, contracts, career, map and settings studies.
- Use **Preview repair stage** to compare the taxi and repair inspectors. Fleet filters and maintenance selection are functional. Pause/speed and settings acknowledge choices within the study only.
- `*-1440.png` and `maintenance-1280.png` are Chromium screenshots of this HTML design study. They are not Unity/gameplay captures. The original SVG airport backdrop and sample data are illustrative, not a replacement for the game's world or real simulation projections.
- `verification.json` records browser checks at 1440×900, 1280×720, 1920×1080 and 900×720, including script errors, horizontal overflow, maintenance action visibility and core interactions.

Detailed visual tokens, all-screen direction, source-grounded maintenance phases, save migration proposal, implementation slices and acceptance criteria: `../../plans/refined-interface-and-maintenance.md`.

No external image, font, icon package, script or data was downloaded. Original inline SVG paths and HTML/CSS are project-owned; system Arial is referenced, not redistributed. No runtime assets, game source, dependencies or hosting configuration changed. The browser is a review surface; shipping UI remains the shared Unity IMGUI system unless a separate migration is approved.

The current study refines structure and visual hierarchy. Native text metrics, day/night/weather contrast over the real moving airport, camera input, audio and performance still require later owner review. Flight HUD, title/setup and return-briefing detailed previews remain future work.
