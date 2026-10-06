# Visual code review and implementation backlog — 6 October 2026

Continuation of Claude’s **Main branch review**, at Bailey’s request. Reviewed
`main` at `000ab56b` (including #532), superseding the initial static backlog at
`6553791` on `claude/confident-allen-c35v79`.

Claude dispatched nine audits. Its last visible summary named CORE and GROUND as
returned; subsequent background-task errors show the session limit stopped work.
Codex recovered the brief and saved backlog through Claude’s desktop window and
GitHub, then used six independent read-only reviews to complete the report. The
review does not claim to have resumed Claude’s processes or recovered every
unfinished subagent result.

**Scope:** actionable presentation and interface defects, optional visual design
work, precise ownership, proposed fixes and useful code-based acceptance. No game
source was changed. No new Unity run, packaged capture, visual sign-off or FPS
measurement was performed. “Code-proven” refers to the mechanism and data chain;
on-screen prominence remains unmeasured.

Bailey’s accepted style and historical manual playtests remain closed. Acceptance
at `bfb4a300` does not establish technical evidence for later changes. These are
development choices; the standing freight backlog and release status are unchanged.

## Concrete defect queue

The detailed packets below give triggers, exact source references, correction
boundaries and proposed checks. P2 means a functional/coordinate error worth
fixing; P3 means limited presentation polish. Priority is a recommendation, not
a declaration that the accepted game is blocked.

| ID | Priority | Code-proven mechanism | Owner | Detailed packet |
|---|---|---|---|---|
| CORE-01 | P2 | Geographic coast foam scales around the airport origin | CORE/WORLD | [1](visual-review-2026-10-06/core-ground.md) |
| GND-01 | P2 | Service wheels orbit/tumble; spare and steering wheels are selected | GROUND/ASSETS | [2](visual-review-2026-10-06/core-ground.md) |
| GND-02 | P2 | Boarding stair tyre contact is .681 m above Adelaide apron | GROUND | [3](visual-review-2026-10-06/core-ground.md) |
| AIR-01 | P2 | Shipped glTF jet fan discs cover only 43–59% of blade radius | AIRCRAFT | [1](visual-review-2026-10-06/aircraft-cockpit.md) |
| AIR-02 | P3 | Port/starboard navigation colours are reversed | AIRCRAFT/WEATHER | [2](visual-review-2026-10-06/aircraft-cockpit.md) |
| AIR-03 | P3 | Touchdown effects use ATR gate for other types; widebodies ~1 s early | AIRCRAFT | [3](visual-review-2026-10-06/aircraft-cockpit.md) |
| CK-01 | P3 | Wipers consume raw precipitation above the rain deck | COCKPIT/WEATHER | [4](visual-review-2026-10-06/aircraft-cockpit.md) |
| MAP-01 | P2 | First scheduled departures disappear until pushback | MAP | [1](visual-review-2026-10-06/hud-map.md) |
| MAP-02 | P2 | Explicit planning/selection retains stale inspection state | MAP | [2](visual-review-2026-10-06/hud-map.md) |
| MAP-03 | P2 | Field icons use runway-frame yaw on a north-up map | MAP | [3](visual-review-2026-10-06/hud-map.md) |
| WLD-01 | P3 | Suburb roof satellite lookup omits floating-origin compensation | SHADERS/WORLD | [W1](visual-review-2026-10-06/world-assets.md) |
| AST-01 | P2 | Box-kit UV projection collapses 8/12 shipped shed triangles | ASSETS | [W2](visual-review-2026-10-06/world-assets.md) |
| AST-02 | P2 | Runtime normal-mapped glTF and combined meshes omit tangents | ASSETS; after AST-01 | [W3](visual-review-2026-10-06/world-assets.md) |
| WX-01 | P2 | Cloud/rain/fog flow follows the wind-from bearing | WEATHER | [1](visual-review-2026-10-06/weather-lighting.md) |
| SKY-01 | P3 | Generated star colour/brightness is discarded by its material | SHADERS/WEATHER | [4](visual-review-2026-10-06/weather-lighting.md) |
| SKY-02 | P3 | Opaque 512 m star shell can occlude more distant traffic on the same ray | SHADERS/WEATHER | [5](visual-review-2026-10-06/weather-lighting.md) |

Two further source-confirmed lighting-coverage gaps are listed as optional H1
(additional/fill lights) and H4 (SSAO reception) below; decide intended coverage
before treating them as regressions. Their source evidence is in weather packet
items 2–3. None of the 18 findings is claimed as a new rendered observation.


## Evidence packets

- [Core/environment and ground services](visual-review-2026-10-06/core-ground.md).
- [Aircraft and cockpit](visual-review-2026-10-06/aircraft-cockpit.md).
- [HUD, map and selection](visual-review-2026-10-06/hud-map.md).
- [Weather, lights and shaders](visual-review-2026-10-06/weather-lighting.md).
- [World geometry and asset paths](visual-review-2026-10-06/world-assets.md).
- [Corrections to the initial report and workflow](visual-review-2026-10-06/report-corrections.md).

## Optional design work retained from Claude’s backlog

These are proposed improvements, source facts or dated evidence gaps. They do not
become demonstrated bugs merely because an implementation is absent.

| ID | Work | Evidence and useful next step | Owner/dependency |
|---|---|---|---|
| A1 | Consistent dynamic HUD text fitting | Some workspaces bypass existing fit helpers. `HudShell.Measure` estimates character width, and the font-size floor can still overflow. Inventory actual painted strings and define shortening/wrapping policy; pure tests can check that policy and box overlap, not exact rendered font metrics. | HUD |
| A3 | Minimum effective caption size | Document supported resolutions and the GUI-matrix scale. An 11-point caption is 8.8 effective pixels at scale .8. Raising `Max(8, size)` alone does not fix it; multiplying by scale in TextStyle would apply scale twice. Add a policy equivalent to minimum pixels / scale, with overflow handling, if desired. | HUD |
| B2 | Camera-local grass/bush detail | Remaining optional ground-cover work in the visual plan. Seeded placement can be checked outside Unity for determinism, exclusion from airside polygons and a reasoned count cap. Runtime appearance/cost are separate. | WORLD; shader handoff if needed |
| B3 | ELVIS 1 m terrain data | ADR 0236 and generator document the hook. Obtain data and read its licence before changing provenance/credits; this is not a code-only defect. | DATA |
| C1 | Tree wind sway | No existing shader wind displacement found. Extract a bounded calm/storm/weather-disabled input policy if adding it; shader compilation and motion remain native evidence. | SHADERS, then WEATHER uniform producer |
| C2 | Measured-crown profile diversity | Real LiDAR crowns carry heights/radii. Height/aspect-driven deterministic shape variation is optional design work; it does not prove the current trees are wrong. | WORLD |
| D1 | Gable/hip roof variants | Current hipped geometry is a design choice. Seeded roof variants need footprint/bounds/topology checks, preserving satellite roof colour. | WORLD; SHADERS only if material changes |
| D2 | Suburb/tree geometry budget | #531’s original report lacked native evidence; integrated #532 records native checks and a packaged player. Dedicated scenery frame-time evidence is still absent. Extract a pure topology estimator and calibrate it against native mesh totals; counts do not prove FPS. | WORLD/PERFORMANCE, sequential handoff |
| E1 | More sourced airport-building heights | Existing audit records 7/78 sourced heights and 71 defaults. Replace defaults using documented OSM/LiDAR/AIP evidence, retaining licence rows and reproducible extraction. The audit count does not prove physical accuracy. | DATA; generated AdelaideBuildings and audit artifacts |
| E2 | Terminal roof/facade detail | Further swept roof/trim/atlas work is optional. Terminal already has parapets, skylights, facade details and rooftop plant; the old generic-hangar citation did not establish that these were absent. One accepted geometry criterion per task. | WORLD |
| F1 | Weather performance budget | The plan’s ~37 FPS is inherited, dated evidence, not this review’s measurement. Runtime buffers already cache substantial work. Layer/quad budgets are growth proxies; actual allocations/draw calls/FPS need profiling. | WEATHER/PERFORMANCE |
| F2 | Correct visual-plan status | ACES, Bloom, ColorAdjustments and SSAO already exist. Record remaining design choices without implying SSAO covers every custom shader. | INTEGRATOR |
| H1 | Additional-light coverage | Custom ground/surroundings/suburb shaders omit additional lights. Missing coverage is a source fact; desired appearance and cost need a decision. PC uses Forward+, so an ordinary additional-light loop alone is incomplete. Use the URP-version-compatible Forward+ path and reasoned limits. | SHADERS; coordinate with WEATHER |
| H4 | Custom surfaces do not consume SSAO | PC SSAO is lighting-integrated; custom shader forward RGB never samples it. DepthNormals supplies AO input but does not apply AO to colour. Decide intended coverage, then use pinned URP direct/indirect AO factors. Material-map AO is separate. | SHADERS; shared H1 implementation |
| H2 | Hill/building shadow casting | Surroundings/SuburbBuildings omit a ShadowCaster pass. Decide intended coverage before adding one; absence alone is not a regression. | SHADERS |
| H3 | HDR grading mode | LDR grading with HDR/ACES is a preference hypothesis. An automatic one-line mode flip cannot prove improvement. Record PC_RPAsset.asset in scope and keep A/B comparison optional. | SHADERS/QUALITY |
| G1 | Construction dust status | Already reserved for a future simulated construction milestone. Clarify deferred status; do not manufacture an active missing feature. | INTEGRATOR |
| G2 | Animation file plans | Runtime animation is integrated while clip/controller files remain planned. Cancelling those files changes the design; record a narrow decision or explicitly defer them. | INTEGRATOR with owner design scope |
| G3 | Historical manual acceptance labels | Reconcile only the manual gates covered by owner acceptance. Preserve technical FBX/import checks, actual evidence and later-change validation. | INTEGRATOR |

Retracted: **B1 mow stripes missing** (shader already contains them), **A2 ready-to-buy
requirement-line clipping** (that state paints a different line). The claimed
persistent **2.2× coast-foam scale** is overwritten later in the same update; the
remaining geographic scaling error is detailed in CORE-01. E3’s “procedural normals are an art ceiling” is also retracted: authored normal maps are preferred; the runtime tangent omission is AST-02. A small raw viewport
must be traced through GUI normalization before asserting HUD layout overflow.

## Parallel ownership and merge order

Read-only audits can run together. Implementation needs one owner per file and
separate branches/worktrees. The old WS-B/C/D/H streams overlap and are not safe
to edit independently. Assign the following boundaries before starting:

| Owner | Exclusive paths/systems | Coordination rule |
|---|---|---|
| CORE | `AirsidePrototype.cs` coastal update and pure coast helpers | This central file is reserved while CORE changes it. HUD scale changes in it wait. |
| GROUND | `AirsidePrototype.GroundService.cs`, `AirsidePrototype.Boarding.cs`, service-wheel helpers | Coordinate shared glTF-loader or geometry-helper changes with AIRCRAFT. |
| AIRCRAFT | `AirsidePrototype.AircraftVisuals.cs`, articulation and pure aircraft-event helpers | Fan building in `AirsidePrototype.cs` hands off to CORE; nav mapping in Lights hands off to WEATHER. Shared `AirsideFlightPath` changes also need a named owner. |
| COCKPIT | `AirsidePrototype.Cockpit.cs`, cockpit environment/interior helpers | Consume WEATHER’s input contract; avoid independently changing the same weather producer. |
| HUD | HudPainter/HudShell/HudLayout and named workspace files | Airline/Map implementation belongs to MAP; reserve central GUI-scale changes through CORE. |
| MAP | `AirsidePrototype.Airline.cs`, map/inspector helpers and pure map models | The Airline partial also owns other HUD actions; explicit handoff before HUD edits it. |
| ASSETS | `ArtGltfLoader.cs`, mesh UV/tangent helpers and associated tests | AST-01 precedes AST-02. Shared loader/pivot changes hand off to GROUND/AIRCRAFT; preserve shared mesh cache ownership. |
| WORLD | Suburbs/trees/terrain/scenery geometry builders and data consumers | All edits to `AirsideAdelaideSuburbs.cs` share one owner; geographic foam construction coordinates with CORE. |
| SHADERS | All three custom scenery shaders, sky material/shader path, URP assets | H1/C1/D1/world-origin shader changes are serialized here; uniform producers hand off to WEATHER/WORLD. |
| WEATHER | Sky/Atmosphere/WeatherEffects/Lights partials and pure weather policy | Material-library edits belong to SHADERS; agree wind/rain convention before COCKPIT integration. |
| DATA | Building-height extraction, generated data/audit, source/licence register | WORLD consumes the generated output; two tools must not regenerate the same artifact. |
| INTEGRATOR | GAME.md, CHANGELOG.md, this backlog, art status and generated harness | Each worker supplies status text and evidence. Required status updates accompany each final change; harness generation runs on integrated sources. |

Workers first select a packet and state its exact file set, input/output contract,
invariants and acceptance. If they need another owner’s file, hand it off and
sequence the merge. Test files also need distinct ownership. Avoid “all independent”
claims based only on feature names.

Suggested start: MAP inspector/scheduled-marker fixes, GROUND contact/wheel fixes,
CORE foam correction and AIRCRAFT event timing can proceed with the ownership
reservations above. WORLD/SHADERS floating-origin work and WEATHER/COCKPIT input
work need their stated handoffs. Optional content follows an explicit selection.

## What code checks can establish

Pure math/data/state checks can demonstrate coordinate invariance, state
transitions, event thresholds and mesh/pivot data. Some production logic must
first be extracted from Unity-dependent partials so the generated harness can
exercise it; testing a copied formula alone does not validate the fix.

The current headless harness does not compile Unity shaders, build the actual
Unity Mesh scenery, measure real font wrapping or render screenshots. Source
keyword checks are supplementary, and budget counters do not establish draw calls
or frame time. For later behavior changes, record native compilation/runtime
limits explicitly under the repository workflow. This report runs no app tests
and does not reopen historical manual acceptance.
