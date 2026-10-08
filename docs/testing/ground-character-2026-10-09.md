# Ground character — 9 October 2026

Task #687. Source-only implementation; no player screenshot or native appearance claim.

## Implemented

- Grass/soil: 7–30 m irregular islands, local dry/damp colour shifts and 1.7 m grain
  fading at 35–120 m camera distance. Existing scanned maps remain in use.
- Infield soil islands break the coarse vertex blend; mowing is spatially interrupted
  and warped. Both fade before the rectangular mesh edge. Shared dry character is
  applied before the satellite blend on both sides of that edge.
- Surrounding mapped land: restrained nearby grain, fading at 180–650 m; sea and
  distant satellite shapes retain their existing treatment.
- Ground-facing pavement: sparse resurfacing and meandering sealed cracks; concrete
  gets offset 5.2 × 5.8 m visual slab joints/age variation. The previous 18 m joint mesh is retained only
  for the missing-shader fallback, avoiding two superimposed grids. Thin lines use pixel
  derivatives and fade at 65–230 m. These are original modelling details, not a
  survey of actual Adelaide repairs or joints.
- Apron repairs: 14 attempts per mapped apron instead of 6, mainly small repairs
  with occasional wider resurfacing. Two age colours plus existing drainage pits
  form three static batches. Radius-based containment keeps every rotated patch
  inside its own apron; the existing regression now checks every corner/inset.

## Validation and limits

- Focused `python3 scripts/test-quick.py ApronSurfaceWear`: 4/4 pass, including
  deterministic output and whole rotated patch containment. Initial sandbox restore
  failed; the authorised retry used the available .NET SDK and succeeded.
- C# 9 Roslyn syntax parsing of all four changed C# files: zero errors.
- Shader source inspection: absolute XZ anchoring, land/upward-face masks, metre
  scales, filtered thin detail, no temporal input/new texture dependencies.
- Generated harness consistency: up to date. `git diff --check`: passes.
- New include has its own ShaderIncludeImporter metadata/GUID. Shaders ship as
  normal Unity assets; no StreamingAssets model/texture mirror is introduced.

Native Unity shader compilation/import, actual wet/dry lighting and readability,
control-tower/cockpit/overview appearance and GPU performance are unverified. No
full domain/Unity suite, packaged build or rendered review was run under Bailey's
standing policy. This is a visual material change, not physical terrain roughness.
