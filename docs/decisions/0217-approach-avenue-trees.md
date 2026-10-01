# 0217 — Approach avenue trees (Bradman / Burbridge / Williams)

Date: 2026-10-01 · Owner: Cursor

**Decision:** place deterministic avenue eucalypts on both landside verges of
Sir Donald Bradman Drive, Burbridge Road and Sir Richard Williams Avenue
(`AdelaideAvenuePlacement` + `AdelaideAvenueGeometry`), reusing
`AdelaideTreeGeometry` lobes, drawn into the road props sink. Cap ≤ 220.
Soft-deduped against the Tapleys windbreak grid. No new mesh assets.

**Reason:** Phase 2d — major approaches should read as planted avenues; NDVI
alone leaves verges patchy next to the field.

**Affected systems:** road props mesh (`AirsideAdelaideRoadNetworkMesh`).
**Migration impact:** none. Reuses ODbL roads + ADR 0212 tree maths.
