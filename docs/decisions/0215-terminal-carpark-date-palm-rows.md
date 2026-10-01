# 0215 — Terminal / car-park date-palm rows

Date: 2026-10-01 · Owner: Cursor

**Decision:** place deterministic Canary Island date-palm rows along OSM
car-park edges in the Terminal 1 landside ring (`AdelaidePalmPlacement` +
`AdelaidePalmGeometry`), drawn into the existing road props sink via
`AdelaideCarParkGeometry.BuildPalms`. Procedural only — no new mesh assets.
Cap ≤ 180 palms, ≤ 36 tris each.

**Reason:** Phase 2 trees — NDVI canopy excludes the landside precinct, so
forecourt/car parks read bare. Palm rows match Adelaide Airport planting and
fill that hole without fighting suburb eucalypts.

**Affected systems:** car-park props mesh (`AirsideAdelaideRoadNetworkMesh`).
**Migration impact:** none. Reuses ODbL car-park data (DAT-YPAD-CARPARKS).
