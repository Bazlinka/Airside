# 0057 — HUD workspaces on a shared draw list

Date: 18 September 2026. Requested by Bailey against five concept references
(`work/hud-concepts/hud-{overview,operations,map,fleet,contracts}-v01.png`, in
the git-ignored scratch folder, so they are reproduced from the same batch in
`docs/art/` when needed). The references define layout, information hierarchy
and interaction model. They are explicitly **not** permission to hardcode the
example values: airline name, aircraft, money, reliability, preparation
progress, routes, contracts and market timers must all come from live game
state.

## Decision

1. **The persistent shell is identical on every page.** A slim top bar with the
   airline, Adelaide time, funds, reliability and tier, and the four workspace
   tabs. The current-objective card joins the shell: same place on the overview
   and on all four workspaces. A window too narrow for a usable workspace beside
   it gives the width to the workspace instead.

2. **Only one major workspace is open at a time** (unchanged from ADR 0053).

3. **Layout and copy are decided by UnityEngine-free painters.** `HudShell` and
   `{Operations,RouteMap,Fleet,Contracts}Workspace` build a model from live
   simulation and career state and emit a `HudDrawList` — a flat list of
   rectangles, text, bars, buttons and hotspots in virtual HUD points.
   `HudPainter` is the only Unity code between a workspace and the screen: it
   rasterises the list through IMGUI and reports the action id that was clicked.

### Why a draw list

Before this, each panel hand-placed IMGUI rects inside a `MonoBehaviour`, so
nothing about the HUD could be checked without a Mac Unity editor. Splitting the
description of a surface from its rasterisation buys three things:

- the layout contract (fits, never overlaps, columns in order, every control has
  a dispatchable action) is covered by `scripts/test-domain.sh`;
- `scripts/hud-mockup` can play a real headless airline and dump the same draw
  lists, which `scripts/render-hud-mockups.py` rasterises offline — so a
  workspace can be compared against its reference without an editor;
- the four pages cannot drift apart, because they share one shell, one palette
  and one set of button and caption treatments.

The offline renderer is not a Unity emulator: IMGUI's glyph metrics differ from
DejaVu's, so text wraps at slightly different points. It is exact about geometry,
colour, ordering and text content, which is what the layout work is about.

## Palette

`AirsidePalette` holds the approved hexes from
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` as plain strings; `AirsideTheme`
parses them. `CoastalBlueStrong` (`#2E86B0`) and `CoastalBlueDeep` (`#101C24`)
are value/saturation steps of the approved Coastal Blue, not new hues: a filled
primary action and a map well both need to separate from a Runway Ink panel.

## What each workspace must read from live state

| Workspace | Live source |
|---|---|
| Operations | `AirlineOperations.Fleet`, `FlightBoard`, `AircraftStatus`, `DeparturePrep`, `RunwayWeather` |
| Map | `CanOperate` (range + `RouteAccess` band), `FlightEconomics.DispatchCost` / `FlightPay`, `LegTiming` |
| Fleet | fleet roster, `RouteAccess.Ceiling`, `CompletedTrips`, `AircraftAcquisition` gates and prices, free stands |
| Contracts | `CareerState.ActiveContract`, `ContractMarket.At` / `WindowEnd`, `RouteContractDefinition` payments and penalties |

## Invariants this must not break

- Nothing in Presentation decides state. Every button routes through an
  `AirlineOperations` command, which stays free to refuse it.
- A control is only drawn as actionable when the command behind it would be
  accepted — a locked purchase, a completed contract or an unplannable route is
  drawn with its real reason instead.
- No maintenance, wear or upgrade economy was invented for the Fleet page; it
  shows only facts the simulation already holds.
- Save schema, economy and simulation timing are untouched.

## What was removed

The Hangar's "Aircraft types" catalogue tab (thumbnails and dimensions per type)
and the always-visible fleet roster sidebar, which had already been dead code
since ADR 0053 replaced it with the selected-aircraft card. The purchase
information that tab carried — price, gates, route band, delivery — now lives in
the Fleet workspace's aircraft market. If the type catalogue is wanted back it
should return as its own surface, not as a second tab on Fleet.
