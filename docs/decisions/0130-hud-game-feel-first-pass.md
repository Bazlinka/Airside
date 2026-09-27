# 0130 — Making the HUD feel like a game (first pass)

Date: 27 September 2026. Author: Claude, at Bailey's request. Bailey asked whether the HUD looks
ugly, "like a game or…".

## Context

The HUD is clean and consistent, but the workspaces read like an admin dashboard:
- rows of sentences, with no pictures;
- one kind of box for everything;
- the same warning printed on every card;
- empty columns.

The aircraft thumbnails for every type were already registered in `AircraftCatalogue`, but only
the setup preview used them.

## Decision: this pass (presentation only, no simulation or save changes)

- **Ops departures board in split-flap tiles** (`OperationsWorkspacePainter.PaintFlapRow` /
  `FlapField`).
  - Fields: TIME, FLIGHT, TO/FROM city, GATE and REMARKS, one character per tile, with a hinge line.
  - Tile width follows the row. Remarks take the tone of the status (delayed in red).
  - The operator and type sit in small print under each row.
  - Also fixed: the day timeline's "NOW" was clipped, and turnaround steps now print the percent
    under the name.
- **Fleet shows the aircraft.**
  - The detail pane opens with the type's picture on a stage in the airline's livery, when the pane
    is tall enough.
  - Each fact leads with an icon.
  - Market cards carry the aircraft picture.
  - A lock that every offer shares (a full base) is said once, beside the caption.
- **Contract offers as game cards.**
  - A coloured kind badge: scheduled, charter, medical (red) or freight.
  - Chips for flights, aircraft and deadline.
  - The aircraft picture, the total pay in large type, and a short ACCEPT button.
  - A shared lock ("one contract at a time") is said once.
- **The Airline page's figures lead with icons.** They drop out on narrow cards. The empty right
  column now lists recent paid flights: registration, pay, and punctuality in tone, with the one
  that finished a contract marked.

Renders of this pass are in `docs/testing/hud-game-feel-2026-09-27/`.

## Next: needs the Mac build

- Workspaces as side sheets over the live airport.
- Motion: hover lift, panels sliding in, and flap tiles flipping when a value changes.
- Reward moments: money counting up, and a tier-up card.
- The flip board draws about 100 commands per row. Check the frame time in the 60 fps soak.

## Verification

- `GameFeelHudTests` covers:
  - flap tiles and truncation;
  - board place names;
  - every buyable type has a picture;
  - shared lock reasons said once, on Contracts and on the Fleet market;
  - the recent-flights list.
- `Stats_OverviewCardsFitTheirText` still passes, with the icons in place.
- 859/859 headless tests pass; the type-check is clean.
