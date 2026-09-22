# 0104 — Desktop HUD interface hierarchy

Date: 22 September 2026. Bailey approved a presentation-only redesign against
four supplied interface references. The existing workspaces contained the right
live information and controls, but gave too many rows and sections equal visual
weight.

## Decision

Keep the current IMGUI/shared-draw-list architecture and restructure its visual
hierarchy rather than replacing it or changing game state.

- The persistent objective becomes **Today's Priority**: one dominant live action,
  a safety-yellow objective rail and the existing campaign/day progress beneath it.
- Operations becomes **Live Apron**. It opens on the current player commitment,
  ranks that flight and real exceptions before airport context, shows at most five
  immediate movements, and gives the selected aircraft a horizontal
  Fuel → Catering → Baggage → Boarding timeline.
- Route Map becomes a deliberate planning desk: the map owns most of the surface,
  opens on a real operable destination, draws only live/career route context, and
  places one destination dossier and one primary plan/update action at right.
- Fleet uses a compact roster beside one selected-aircraft operational detail. The
  market exposes exactly three real, career-gated offers at a time.
- Career leads with the four real base capability stages (Starter, Regional,
  Jet-gate, International), one next live milestone, and a restrained strip of
  reached achievements.
- Runway Ink surfaces remain translucent so the miniature airport and moving
  aircraft stay visibly behind every workspace.

Cloud white carries primary text, coastal blue selection/actions, eucalyptus
green ready/completed state, safety yellow the objective and attention state, and
muted red delays/cancellation only. All labels remain Unity-rendered text and all
identities remain fictional game identities.

## Affected systems

`HudDraw`, `HudShell`, `OperationsWorkspace`, `RouteMapWorkspace`,
`FleetWorkspace`, `StatsWorkspace`, and the presentation-only workspace opening
defaults in `AirsidePrototype`. Layout and painter tests cover the hierarchy and
the existing constrained viewport matrix.

## Migration impact

None. No simulation rule, command, timing, economy value, persisted field, save
version, asset, data source, or external identity changes. Selecting the first
live priority/aircraft/destination on workspace open is transient presentation
state; commands still route through `AirlineOperations` and may be refused by the
same rules as before.

## Guardrails

- Never manufacture a metric, chart, route, aircraft state or destination fact
  for visual fullness.
- Do not turn Operations back into a dense full-airport spreadsheet; the complete
  live movement collection remains available to the model while the apron lens
  shows only the immediate decision and context.
- Do not add raster text, real-world logos, real airline branding or decorative
  corporate dashboard furniture.
- Preserve current controls, responsive layout contracts and the visible 3D
  airport beneath the shell.

## Evidence

- HUD/layout EditMode suite: 65/65.
- Packaged Mac build captured and inspected at 1225×768 and 800×600 for
  Operations, Map, Fleet and Career (`work/hud-*-redesign*.png`).
- Complete Unity EditMode: 979/987. The eight failures are the exact pre-existing
  main apron-density/schedule set already recorded in `GAME.md`
  (RegionalCarriers ×3, AdelaidePavement, AirlineOperations, AirlineSoak,
  AirportCurfew and GroundSeparation); no HUD or constrained-layout test failed.
