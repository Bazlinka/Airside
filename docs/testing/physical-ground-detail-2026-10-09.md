# Physical ground detail — 9 October 2026

Task #693. Source/geometry evidence only; no native visual claim.

## Changes

- Sparse 18–42 cm grass blades around existing mapped avenue/windbreak planting.
  Four tufts/three two-sided blades per cluster: 24 triangles, at most 160 clusters.
  No trees/grass are randomly scattered across operational surfaces. Coarse mapped
  natural/unknown land is accepted only at existing roadside planting anchors.
- Whole 1.6 m cluster radius stays outside the aerodrome boundary with 3 m additional
  clearance; airport pavement has 5 m additional clearance. Every ground-level
  road is checked with width + cluster radius + 2 m margin, including neighbouring
  roads/service lanes. Cluster centres remain at least 4 m apart.
- Coastal scrub uses existing placement, with two/three differently proportioned
  lobes and four/five irregular radial sides. Maximum 30 triangles per bush.
  Solid faces have their geometric normals; grass back faces have opposed normals.
- Existing drain pits gain four rim pieces/six bars, all inside the old pit outline.
  Metal top is 4 mm above the pit polygon; a single static batch keeps draw cost fixed.
- Taxiway wear has varying width, length, interval and inset. Tapered polygons remain
  within the width/longitudinal limits of each existing taxiway segment.

## Checks

- `python3 scripts/test-quick.py GroundPhysicalDetail TaxiwayEdgeWear AdelaideDuneScrubPlacement`:
  **11 passed**, zero failed, ~356 ms test execution after compilation.
- Generated harness updated: two new pure production files and one focused test file.
  Regeneration compiles the resulting headless source set; no broad test suite run.
- Changed Unity-facing road-props/YPAD builders: C# 9 Roslyn syntax parsing, zero errors.
- `python3 scripts/audit-unity-assets.py`: no new metadata/GUID/mirror issues; fails
  only on the already documented satellite JPEG mirror mismatch, retained unchanged.
- `git diff --check`: passes.

## Limits and next review

No Unity import/compile, packaged build, rendered review, day/dusk/night/wet/dry
appearance or GPU measurement. Inspect drain flushness and grass/scrub silhouettes
in a close follow view; check overview readability, shadows and long-journey origin
shifts when Bailey chooses to playtest. Numerical clearance/budgets do not prove
native appearance or frame time. Spatial wetness/puddles remain a separate future
improvement; this pass changes vegetation, drainage metalwork and pavement edges.
