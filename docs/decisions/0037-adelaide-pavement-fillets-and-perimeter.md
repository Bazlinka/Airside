# 0037 — YPAD pavement fillets + Adelaide perimeter fence

**Date:** 2026-09-11
**Status:** Accepted
**Decision owner:** Cursor (Bailey: improve taxi/runway realism; gate the airport)

## Decision

1. **Taxi / runway joins use Code C/E fillet geometry**, not sharp cube corners.
   `AirsideAdelaidePavement.AllFillets()` publishes:
   - 42 m quarter-disk fillets at Taxiway F ↔ D/E and runway ↔ D/E T-junctions
   - Semicircle end caps on Taxiway F
   - 38 m soft pad at the 05/23 × 12/30 crossing
   Runtime builds sector meshes from those specs. Sealed 3.5 m taxi shoulders
   sit beside F/D/E. Taxi paint is yellow; rubber-deposit bands darken the TDZ.

2. **Perimeter security fence follows the published 785 ha site rectangle**
   already encoded in `AirsideBareField` (3 400 × 2 309 m). Fence height is
   2.44 m mesh + 0.40 m top guard. Vehicle gates sit on the north (terminal),
   west (Tapleys), and south (Melrose) sides. No buildings.

3. **Presentation only.** Simulation taxi topology, `SkipGroundTaxi`, saves and
   the circuit path stay on ADR 0032/0033/0036. Aircraft still circuit on 05/23.

## Reason

Bailey asked for critical realism on taxi/runway curves and for an Adelaide-
identical perimeter gate with ground sizing (no buildings). Hard cube T-junctions
fail that bar; the site fence must sit on the real-metre property line, not cage
the strip.

## Affected systems

- Presentation: `AirsideAdelaidePavement`, `AirsideAdelaidePerimeter`,
  `AirsidePrototype` bare pavement / fence builders
- Tests: `AdelaidePavementTests`
- Docs: `GAME.md`, `CHANGELOG.md`

## Migration impact

None for saves.
