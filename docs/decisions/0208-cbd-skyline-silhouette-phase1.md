# 0208 — Phase 1 CBD skyline silhouette (far ring)

Date: 2026-10-01 · Owner: Cursor · Status: **accepted (design lock; implement in the next narrow PR)**

**Decision:** draw the Adelaide CBD skyline as a **small cluster of low-poly grey boxes**
from OSM heights already in the repo — **not** billboards. Presentation only; stand the
boxes on the existing DEM relief; one merged mesh, no shadows.

## Why boxes, not billboards

| | Low-poly boxes (chosen) | Billboard silhouette cards |
|---|---|---|
| Licence | Reuse ODbL OSM already registered (`DAT-YPAD-SUBURBS`) — no new art | Needs a skyline texture + register entry; rejected for this slice |
| Overview / any yaw | Reads as a city from every angle | Flattens or spins when the overview looks NE at the city |
| Determinism | Footprints + heights from a committed snapshot | Hand-authored texture hard to lock headlessly |
| Cost | ~40 boxes × 12 tris ≈ **500 tris**, 1 draw | Cheap draw, but texture memory + fake look |
| Plan language | Matches `ypad-surroundings-plan.md` P6 (“CBD as a small cluster of grey boxes”) and visual overhaul Phase 1 item 5 | Allowed by the plan, worse fit here |

Hills haze / aerial perspective and coast-horizon polish stay **out of this slice**
(separate Phase 1 follow-ups). This ADR only locks the CBD skyline silhouette.

## Data already in the repo (no new licence)

**Source:** `DAT-YPAD-SUBURBS` — snapshot
`docs/data/osm/adelaide-suburb-buildings-2026-09-29.json` (ODbL; same register row as
suburb houses). The fetch bbox reaches lon ≈ 138.598 and already contains CBD towers;
`scripts/generate-adelaide-suburbs.py` drops them only because of its **2 km airfield
band** (ADR 0159).

Probe of that snapshot (runway frame, Victoria Square ≈ `(6381, −2280)`, ~6.8 km from
the midpoint):

- Filter: centroid within **2.5 km** of Victoria Square **and** height ≥ **30 m**
  (`height` tag, else `building:levels × 2.9` to match the suburb house convention for
  level-only tags; prefer tagged `height` when present, as the suburb non-house path does).
- Result: **42** towers (28 named), including Victoria Tower (121 m), Festival Tower
  (115 m), Sofitel Adelaide (99 m), Telstra (68 m).
- Cluster AABB ≈ `x[5447, 7366]`, `z[−2358, −829]`; centroid ≈ `(6364, −1673)` (~6.6 km).
- **Known gap (do not invent data):** Westpac House and other towers east of lon 138.598
  are outside this snapshot. Optional later: a dedicated CBD Overpass/API extract under
  the same ODbL, new `docs/data/osm/adelaide-cbd-buildings-*.json`, new register row.
  The 42 towers already read as a skyline at overview.

**Not used for this slice:** `AdelaideBuildings` (airside only), far land-cover Built class
(ADR 0190 — 250 m grey-tan tint, not extrusions), DEM alone (ADR 0158 — CBD ground ~50 m,
no towers).

## Placement (player-visible)

- CBD is **ENE of YPAD**, ~**7 km** (Victoria Square ≈ 6.8 km runway-frame).
- Boxes sit on `plainY + AdelaideTerrainHeights.ReliefAbovePlain(DEM height)` at each
  centroid (same land surface as far terrain / suburbs).
- Parent under the surroundings / far-terrain root so the existing horizon fade applies.
- Player-visible at **field overview looking toward the city** and at far zoom; optional
  later bookmark `city-skyline` (not required for the first PR).

## Smallest shippable implementation PR (next)

One acceptance criterion: *from overview / far zoom toward the city, a grey tower
cluster appears ~7 km ENE instead of flat satellite ground.*

### Files to add

