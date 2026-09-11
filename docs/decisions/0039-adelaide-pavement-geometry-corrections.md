# 0039 — YPAD silhouette geometry corrections (fillets, separations, fence seating)

**Date:** 2026-09-11
**Status:** Accepted
**Decision owner:** Claude (independent review of ADR 0036–0038 / PR #194–#196)

Supersedes the fillet-shape decision in ADR 0037 §1 and the Taxiway F / apron
stations in ADR 0036 and ADR 0038. Everything else in 0036–0038 stands.

## Context

An independent review of the merged silhouette found three defects that the
existing 11 pavement tests did not cover, plus a set of realism and code issues.
The apron-vs-12/30 overlap that ADR 0038 set out to fix *was* genuinely fixed —
the terminal pad clears both runways by 198 m on the merged geometry — but the
work introduced or left standing the following.

## Decision

1. **Fillets are true concave fillets, not quarter-disks.**
   ADR 0037 placed a quarter-disk of radius *r* centred **on** each corner. That
   is the geometric complement of a fillet: it bulges convexly outward and is
   tangent to neither edge. Because every stub is shorter than 2 r, the four
   disks on a stub merged and swallowed it — a 23 m taxiway rendered as an
   80–107 m blob along its whole length.

   `FilletSpec` now carries a `PavementArcKind`. A `CornerFillet` is the curved
   triangle bounded by the two pavement edges and an arc of radius *r* tangent to
   both, centred at the corner offset by *r* along each edge. `Sector` keeps the
   old behaviour for the taxiway end caps and the runway crossing pad.
   `ClampFilletRadius` stops a radius outrunning the stub it smooths.

   Measured result across each stub's middle: 23.0 m (was 80.7 m).

2. **Parallel taxiways meet ICAO code 4E separations.**
   Taxiway F was 95 m from the runway centreline. Annex 14 requires 182.5 m for
   code 4E on a precision approach runway, and the code 4 runway strip is 150 m
   half-width — so F, Taxiway A, every stub and both apron pads were standing
   inside the protected strip. F moves to 182.5 m, A to 290 m (107.5 m
   separation, over the 80 m code E taxiway/taxiway minimum).

   Downstream stations move with it: the terminal apron to Z = 450 m and the
   RFDS pad to Z = −230 m, which also takes the RFDS pad out of the runway
   strip (it was 120 m from the centreline, inside the 150 m band).

3. **Hold-short bars sit at the code E holding position.**
   They were 12 m from the runway edge — 34.5 m from the centreline, on the
   shoulder. They now sit at `HoldShortFromRunwayEdgeMetres`, which places the
   pattern at 90 m from the centreline. The 148.5 m exit links have the room;
   the old 61 m links did not, which is why this could not be fixed before (2).

4. **The perimeter fence is seated on the ground it stands on.**
   Every panel, post, guard and gate leaf was pinned to world Y = 0, while the
   authored ground drops ~2.4 m into its boundary lip exactly where the fence
   runs. The whole ~11 km ribbon floated by about its own height.
   `AirsideAdelaidePerimeter.FenceBaseY` / `FenceBaseYAlongSegment` seat it on
   `AirsideAdelaideGround.WorldHeight`, with a fallback to the flat slab top when
   the authored mesh could not be built. Panels are also combined per side,
   turning ~900 renderers into a handful.

5. **Taxiway paint is taxiway paint.**
   `TaxiwayGuide` reused the runway centreline (30 m dashes) and a single wide
   edge stripe. It is now one continuous 0.15 m centreline plus the double
   yellow edge marking. `HoldShortBars` is ICAO pattern A — two solid bars on
   the runway side, two dashed beyond.

## Also corrected

- **Shared-material mutation.** The fillet builder wrote `mainTexture` and
  `mainTextureScale` onto the material returned by `CreateSharedSurfaceMaterial`,
  which is cached and shared by key. All 49 fillets fought over one material and
  the last write won. The texture and tiling now go into the cache key, and the
  meshes carry real world-metre UVs (they previously had none at all, so the
  tiling was inert regardless).
- **Fillet Y.** Fillets were placed at the slab's mid-height and then Y-scaled —
  a no-op on a mesh whose vertices are all at y = 0, leaving them 6.4 cm below
  the taxiway surface. They now sit on the surface.
- **Fan winding** is derived from the signed area of the fan polygon including
  its apex, so a concave patch cannot render face-down and invisible.
- **Wet-surface filters.** 12/30's children were named `slab`, `shoulder L`,
  `edge_left`… and matched no prefix, so the cross runway stayed dry in the rain
  while 05/23 darkened. Bare-field pavement is now named `Runway 05/23 …` /
  `Runway 12/30 …`, and `Pavement fillet` is in both filters.
- **`AllFillets()` is cached.** It allocated a 64-element array plus an
  `Array.Resize` on every call, and it is called once per ground-mesh vertex
  (6 305 of them). Whole-mesh `LayerWeights` now costs 33 ms, allocation-free.
- **`AirsideAdelaideGround.DistanceToRunway` → `DistanceToPavement`**, which is
  what it has measured since ADR 0038.
- **Stub sealed shoulders** are in the distance field; the builder spawned them
  but `DistanceToPavement` did not know about them.
- Dead gate-gap branch and `alongX ? station : station` removed from the fence
  loop; `AirsideBareField` indentation repaired.

## Explicitly NOT changed

**`CrossYawDegrees` stays at 73°.** The comment justified it as
"magnetic 115° − 042°", but 042° is the bearing of a runway designated 04, not
05 — the derivation was wrong. The designators bound the crossing angle to
roughly 61°–79°, and 73° sits inside that, so the number is plausible but
unverified. Changing it to another unverified number would not be an
improvement. The comment now says so, and a test pins the band rather than
asserting the figure is correct. **This needs checking against the published
YPAD DAP**, along with the decision to cross both runways at their midpoints.

## Tests

`AdelaidePavementTests` grows from 11 to 21. The three blockers now have
regressions that were confirmed to fail against the old code:

- `Pavement_FilletsDoNotSwallowTheStubsTheySmooth` — reverting to quarter-disks
  reports "runway exit link at Z=52.2 is 82.0 m wide; nominal is 23 m"
- `Perimeter_FenceFollowsTheGroundItStandsOn` — pinning the base to 0 reports
  "fence base at (-1528,-1153) must not float above the ground"
- `Pavement_CornerFilletIsTangentToBothEdges` — pins the concavity itself

## Affected systems

- Presentation: `AirsideAdelaidePavement`, `AirsideAdelaidePerimeter`,
  `AirsideStripMarkings`, `AirsideAdelaideGround`, `AirsideBareField`,
  `AirsidePrototype` bare builders
- Tests: `AdelaidePavementTests`
- Docs: `GAME.md`, `CHANGELOG.md`

## Migration impact

None. Domain, Simulation and Persistence are untouched; saves, the circuit skip
and reservations are unchanged. Still no buildings.
