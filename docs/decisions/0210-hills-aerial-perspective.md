# 0210 — Hills haze / aerial perspective on the outer ring

Date: 2026-10-01 · Owner: Cursor

**Decision:** bake cool blue-grey aerial haze into outer-terrain land vertex
colours from horizontal distance + DEM height (`AdelaideAerialPerspective`).
Sea unchanged. Presentation only; no new textures.

**Reason:** Phase 1 far-ring item — Hills should dissolve into haze at distance
instead of staying a hard brown silhouette (ypad-surroundings-plan P6).

**Affected systems:** `AdelaideOuterTerrainGeometry` land colours.
**Migration impact:** none.

**Amendment 2026-10-07 (Cursor):** the haze is baked by range from the airfield, not
the camera, so the zoomed-out overview looked straight down on full-strength haze and
the 30 to 96 km ring read as white cloud. It now starts at the 30 km satellite-disc
edge (no seam), runs to 96 km and is capped at 0.3 (`MaxStrength`). Ground-level views
are unaffected: the shader's camera-distance fade already hazes that ring.