| Path | Role |
|---|---|
| `scripts/generate-adelaide-cbd-skyline.py` | Read the existing suburb-buildings JSON; emit the silhouette table (`--check` byte-stable) |
| `game/Airside/Assets/Airside/Simulation/AdelaideCbdSkyline.cs` *(or Art `Terrain/adelaide_cbd_skyline_v01.bin`)* | Baked boxes: centre x/z, half-extents, wall height, OSM id; Attribution string |
| `game/Airside/Assets/Airside/Presentation/AdelaideCbdSkylineGeometry.cs` | Pure (no `UnityEngine`): box positions / colours / triangles |
| `game/Airside/Assets/Airside/Presentation/AirsideAdelaideCbdSkyline.cs` | Unity build: one mesh, one unlit/grey material, shadows off |
| `game/Airside/Assets/Airside/Tests/EditMode/AdelaideCbdSkylineTests.cs` | Headless locks below |
| `docs/decisions/0208-…` | This ADR (already) |

Prefer a **generated C# table** (golf-bunker pattern, ADR 0207) while N≈42; switch to a
`.bin` only if a later denser mid-rise fill grows the set.

### Files to edit

| Path | Change |
|---|---|
| `AirsideAdelaideSurroundings.cs` or far-terrain build path | Call `AirsideAdelaideCbdSkyline.TryBuild` after far terrain |
| `docs/data/ASSET_AND_DATA_REGISTER.md` | Note derived use of `DAT-YPAD-SUBURBS` for CBD silhouette (same ODbL; no new licence). Optional dedicated `DAT-YPAD-CBD-SKYLINE` row as a *derived* product of that snapshot |
| `GAME.md`, `CHANGELOG.md`, `docs/plans/visual-overhaul-plan.md` | Status |
| `scripts/dotnet-harness/Harness.Generated.props` | Via `update-harness.py` when the new Presentation/test files appear |

### Geometry rules (deterministic)

- Footprint → axis-aligned runway-frame AABB (or minimum-area rectangle if cheap); **no**
  full extruded footprints in v1 (keeps tris tiny and pure builder simple).
- Wall height = OSM `height`, else `2.9 × building:levels`, else drop (no rule defaults —
  silhouette must be sourced).
- Min height **30 m**; max half-extent clamp ~80 m so one huge footprint cannot dominate.
- Grey palette consistent with far Built / suburb commercial greys; night glow **out of
  scope** (Phase 4).
- Soft-fail: missing snapshot / empty table → no skyline object (far ring unchanged).

### Test assertions (headless)

1. `AdelaideCbdSkyline.Count` in **30–60** (probe: 42).
2. Every box centroid inside CBD AABB `x∈[5000,7800]`, `z∈[−2800,−600]` (or
   distance to Victoria Square ≤ 2.5 km).
3. Every height ≥ 30 m and ≤ 200 m.
4. Named / id locks: OSM ids for Victoria Tower (`1376925785`), Festival Tower
   (`1285172442`), Sofitel (`971999300`) present (or centroids within 80 m of probe
   positions).
5. `AdelaideCbdSkylineGeometry.Build`: vertex count = `8 × N`, triangle count =
   `12 × N`; all Y bases ≈ DEM relief + plain at that xz (±1 m).
6. Attribution contains `OpenStreetMap`.
7. Geometry / data types compile in the headless harness (**no `UnityEngine`**).
8. Generator `--check` is byte-identical to the committed table.

### Performance budget

- **≤ ~1 k tris**, **1 draw call**, **0 shadow casters**, no new textures.
- Negligible vs far ring (~72 k tris) and suburbs (~212 k tris).
- Still under the surroundings plan’s ≤ 150 k extra-tris guideline for this whole
  family of work; this slice uses ~0.3 % of that.

## Explicitly unchanged

Simulation, routes, reservations, saves, suburb 2 km band, far DEM/land-cover meshes,
Sentinel drapes, and weather. No Mac verify gate; Bailey rebuilds when he wants.

## Evidence owed by the implement PR

`scripts/test-domain.sh` green including `AdelaideCbdSkylineTests`. Unity look optional.
