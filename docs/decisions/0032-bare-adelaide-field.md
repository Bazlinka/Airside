# 0032 — Bare Adelaide field: one aircraft, one runway, empty ground

**Date:** 2026-09-10
**Status:** Accepted
**Decision owner:** Bailey (product), Cursor (implementation)

## Decision

The player-visible world is only:

1. One commercial aircraft (the existing v06 turboprop).
2. One runway at **real metres**: Adelaide 05/23, **3 100 m × 45 m**.
3. Empty grass ground the size of the published Adelaide Airport site:
   **3 400 m × 2 309 m = 785.06 ha**.
4. Normal sun / fill / ambient lighting (daylight remains pinned).

Buildings, cars, signs, taxiways, apron, coast, trees, fences, hills, clouds,
birds, decorative lamps, windsock, GSE and a second aircraft are **not spawned**.
`AirsideBareField.Enabled` is the single switch. `AirsideFocusMode` readers all
derive from it.

The 1:20 miniature (`AirportLayout`, 155 m runway) stays as the simulation
coordinate system so reservations, phase timing and save data do not change.
Presentation draws the real-metre field around that operating strip.

## Reason

The previous Adelaide pass (#187) hid vehicles behind `AircraftOnly` and kept
the 1:20 toy field, so the scene was still a packed miniature: terminal, hangar,
taxiways, apron, coast, lights. Bailey asked for the opposite — a clean field
at real runway length and Adelaide ground area, with everything else gone.

## Affected systems

- Presentation: `AirsideBareField`, `AirsideFocusMode`, `AirsidePrototype.BuildAirfield`,
  `AirsideCameraController` (overview / far clip / zoom), fog densities
- Tests: `BareFieldTests`, `PresentationLayoutTests`
- Docs: `GAME.md`, `CHANGELOG.md`

Simulation, taxi graph, economy, save schema and aircraft motion curves are
unchanged.

## Migration impact

None. Presentation-only. No persisted schema change.
