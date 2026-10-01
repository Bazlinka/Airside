# 0218 — Coastal dune scrub on West Beach sand

Date: 2026-10-01 · Owner: Cursor

**Decision:** place deterministic low multi-lobe tea-tree/scrub bushes on OSM
`Kind.Sand` cells in the West Beach dune band (28–150 m inland of the OSM
coast), via `AdelaideDuneScrubPlacement` + `AdelaideDuneScrubGeometry`. Drawn
into the road props sink. Cap ≤ 250 bushes, ≤ 16 tris each. No new mesh assets.
Does not use VEG-002 (greybox kit path stays unchanged).

**Reason:** Phase 2e — dunes already have height berms (ADR 0203) but read bare
without scrub clumps.

**Affected systems:** road props mesh; `AdelaideCoastLandform.CoastDistanceMetres`.
**Migration impact:** none. Reuses ODbL land cover + coastline.
