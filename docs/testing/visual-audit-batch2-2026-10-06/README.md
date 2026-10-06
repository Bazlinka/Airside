# Visual audit fixes — second code-only batch, 6 October 2026

Bailey authorised implementation of the concrete audit backlog in draft PR #534,
with parallel workers. Explicit constraint: **no Unity testing, builds or player
execution**. Native compilation and rendered behavior remain unverified.

First seven code fixes merged in #535 (`582290c0`), integrated headless 1,557 pass.
This second batch covers the nine remaining concrete findings. No optional
lighting/SSAO, tree/scenery design, data acquisition or grading change is included.

## Ownership and acceptance

- ASSETS owns AST-01/02: per-face box-kit UVs with vertex seams before tangent
  generation; preserve aircraft cylindrical mapping and shared-cache ownership.
  Test actual shipped shed triangles and tangent math; check both individual and
  combined mesh callers through source review. Runtime Mesh execution is excluded.
- ENVIRONMENT owns CORE-01 and WLD-01: geographic coastline foam keeps identity
  scale while alpha animates; primitive coast pads retain their separate behavior.
  Roof texture lookup compensates flight origin; fog stays in shifted coordinates.
  Pure coast/UV arithmetic and source contracts are not shader compilation.
- WEATHER owns AIR-02, CK-01, WX-01, SKY-01/02: correct port/starboard palette,
  observer rain for wipers, meteorological downwind flow, dedicated vertex-colour
  celestial star pass without foreground depth occlusion. Preserve sun/moon and
  weather/simulation settings. Source shader/material checks and pure policies
  need explicit verification limits. Include the dedicated shader for packaging.
- INTEGRATOR owns status/docs, source asset register and shared generated harness.

Use separate worktrees and non-overlapping files; shared-file requests are handed
off explicitly. Keep scheduling, saves, simulation weather and optional design
choices unchanged. No scene, runtime art mesh or aircraft paint is regenerated.

## Verification

- ENVIRONMENT worker `359aa82a`: **13/13 focused** coastline and roof-coordinate
  checks. Source contracts verify production scale writes and geographic shader
  lookup; math fixtures use the actual distant Adelaide coast segments.
- ASSETS worker `e7dbfa47`: **8/8 focused** UV/tangent checks, including all 12
  shipped shed triangles and 24,000 source vertices expanding to 72,000 seam
  vertices; see [focused run](assets-focused.txt).
- WEATHER worker `b994fb98`: **25/25 focused** pure nav, observer-rain, wind and
  star colour checks; six source contracts independently rerun by the integrator:
  [contracts](weather-star-contracts.txt). Rerun `python3 scripts/test-weather-star-contract.py`.
- Static asset audit: **1,760 unique GUIDs**, **386 byte-identical mirrors**,
  **70 committed character materials**; [filesystem audit](asset-audit.txt).
- Shared harness generation: 137 pure Presentation files and 192 test files.
- Primary source review and independent cross-review found no unresolved
  actionable symbol, coordinate, lifecycle, tangent or material-cache issue.
- Final integrated full headless: **1,603 passed / 0 failed**; 46 new cases
  in this batch, 92 across both batches; [full run](integrated-headless.txt). No Unity commands are authorised.

The dedicated star shader uses additive RGB and the existing global RGB fade, so
zero fade contributes zero light. Root review rejected alpha-one replacement
blending, which would darken the underlying sky. Far depth plus no depth writes
preserves foreground ordering by construction; a runtime capture remains absent.
Missing/unsupported shader uses an independent unlit fallback; ordinary sun/moon
material caches are unchanged. The fallback does not gain vertex tint variation.
Always-included shader registration is a static packaging precaution, not proof
of a successful build.

The mesh loader uses the tested pure unwrap/tangent helpers, preserving source
normals and triangle order before upload. It selects 32-bit indices after seam
expansion. Changed native Mesh calls, material bindings and shaders were reviewed
as source and were not compiled in Unity. Headless tests do not verify normal-map
appearance, rendered coast positions, star depth ordering, wiper transforms,
lighting quality or frame time.

## Disposition

Together with #535, all 16 concrete findings from draft #534 are implemented in
code, subject to the native/visual limits above. Two optional lighting-coverage
gaps and the optional design/data/performance backlog are unchanged. #534 remains
a separate draft audit report. Historical manual acceptance stays closed, and
existing technical issues remain recorded.
