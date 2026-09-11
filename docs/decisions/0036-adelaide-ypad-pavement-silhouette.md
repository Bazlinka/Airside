# 0036 — Adelaide YPAD pavement silhouette (05/23 + 12/30 + taxi)

**Date:** 2026-09-11
**Status:** Accepted
**Decision owner:** Cursor (Bailey: implement Adelaide runway/taxi layout)

## Decision

1. **Bare Adelaide presentation grows from one strip to a YPAD silhouette.**
   The visible field keeps the authored Adelaide ground (ADR 0035) and adds:
   - **Runway 05/23** — existing 3 100 × 45 m strip, renamed from `Runway W`
   - **Runway 12/30** — published 1 652 × 45 m cross strip at **73°** to 05/23
     (magnetic 115° − 042°)
   - **Taxiway F** — parallel Code-C-width (23 m) spine on the terminal side
   - **Taxiway D / E** — exit stubs toward the 23 and 05 ends

2. **Layout lives in pure constants** (`AirsideAdelaidePavement`,
   `AirsideStripMarkings`) with no UnityEngine types. Headless tests and the
   runtime builder share one metre contract. Paint for 12/30 and taxi uses the
   same combined-mesh / paint-lift standard as 05/23.

3. **Ops plateau expands** so 12/30 and Taxiway F sit on dead-level ground
   (`PlateauHalfZ` ≈ 860 m). Dirt / grass weights sample distance to any
   silhouette pavement, not only the main strip.

4. **Simulation is unchanged.** Reservations, the 1:20 taxi graph,
   `SkipGroundTaxi`, save schema and the circuit path stay on ADR 0032/0033.
   Aircraft still circuit on 05/23 only. Using the new pavement for taxi-in /
   line-up is a **separate** topology ADR.

## Reason

Bailey asked for Adelaide-looking runways and taxiways to the same visual
standard as the current strip. A second runway is required for that silhouette;
taxi F + D/E exits are the minimum readable skeleton. Keeping sim on the
miniature avoids a save/reservation migration in this pass.

## Affected systems

- Presentation: `AirsideAdelaidePavement`, `AirsideStripMarkings`,
  `AirsideBareField` (runway name), `AirsideAdelaideGround` (plateau / distance),
  `AirsidePrototype.BuildBareAdelaidePavement`
- Tests: `AdelaidePavementTests`, `BareFieldTests` name assertion
- Docs: `GAME.md`, `CHANGELOG.md`

## Migration impact

None for saves. Presentation-only rename of the main runway GameObject
(`Runway W` → `Runway 05/23`).
