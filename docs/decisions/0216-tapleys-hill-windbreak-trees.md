# 0216 — Tapleys Hill Road windbreak trees

Date: 2026-10-01 · Owner: Cursor

**Decision:** place a deterministic eucalypt/scrub windbreak along the Tapleys
Hill Road landside verge (`AdelaideWindbreakPlacement` +
`AdelaideWindbreakGeometry`), reusing `AdelaideTreeGeometry` lobes, drawn into
the road props sink. Cap ≤ 180 trees. Airport frontage only (outside the
aerodrome boundary, 20–220 m from the fence). No new mesh assets.

**Reason:** Phase 2c — NDVI canopy thins on the Tapleys verge; a windbreak row
reads as the real roadside planting west of YPAD.

**Affected systems:** road props mesh (`AirsideAdelaideRoadNetworkMesh`).
**Migration impact:** none. Reuses ODbL roads + ADR 0212 tree maths.
