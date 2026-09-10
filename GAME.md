## Where to resume — session handoff

- **Last updated:** 2026-09-10 (Cursor — aircraft refine Pass B: gear retract)
- **Branch:** `cursor/bare-adelaide-field-bc75` (off latest `main`)
- **Do next:** Pass C — cabin/cockpit glass sits inset in the fuselage. From F
  the panes should read as openings in the skin, not floating rectangles or a
  glass cage. In the v06 kit the window meshes are already close to the skin
  (`cabin_window_*` x≈±0.8 against a fuselage of half-width ≈0.82), so prefer the
  kit's own glass with a glass material and only nudge offsets inward if a pane
  proud of the skin is confirmed on Mac — do not add extra glass cubes or bake
  window text. Check `UpdateCabinWindowGlow` and the glass material path, not new
  geometry. Verify on Mac: Play, F, one circuit.
- **Just landed (Pass B):** gear retract folds each leg from its top hinge and
  carries the nested wheels/oleo/scissors, instead of swinging the whole leg off
  the belly. `RebakeGearStrutPivots` rebakes `Gear nose/L/R` to their top hinge
  before the wheels are nested (own-mesh rebake, so nested parts are not
  re-homed); the retract pass and the rebake share
  `AirsideAircraftParts.IsGearStrut`, proven disjoint from the spin set. Fold
  geometry still needs a Mac Play check.
- **Landed earlier (Pass A):** main + nose wheels spin in place on
  landing/takeoff and stop airborne. Root cause was flat kit nodes with
  world-baked meshes (same trap the props have): `RollLandingGearTires` swept
  each wheel around the fuselage origin. Fix: `RebakeWheelPivots` rebakes each
  tyre/wheel/rim to its axle centre; the roll pass and the rebake share
  `AirsideAircraftParts.RollsInPlace`. Both passes locked headless by
  `AircraftPartsTests`; run `scripts/test-unity.sh` on a Mac before merging.
- **In progress / half-done:** Bare field, circuit loop, real-metre markings,
  and presentation cues on the v06 turboprop. Taxi/stand/pushback still exist
  on the phase enum (1 s each) for save compatibility but are not drawn. Night
  lighting stays pinned off. Dormant building spawners remain in
  `AirsidePrototype` but are not called.
