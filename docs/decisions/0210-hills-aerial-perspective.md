# 0210 — Hills haze / aerial perspective on the outer ring

Date: 2026-10-01 · Owner: Cursor

**Decision:** bake cool blue-grey aerial haze into outer-terrain land vertex
colours from horizontal distance + DEM height (`AdelaideAerialPerspective`).
Sea unchanged. Presentation only; no new textures.

**Reason:** Phase 1 far-ring item — Hills should dissolve into haze at distance
instead of staying a hard brown silhouette (ypad-surroundings-plan P6).

**Affected systems:** `AdelaideOuterTerrainGeometry` land colours.
**Migration impact:** none.
