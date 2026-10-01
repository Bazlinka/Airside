# 0220 — Bake-time LOD for NDVI suburb trees

Date: 2026-10-01 · Owner: Cursor

**Decision:** choose NDVI tree crown detail from horizontal distance to ARP at
field-build time (`AdelaideTreeLod`): Full 3-lobe ≤ 1400 m, Medium primary
lobe ≤ 2800 m, Billboard crossed cards beyond. Drawn by
`AirsideAdelaideSuburbs.AddTree`. No GPU instancing or runtime impostors yet.

**Reason:** Phase 2g — 11k+ Full crowns are too heavy at overview; far suburbs
only need a canopy mark. Bake LOD cuts tris without new assets or licences.

**Affected systems:** suburb tree mesh only.
**Migration impact:** none. Runtime camera LOD / wind remain later Phase 2 work.
