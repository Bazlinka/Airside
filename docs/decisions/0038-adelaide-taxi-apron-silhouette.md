# 0038 — YPAD denser taxi / apron silhouette (A + exits + pads)

**Date:** 2026-09-11
**Status:** Accepted
**Decision owner:** Cursor (Bailey: keep going on airfield realism)

## Decision

1. **Bare Adelaide pavement grows denser, still presentation-only.**
   On top of ADR 0036/0037 (05/23, 12/30, F, D/E, fillets, perimeter fence):
   - **Taxiway A** — parallel spine ~105 m north of F (terminal side)
   - **D2 / E2** — inner runway↔F exits at ±550 m
   - **A↔F links** at ±300 / ±900 m
   - **Terminal apron pad** north of A and **west of 12/30** (concrete, empty —
     no buildings). Pad must keep ≥ 60 m clearance from both runway strips;
     regression via `TerminalApronClearanceFromRunways`.
   - **RFDS apron pad** south of 05/23 near the 05 end (same clearance rule)
   - Apron entry stubs from A into the terminal pad
   - Matching Code C/E fillets and sealed shoulders

2. **Layout stays in pure constants** (`AirsideAdelaidePavement`). Distance /
   contains helpers and the ops plateau cover the new pads. Simulation taxi
   topology, `SkipGroundTaxi`, saves and the circuit path remain on ADR
   0032/0033/0036.

3. **No buildings.** Apron pads are empty slabs sized to the DAP silhouette so
   the field reads as Adelaide airside without committing to terminal geometry.

## Reason

Bailey asked to keep going after fillets + fence. The next readable step is a
denser taxi/apron silhouette (option b), not sim wiring or buildings yet.

## Affected systems

- Presentation: `AirsideAdelaidePavement`, `AirsidePrototype` bare taxi builder
- Tests: `AdelaidePavementTests`
- Docs: `GAME.md`, `CHANGELOG.md`

## Migration impact

None for saves.
