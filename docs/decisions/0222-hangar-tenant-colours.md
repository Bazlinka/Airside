# 0222 — Hangar tenant cladding colours

Date: 2026-10-01 · Owner: Cursor

**Decision:** paint named Adelaide hangars (and the RFDS outline) with
stylised per-tenant shell / door / roof hex bands from
`HangarTenantPalette`, batched by stable tenant key. Unnamed hangars keep
today's default greys. Reserved keys for Aero Club / Adelaide Aero Club /
Qantas wait for OSM names. No logos, no new assets, no surveyed footprint
changes.

**Reason:** Phase 3 airside accuracy — the GA row and RFDS currently share
one grey metal mesh and read as one anonymous shed. Muted cladding bands
separate tenants from overview without fighting aircraft liveries.

**Affected systems:** `HangarTenantPalette` (new); hangar / RFDS spawn in
`AirsidePrototype.YpadPavement`.
**Migration impact:** none.
