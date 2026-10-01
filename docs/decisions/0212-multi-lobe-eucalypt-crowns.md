# 0212 — Multi-lobe eucalypt crowns on NDVI suburb trees

Date: 2026-10-01 · Owner: Cursor

**Decision:** replace the single hex crown on NDVI suburb trees with a
deterministic 3-lobe faceted canopy (primary dome + two side clusters) from
`AdelaideTreeGeometry`. Pure layout maths; `AirsideAdelaideSuburbs.AddTree`
draws the triangles. No new mesh assets or licences. Cap ≤ 48 crown tris/tree
(ships at 28).

**Reason:** Phase 2 trees — VEG-001 already reads as multi-lobe eucalypts on the
legacy kit path; suburb NDVI trees still looked like one hex blob. Matching that
read without importing new art.

**Affected systems:** `AirsideAdelaideSuburbs` tree mesh; Presentation only.
**Migration impact:** none.
