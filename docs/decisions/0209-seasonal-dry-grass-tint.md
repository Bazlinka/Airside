# 0209 — Phase 1 seasonal / dry-grass tint by date

Date: 2026-10-01 · Owner: Cursor · Status: **design lock** (implement next)

**Decision:** tint Adelaide surroundings grass-like vertex colours from the injected
airline clock’s Adelaide calendar day — a pure dryness curve, no new textures.
Apply only to **Plain base, Park, Scrub, and AirfieldEdge**. Leave **Golf** (and
built/sand/water) year-round. Presentation only; bake once at surroundings build.

## Why this slice

Visual overhaul Phase 1 item 4 still open after Golf (0206), bunkers (0207), and
CBD boxes (0208): *“seasonal/dry-grass tint by date.”* Adelaide plains go straw in
late summer and greener in winter. Overview already has fixed sRGB palette colours
in `AirsideAdelaideSurroundings` (`Plain` / `Park` / `Scrub` / `AirfieldEdge`); they
never see the calendar today.

## How colours work today

| Path | Role |
|---|---|
| `AirsideAdelaideSurroundings.LandColour` | Noise plain → optional OSM `CoverColour` → lerp to `AirfieldEdge` near the field → beach |
| `CoverColour` | Fixed palette: Park olive, Golf irrigated green (0206), Scrub, Suburb, … |
| `AdelaideLandCover.Kind` | No `Plain` kind — `None` (and unmapped cells) keep the noise `Plain`/`Suburb`/`Park` blend |
| Vertex bake | Once in `TryBuild` (`AirsidePrototype.FieldBuild`); no date argument |
| `Surroundings.shader` | Multiplies `_AirfieldTint` / `_SatelliteTint`; RGB from vertices; land `a = 0` |
| `AirsideAdelaideGroundMesh` | Separate dry/green/dirt layers + `_Tint`; Sentinel-2 summer composite (ADR 0157) |
| `AdelaideFarLandCover.Colour` | Far DEM already authored as late-summer straw (ADR 0190) — out of this slice |

A global material multiplier would also hit Golf, suburbs and car parks. Selective
kinds need the vertex path (or a susceptibility channel). Smallest fix: multiply
grass-like base colours **before** the final `.linear` write in `LandColour`.

## Date source (build / runtime)

| Moment | Source |
|---|---|
| **Fleet / career** | `AirlineClock.LocalAt(SimulationTime)` → `DateTime.DayOfYear` (1–365/366). Same pattern as `RunwayWeather.At` and Cathay season. |
| **Field build** | When `AirsidePrototype` calls `AirsideAdelaideSurroundings.TryBuild`, pass that day-of-year from `_operations.Clock` (else `AirlineClock.Default`) and `_clock.Now`. |
| **Bare / review** | Same Default clock, or review local date if already overriding presentation day via existing review flags — do **not** invent a second calendar. |
| **Headless tests** | Call pure maths with literal day-of-year integers; no clock needed. |

**Bake once at build.** Mid-session calendar advance does **not** rebuild the mesh
in this slice (career soaks that cross seasons keep the start-of-session tint).
Optional later: refresh when `DayOfYear` crosses a coarse bucket — out of scope.

Do **not** use `DateTime.UtcNow` for the tint when an injected clock exists.

## Dryness curve (southern hemisphere)

Pure function (no `UnityEngine`):

- `Dryness01(dayOfYear)` ∈ [0, 1]
- Peak dry ≈ **15 Jan** (day 15); peak green ≈ **15 Jul** (day 196)
- `dryness = 0.5 + 0.5 * cos(2π * (dayOfYear − 15) / 365)` (leap days: clamp/wrap day into 1…365 for stability)
- `ApplyRgb(r,g,b, dryness)` pushes toward straw (raise R, drop G slightly) as dryness → 1; identity at 0

Tunable constants live next to the function; lock numeric samples in tests so the
overview does not drift silently.

## What to tint (and what not)

| Target | Tint? | Reason |
|---|---|---|
| Noise **Plain** base (+ `AirfieldEdge`) | **Yes** | Open dry grass / airfield seat |
| **Park** | **Yes** | City parks brown off-irrigation in summer |
| **Scrub** | **Yes** | Coastal/dune scrub already dry; strengthen summer |
| **Golf** | **No** | ADR 0206 irrigated fairway must stay readable year-round |
| Residential / Commercial / Parking / Sand / Water | **No** | Not grass |
| `AirsideAdelaideGround` dry layer / `_Tint` | **No (this PR)** | Sentinel summer bake already dominates; separate follow-up if Bailey wants field grass to track winter |
| Far land-cover / Hills haze | **No** | Far ring already late-summer; haze is its own Phase 1 item |

**Verdict:** tint **Park + Scrub + Plain/AirfieldEdge only** — not Golf.

## Smallest shippable implementation PR

One acceptance criterion: *at overview, open plain and parks read straw in mid-January
and greener in mid-July for the same mesh path; golf courses stay the irrigated green.*

### Files to add

| Path | Role |
|---|---|
| `game/Airside/Assets/Airside/Presentation/AdelaideSeasonGrassTint.cs` | Pure maths: `Dryness01`, `ApplyRgb` / cover predicate; no `UnityEngine` |
| `game/Airside/Assets/Airside/Tests/EditMode/AdelaideSeasonGrassTintTests.cs` | Headless locks below |
| `docs/decisions/0209-…` | This ADR |

### Files to edit

| Path | Change |
|---|---|
| `AirsideAdelaideSurroundings.cs` | `TryBuild(..., int adelaideDayOfYear, ...)`; in `LandColour` / `CoverColour` path, apply tint to Plain/Park/Scrub/AirfieldEdge only |
| `AirsidePrototype.FieldBuild.cs` | Pass `LocalAt(_clock.Now).DayOfYear` into `TryBuild` |
| `scripts/dotnet-harness/Harness.Generated.props` | Via `scripts/update-harness.py` after adding the test/presentation file |
| `GAME.md`, `CHANGELOG.md`, `docs/plans/visual-overhaul-plan.md` | Status |

### Explicitly out of this PR

- New textures / TerrainLayers / Sentinel re-bake
- Shader channel packing or `_SeasonTint` global
- Remeshing or per-frame vertex updates as the career clock advances
- Far DEM / Hills haze / CBD (done)
- Simulation, saves, land-cover generator

## Test assertions (headless)

1. `Dryness01(15)` ≈ 1 (within ~0.02); `Dryness01(196)` ≈ 0.
2. `Dryness01` is continuous / symmetric enough: day 15+k and 15−k match within tolerance (wrap-aware).
3. Park RGB after `Apply` at dryness 1 has **higher R/G ratio** (strawier) than at dryness 0; green channel does not increase.
4. Golf base RGB is **unchanged** for any dryness (predicate / CoverColour path).
5. Suburb / Parking / Beach samples unchanged.
6. Same `(r,g,b,day)` → bit-identical floats (deterministic).
7. Optional thin lock: `AppliesTo(Kind.Park|Scrub|None)` true; `AppliesTo(Kind.Golf|Residential|…)` false.

No Mac verify; Bailey rebuilds when he wants (plan principle).

## Migration impact

None. Presentation-only; no save schema; no land-cover byte change.

## Affected systems

Surroundings vertex tint only. Domain clock already exists; Simulation unchanged.
