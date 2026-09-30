# 0199 — Airside building detail (visual overhaul Phase 3d, first slice)

Date: 2026-09-30 · Owner: Claude
**Decision:** freight sheds and the fire station use the existing fitted-roof code at 0.55x rise (plant only if no roof
fits); all support/freight/fire buildings get a 0.5 m plinth; doors get lit wall packs; freight docks get bumpers and
bollards. **Reason:** flat prisms with scattered plant read as boxes. **Affected:** `BuildingDetail` and its tests only.
**Migration:** none; footprints, heights, routes and saves unchanged. Render appearance not yet reviewed on a Mac.