- **Watch for / assumptions:**
  - Combined pads keep wet-surface collector names (`Runway W`, `Apron `, `Taxiway A`, `Infield grass`, `Taxi centre`, `Taxi exit centre`)
  - Paint families keep collector prefixes (`runway_centre`, `runway_edge_left`,
    `runway_edge_right`, `runway_threshold`, `Aiming point`, `TDZ marks`)
  - Do not stretch or call `PlaceWorldMarkings()` / WLD-001 on the bare field;
    that kit is the 155 m miniature
  - `AirsideRunwayMarkings` holds no UnityEngine types. Spawn from it; do not
    duplicate metre literals in `BuildBareAdelaideField`
  - The 300 m TDZ pair is centred on `AirsideFlightPath.TouchdownX`. Marks fit
    the path; do not move the path to fit the marks
  - `Infield grass` is now buried under the terrain rather than removed. Its top
    is at -0.41, below the terrain at -0.045, so it is hidden but still
    collectable. Do not "tidy it up"
  - Everything about the terrain's shape and blending lives in
    `AirsideTerrainField`, which holds no UnityEngine types on purpose: the
    Editor baker and the headless tests sample the same functions, so the shipped
    ground and the thing under test cannot drift. Re-run `scripts/bake-terrain.sh`
    after changing it or after regenerating the maps
  - The operational plateau is pinned dead level across X [-64, 66] × Z [-12, 60].
    If a pad, stand or taxi node is ever moved outside that box it will end up
    over sculpted ground; widen the plateau in the same commit
  - `Kingscote terrain` is in `AirsideStaticWorld.IsDynamic` so `Collect` skips
    its subtree entirely and it can never reach `StaticBatchingUtility.Combine`
  - The TerrainLayer maps under `Art/Textures/Terrain/` are Editor-imported and
    are deliberately excluded from the StreamingAssets sync. The apron concrete
    v03 under `Art/Textures/Surfaces/` *is* runtime-loaded and does sync;
    `AirsideMaterialLibrary` already prefers v03 over v02 and v01
  - High path must not drop bloom/SSAO/shadows; Medium is the cheaper ladder
  - `scripts/test-domain.sh` does not compile Presentation; Unity EditMode is required for `PresentationLayoutTests`
  - Because of that gap, `work/flightcheck` is the only thing that compiles the
    real flight geometry here, and any `PresentationLayoutTests` assertion it does
    not mirror is unverified. One had already gone stale that way: it compared the
    landing rollout at a progress that `TouchdownProgress` had moved past. Derive
    sample points from the constants instead of writing literals, and mirror the
    check in the harness. New: `FlightPath_TouchdownSitsOnTheThreeHundredMetreTdz`
    and `CircuitCues_*` — mirror them the next time that harness runs
  - Phase progress is read at the fractional presentation clock
    (`AirsideAircraftMotion.PhaseProgress`), not sampled at the simulated second.
    It is clamped to 1, which is what keeps a held departure parked at the
    hold-short bar instead of sliding onto the runway ahead of its clearance
  - A flight waiting on a reservation has `PhaseStartedAt` pushed forward every
    stalled second, so a fractional read would creep forward and snap back. That
    case falls through to the simulated value on purpose — do not "simplify" it
  - The runway holding position lives in `AirportTaxiNetwork.RunwayHoldingPositionZ`
    because both layers need it: presentation draws the bar there and the
    simulation decides an arrival has vacated there. Do not fork the number
  - Takeoff rotation is derived (`AirsideFlightPath.RotateProgress` ≈ 0.75), not a
    literal. Gear retract, landing lights and runway spray all key off it
  - Landing lights are not gated on night. Pinned daylight still shows them in
    follow (emissive + brighter spots). They stay on through skipped ground
    phases and go out after `GearRetractProgress`
  - Props keep spinning in `Departed` (takeoff RPM). AtStand only kills them
    when the full taxi loop is on; the circuit idles them instead
  - Cabin doors stay shut on the circuit (`CabinDoorBias` is 0 while
    `SkipGroundTaxi`). Do not restore stand-door theatre
  - Touchdown smoke/skid is presentation-only again. It keys off
    `HasTouchedDown` / `TouchdownProgress`, not the Approach→Landing seam
  - `AirsideFlightPath` and `TaxiVisualPath` are presentation-only. Phase timing
    stays in Simulation, so frame rate and these curves cannot change simulation
    outcomes
  - The horizon dome is a background-queue backdrop with no depth write. If it
    goes back to opaque geometry, every aircraft past 165 m disappears again
  - Save schema unchanged
- **Decisions:** visible world is ADR 0032. Circuit loop is ADR 0033.
  Aircraft motion read remains ADR 0030.
- **Open question for Bailey:** keep the dormant building/GSE spawners in
  `AirsidePrototype` for a later restore, or delete that code now that the
  field is bare?

---

## Current milestone

Toward the first playable airport. The airport sits at a named location
(Kingscote, Kangaroo Island by default; Port Lincoln and Coober Pedy also
available) and runs a day/night cycle — one simulated day every 20 real minutes,
driving the sun and ambient light and shown on the HUD. Airlines propose scheduled routes on a timer; the player accepts (or declines) an offer and every
completed flight then pays a recurring per-flight amount. Schedule demand is capped by stand
capacity (`StandCount × 6` flights/day) so acceptance cannot outrun the airfield. The airport's reputation
(0–100) rises with on-time departures and falls with delays; airlines gate their
proposals on it and pay more when it is high. Deterministic weather changes
through the day and, with a base fee and crew payroll, is charged as a daily
running cost — so the airport now has expenses it must cover, not just income.
The player employs ground crew: the baseline runs turnarounds normally, extra
crew speed them up, and understaffing stretches them into delays. The player can
buy a third stand for 8000 — the first buildable capacity upgrade. Research unlocks
progression: Operations Efficiency (−$100/day running cost), then Passenger Services
(+$75 route income per departed commercial). If cash stays negative across three
consecutive day closes, the airport is declared insolvent and the simulation stops.

