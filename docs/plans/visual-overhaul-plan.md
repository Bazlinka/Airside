# Visual overhaul plan — buildings, ground, trees, land

Status: **approved (Bailey 2026-10-01)** · continue Phase 1+ without ask-to-verify · Author: Claude · ADR 0198

## Goal
The overview and follow cameras should look attractive *and* read as Adelaide Airport (YPAD): accurate footprints and
heights, believable ground, real tree cover, coast, suburbs and Hills. Nothing here changes simulation, gates,
routes, reservations or saves.

## Starting point (from the repo)
- Buildings: 78 surveyed operational buildings (`AdelaideBuildings`), Terminal 1 + RFDS from OSM, procedural facade
  detail in `BuildingDetail` (windows, doors, hangar roofs, tower). Suburb houses are OSM footprints (ADR 0159).
- Ground: `AirsideAdelaideGroundMesh` + Sentinel-2 10 m albedo tint (ADR 0157), CC0 grass/dirt, coarse terrain and a
  Hills far ring (ADR 0158), road network (6,735 roads), car parks, boundary fence.
- Trees: `adelaide_trees_v01.bin`, placed from Sentinel-2 NDVI canopy (ADR 0160).
- URP 17.3, procedural materials via `AirsideMaterialLibrary`; art rules in `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`.
- **Constraint:** target 60 fps overview at 1600×900 on the dev Mac. Agents land narrow Presentation
  slices with headless locks; Bailey rebuilds when he wants — do **not** block on Mac captures or
  ask him to verify each slice.

## Principles
1. **Ship narrow slices; Bailey looks when ready.** Prefer headless-locked Presentation changes.
   Optional baseline captures stay available (`scripts/capture-visual-baseline.sh`) but do not gate merges.
2. **Accuracy is sourced.** Real footprints/heights from OSM and AIP data already in `docs/data/`; anything not
   sourced is marked approximate in the ADR (as done for ADR 0197).
3. **Licences first.** No external asset enters without a register entry (`docs/data/ASSET_AND_DATA_REGISTER.md`).
   Google/Bing/Esri imagery and Google 3D Tiles are rejected (see `ypad-surroundings-plan.md`).
4. **Procedural fallback stays** until a replacement is integrated.
5. One slice = one branch/PR, narrow and reviewable.

## Phase 0 — Baseline and budget (do first, ~1 session)
- Add a repeatable capture script set (extend `scripts/capture-game.sh`) with named camera bookmarks.
- Record fps / frame p95 / SetPass / batches per bookmark in `docs/testing/visual-baseline-<date>/`.
- Set budgets: 60 fps at 1600×900 at overview on the dev Mac; triangles and batches ceilings per phase.
- **Exit (code):** bookmarks + capture script on `main` (ADR 0200). Packaged PNG/metrics optional whenever Bailey runs the Mac script.

## Phase 1 — Ground and land (biggest visual return)
1. **Ground albedo and detail:** macro-variation (large-scale colour noise), triplanar detail texture, distance
   fade to the Sentinel tint so the CC0 grass never tiles visibly; soft edges between grass/dirt/sand/paved.
2. **Airside ground truth:** worn tarmac/concrete variation, rubber build-up at touchdown zones, oil staining at
   stands, apron joints and patch repairs, grass mow stripes on the runway strips, drainage pits.
3. **Landform:** finer heightfield near the airport (Sentinel/DEM-derived, plateau kept dead-level), the
   Patawalonga/Barcoo outlet, dunes and beach at West Beach, sea colour and shore foam.
4. **Land cover polish:** golf courses (West Beach, Glenelg) with fairways/greens/bunkers, ovals, parks, salt
   flats and scrub as distinct materials; seasonal/dry-grass tint by date.
5. **Far ring:** Hills haze and aerial perspective, coast horizon, city skyline silhouette (CBD, ~7 km) as
   billboards or low-poly blocks from OSM heights.
- **Files:** `AirsideAdelaideGround*.cs`, `AirsideTerrainField.cs`, `AirsideAdelaideOuterTerrain.cs`,
  `AirsideMaterialLibrary.cs`, generators under `scripts/`.
- **Exit:** headless locks green; new textures registered (CC0 or generated). Bailey rebuilds when he wants.

## Phase 2 — Trees and vegetation
1. **Species set:** replace generic crowns with a small, licensed set matching Adelaide: river red gum /
   sugar gum eucalypts, Norfolk Island pine (Glenelg/Henley), Moreton Bay fig, Canary Island date palm,
   olive, coastal tea-tree/scrub, street planes. Source CC0 (Quaternius/Kenney) or author procedurally; register each.
2. **Rendering:** 3 LODs + billboard impostors, GPU-instanced with wind sway; cap at N thousand visible trees;
   shadow only for the nearest ring.
3. **Placement:** keep the NDVI canopy points (ADR 0160) and add rule layers: avenues along real streets, palm
   rows at the terminal forecourt and car parks, windbreaks on Tapleys Hill Rd, scrub on dunes, none on airside
   (obstacle limitation surfaces).
4. **Ground cover:** grass tufts and bushes near the camera only, fading with distance.
- **Exit:** tree count and triangle budget met; day/night and storm wind checked; licences recorded.

