# 0201 — Stand oil stains (visual overhaul Phase 1)

Date: 2026-09-30 · Owner: Cursor

**Decision:** every regional bay and terminal gate gets seeded oil/fuel stains
(`StandOilStains`) drawn as two combined apron meshes (old vs fresh). Ground
macro variation and mow-stripe contrast are raised slightly, and the paved/grass
shoulder softens earlier so the edge does not hard-cut.

**Reason:** Phase 1 airside ground truth — clean concrete aprons read as brand-new
boxes; real stands show drip under gear, APU and GPU. Soft shoulders and stronger
macro/mow detail finish the Phase 1 ground-albedo slice without new texture assets.

**Affected systems:** presentation pavement build and Adelaide ground weights/shader
uniforms only.

**Migration impact:** none. Simulation, stands, routes, reservations and saves are
unchanged.