When accepted route demand reaches four flights/day, a second commercial aircraft operates alongside the first (stands never double-book). Still current: simultaneous traffic. A ground-traffic fleet shares the airfield with the primary flight: `GT-201` runs a repeating arrival / stand dwell / departure schedule on whichever stand the primary flight is not using, and `GT-202` repositions in and out via a run-up bay without using a stand. Fleet aircraft reserve a single-file corridor lock for the whole time they are on the A1/A2 taxiway, so they queue rather than meet head-on. The primary flight keeps absolute priority on the segments themselves; a hold beyond ten seconds is explained by the traffic wait monitor. The design is deadlock-free by construction.

## Visual asset contract

The approved visual direction, exact asset paths, animation responsibilities and
production order live in
`docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` (decision 0022). The first playable
moves from procedural primitives to approved art in batches, with primitives kept
as fallbacks during integration.

The immediate visual target is a premium stylised-realism miniature of a regional
Australian airport. Generated images establish composition, palette, fictional
liveries and UI direction. Runtime aircraft, buildings and service vehicles remain
true 3D assets; animation and VFX mirror simulation state and never drive it.

- Anti-aliasing is on: High keeps 4× MSAA on the PC pipeline plus SMAA (high) on
  the runtime camera; Medium uses 2× MSAA + SMAA. Vsync is on (`vSyncCount` 1).
- The post stack runs a deliberate grade only — the template default profile's depth of field, motion blur, lens distortion, chromatic aberration, lens flare and panini are pinned off.
- The simulation keeps running when the window loses focus (`runInBackground`).
- Ground traffic only uses stands the airport has actually built; with every built stand occupied by a commercial it holds off-field (leaving the corridor free) rather than taxiing to an unbuilt Stand 3.
- Every reported delay names a cause the player can act on — understaffing included, not only cabin-cleaning disruptions.
- The daily finance brief projects income from every commercial aircraft currently operating, not just the first.
- A player command issued before the first simulated tick (second 0) is replayed on load like any other.

## Invariants

- Domain and simulation rules remain independent of Unity scenes.
- Any save-schema change ships with an explicit version bump and a migration path (see `AirsideSaveData.Migrate`).
- Time comes from an injected clock.
- Random choices come from a seeded source.
- Runways, taxiways and stands must be reserved before use.
- Frame rate must not change simulation outcomes.
- No external data or asset enters the project without a recorded licence.

## Run it

Open `game/Airside` in Unity 6.3 LTS and press Play.

- Space: pause or resume simulation
- Tab: switch between 1× and 4× time
- Right-drag: orbit camera
- Scroll: zoom
- WASD: pan overview
- F: follow aircraft (press again to cycle commercials)
- O: return to overview
- P: hire a priority turnaround crew while the aircraft is at stand

Run checks with `scripts/test-unity.sh`. Build the local Mac app with `scripts/build-mac.sh`.
Without a Mac Unity editor, `scripts/test-domain.sh` runs the same Domain/Simulation/
Persistence EditMode tests headlessly via `dotnet test` (.NET 8 SDK) — a fast
supplementary check, not a replacement for a real Unity run before merging.

## Current evidence

- **Circuit flight-state cues** on `cursor/bare-adelaide-field-bc75`: same v06
  turboprop. Gear down on the runway, up after `RotateProgress + 0.05`. Landing
  lights on through approach / land / skipped wait / takeoff roll, off after
  retract, readable in pinned daylight. Props keep takeoff RPM in climb-out.
  Touchdown smoke fires once at `TouchdownProgress` (the 300 m TDZ), not on
  short final. Cabin doors stay shut on the circuit.

- **Real-metre runway markings** on `cursor/bare-adelaide-field-bc75`: the 3 100 ×
  45 m slab now has ICAO-ish threshold bars (12 per end), aiming points at 400 m,
  dashed centreline (30/20), 0.90 m edge lines, and TDZ pairs at 150/300/600/750/900 m.
  Numbers live in `AirsideRunwayMarkings` (no UnityEngine). The 300 m pair is
  centred on `AirsideFlightPath.TouchdownX` (-1250). Paint is combined per family,
  not hundreds of cubes. WLD-001 is not used.

