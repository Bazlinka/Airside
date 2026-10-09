# Physical ground detail on verges, drains and pavement edges

Status: Accepted — Bailey authorised the next environment improvements.
Date: 2026-10-09. Task: #693. Owner: Codex.

## Decision

Add sparse original 3D grass around the existing mapped landside avenue/windbreak
planting. Keep clusters outside the aerodrome boundary, clear of airport pavement
and all nearby ground-level roads, and off mapped water, buildings, parking and golf.
The coarse land-cover map's unknown cells are allowed only at these already mapped
roadside planting anchors. Cap at 160 clusters/3,840 triangles, collected into the
existing spatially culled road props tiles; no per-cluster GameObjects or update loop.

Coastal scrub retains its existing sites but gains two/three asymmetric lobes,
varied radius/height/colour and true outward face normals. Budget rises from 16 to
at most 30 triangles per bush. A shared solid-triangle helper supplies geometric
normals and explicit back faces for thin grass; existing road-surface primitives
remain unchanged.

Existing apron pits gain a flush rim and six grate bars, fitted within their old
footprint. Their top sits 4 mm above the pit shadow polygon (under 1 cm above the
apron); bases embed below it. One combined metalwork batch, no collider/obstacle.
Taxiway edge wear gains varied width, length, stride and inset, drawn as tapered
six-point islands within the original pavement instead of repeated rectangles.

## Reason

The previous surface-character pass breaks colour uniformity. These details add
physical silhouettes and plausible maintenance detail in close/follow views while
keeping airport operations legible and the ground maintained.

## Affected systems and migration

RoadMeshSink, AdelaideDuneScrubGeometry, new AdelaideVergeDetail and ApronDrainGeometry,
road-props integration, TaxiwayEdgeWear and YpadPavement drain/edge batching.
Original project-owned procedural geometry; zero cost/external sources. Existing
OSM/terrain registrations and attribution remain. No save/data migration, new
simulation decisions, terrain heights, aircraft or mapped layout changes. Existing
procedural terrain/materials and previous committed presentation remain fallback.

## Verification

Focused geometry, containment, normal/budget and existing scrub/edge regressions:
11/11 pass. New pure files included by the generated harness; Unity-facing changes
parse with C# 9. Asset metadata is valid; repository audit retains only the known
satellite JPEG mirror mismatch. No broad suite, Unity build or rendered review run.
Native appearance, shadows, origin-shift rendering and GPU cost remain unverified.
Evidence: `docs/testing/physical-ground-detail-2026-10-09.md`.