## Phase 3 — Airport buildings (accuracy-led)
1. **Audit:** compare all 78 footprints/heights with OSM + AIP + imagery; correct mislabelled kinds; list
   unknowns. Output: `docs/data/ypad-buildings-audit.md`.
2. **Terminal 1:** real roof form (long curved standing-seam roof with the raised central clerestory), level 2
   departures forecourt/kerb, signage, glass and steel materials with reflections, interior glow at night
   (gate lounges, retail), aerobridge polish. Building on ADR 0185/0197.
3. **Landside precinct:** multi-storey car park facades, Hilton/office pads, rental car area, bus and taxi ranks,
   pedestrian links, kerb furniture, wayfinding poles (data: `AdelaidePrecinctGeometry`).
4. **Airside buildings:** control tower (real cab shape and height), fire station, hangars (per-tenant colours:
   RFDS, Qantas, Aero Club, Adelaide Aero Club), freight sheds, GSE depots, fuel farm tanks, substation.
5. **Facade system:** replace flat-colour boxes with a small trim-sheet/atlas material set (concrete, metal
   cladding, brick, glass, roofing) with normal maps; per-building tint from a seeded palette; roof clutter.
6. **Suburbs:** OSM houses get roof-pitch variants (hip/gable), roof colours sampled from local imagery, fences
   and driveways; taller landmarks (Glenelg towers, Harbour Town, Westfield West Lakes) at surveyed heights.
- **Exit:** every building has an audited source line; terminal/hangar/tower captures approved; batches within budget.

## Phase 4 — Lighting, atmosphere and colour
- Grading pass (tone-mapping, colour LUT), softer sun shadows, ambient occlusion, warmer golden-hour,
  nicer night lighting (apron floods, terminal glow, runway/taxiway lights bloom), wet-ground reflections in rain.
- Aerial perspective tuned to the Adelaide haze and Hills backdrop.
- **Exit:** day/dusk/night captures reviewed; no regression in weather visuals (ADR 0193).

## Phase 5 — Quality tiers and performance
- Quality presets (Low/Med/High) mapped to tree/shadow/detail counts; LOD and culling distances for buildings and
  suburbs; static batching / GPU instancing / mesh merging audit; asset streaming for far terrain.
- **Exit:** High ≥ 60 fps on the dev Mac at 1600×900; Low keeps the game fully playable.

## Phase 6 — Verification
- Unity EditMode + headless tests for every deterministic generator (footprint bounds, tree placement rules,
  licence register, GUID audit `scripts/audit-unity-assets.py`).
- Packaged Mac build and reference captures reviewed by Bailey; `GAME.md`, `CHANGELOG.md`, ADRs per phase.

## Suggested order and sizing
| Phase | Size | Depends on |
|---|---|---|
| 0 Baseline | S | — |
| 1 Ground & land | L | 0 |
| 2 Trees | M | 0 (1 helps) |
| 3 Buildings | XL (split into 3a audit, 3b terminal, 3c landside, 3d airside, 3e facade system, 3f suburbs) | 0 |
| 4 Lighting | M | 1–3 |
| 5 Tiers/perf | M | runs alongside; final pass last |

Phases 1, 2 and 3a can run in parallel on separate branches (disjoint files).

## Decisions (delegated to Claude by Bailey, 2026-09-30)
1. **Style:** stay stylised-clean per the art direction, but richer — more material variation, detail and lighting,
   not photoreal. Consistency with the existing aircraft and UI matters more than realism.
2. **Order:** baseline, then ground and land, then trees, then buildings (audit first, since it needs no renderer).
3. **Performance:** 60 fps at the overview camera at 1600×900 on the dev Mac is the High-tier target. A Low tier
   must stay fully playable. Do not block slices waiting for Bailey to measure.
4. **Assets:** CC0 packs (Quaternius, Kenney, Poly Haven) are allowed when registered with source, licence and
   fallback; otherwise author procedurally. No non-CC0 or online-only assets.
5. **Terminal reference:** none supplied, so Terminal 1 detail stays approximate and is labelled as such. Bailey's
   photos or plans would upgrade it; this does not block other work.
6. **Imagery:** Sentinel-2 tint only. Licensed SA Government imagery is not requested for now.

## Status
- **Bailey (2026-10-01):** plan approved; P0 closed; agents continue without ask-to-verify.
- Phase 3a (building audit): done — `scripts/audit-ypad-buildings.py`, `docs/data/ypad-buildings-audit.md`.
  Finding: only 7 of 78 heights are sourced; 71 are rule defaults and 60 buildings are unnamed.
- Phase 0 (bookmarks + capture script): done in code — ADR 0200. Optional Mac PNG/metrics whenever convenient.
- Phase 1 slices done: stand oil stains + softer ground edges (ADR 0201); apron patch
  repairs + drainage pits (ADR 0202); West Beach dunes/foam/Patawalonga outlet (ADR 0203).
  Phase 1 Golf (0206), bunkers (0207), CBD skyline (0208), seasonal tint (0209) on tip.
  **Next Phase 1:** Hills haze / aerial perspective.
- Phases 2, 3b–f, 4, 5 remain after Phase 1.

## Risks
- Performance (already tight) → per-phase budgets, LOD, tiers.
- Accuracy gaps in OSM (heights, missing houses) → mark approximate, prefer audited sources.
- Licence creep → register before merge; CI asset audit.
- Scope creep → one phase per PR; new ideas to a backlog.