- **Bare Adelaide field** on `cursor/bare-adelaide-field-bc75`: visible world is
  one 3 100 × 45 m runway (YPAD 05/23), 3 400 × 2 309 m / 785 ha empty ground,
  one turboprop and pinned daylight. No buildings, cars, signs, taxiways or
  decorative lights. Decision 0032. `scripts/test-domain.sh` is the headless
  check for the new metre constants.

- CC0 Unity Terrain ground on `feature/cc0-terrain-ground`: 256 × 220 × 8 m
  TerrainData (heightmap 257, alphamap 256), four CC0 TerrainLayers on the
  built-in URP Terrain Lit shader, plus a Poly Haven worn-concrete apron.
  Operational plateau dead level at **-0.0450 min and max** across X [-64, 66] ×
  Z [-12, 60], lowest pad **3.5 cm** proud; normalized heights **0.1053–0.5538**
  (no clamping); relief **3.57 m over 220 m**; overview core **67.1% dry grass,
  15.0% green, 17.8% worn dirt**; dirt shoulder **2.20–3.20 m**; lag correlation
  decays monotonically **0.799 at 11 m → 0.430 at 32 m** with no resurgence at
  any tile size. Albedo tile-scale luminance spread **0.5–2.0 points** with
  detail std **8.9–23.8**. `scripts/test-domain.sh` **200 passed** (19 new
  terrain tests, mutation-checked). Decision 0031. **The bake has not been run:
  needs `scripts/bake-terrain.sh` on the Mac, then Unity compile, Mac build and
  packaged day/dusk/night/rain QA.**

- Runtime airfield performance **P0–P2 plus GPU-state + paint/probe/kit-combine
  pass, merged to `main` as PR #183**: combined operational pads (6)
  replace 745 Terrain11 tiles; textures/materials cached; Addressables on demand;
  High/Medium ladder; probe bands. Per-frame `Renderer.material` clones removed;
  scene index; one star mesh; probes `RenderProbe` after world combine. Taxi
  paint is strips not 1 m cubes; kit fence/forecourt/GSE/planters/chocks/belt
  loader stamp cached combined meshes; static combine skips moving GSE/clouds/
  birds/boats/`antenna_dish`. Ambient audio `Resources.Load` is deferred off
  Awake. Medium thins fillet lights, fence rails, window PointLights and scrub.
  `AirsidePrototype.cs` brace depth 0. Decision 0029. Mac Play visual-first-frame
  still required.

- Layering / collision / route **100-fix** on `cursor/layering-collision-bugfix-100-d7f0`: dogleg lead-ins, apron throat, stand spacing 14/24/34, GT off-field + run-up bay, selective yield, `scripts/test-domain.sh` **177 passed** (`CollisionPass100Tests`).

- Fidelity-board integration **merged via #167**: scrub/terrain v02, surface
  `tx_*_v02` + wet concrete, ARFF prefab v02 densify, CHR dual wands,
  turnaround GSE zone layout, docs Approved · Integrated.
- `scripts/test-domain.sh`: **136/136** on the integrate branch before merge.
- Day/night readability merged via #158 (Mac noon/midnight overview pending).
- 50-item bugfix pass merged via #157.
- Eucalyptus VEG-001 v02 merged via #156 (Mac overview vs REF still pending).
- Forecourt PRP-003 v02 merged via #155 (Mac overview vs REF still pending).
- Fence/gate PRP-002 v02 merged via #154 (Mac overview vs REF still pending).
- Character kits CHR-001/002 v02 merged via #153 (Mac overview/follow vs REF-003
  still pending).

## Next work

1. Watch the loop in Unity Play (F, one circuit, no HUD). Then **one taxiway
   and one stand** only when Bailey says so.
2. No new economy systems; no Companion/CloudKit; no buildings/GSE restore.
