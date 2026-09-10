## Where to resume — session handoff

- **Last updated:** 2026-09-10 (Cursor — aircraft logic pass)
- **Branch:** `main`. Everything through the aircraft-logic pass is merged —
  performance (PR #183) and flight realism + aircraft logic (PR #184). There is
  no outstanding branch; start new work from `main`.
- **Do next:** **Mac Play, and this needs a Unity compile first — nothing in this
  pass has been through the Unity editor.** Watch a full arrival and a full
  departure at 1× and then at 4×, on both the overview and the follow camera.
  What to judge: the aircraft should move continuously at both speeds (the 1 Hz
  staircase is what made 4× look so much worse); taxi should hold one steady
  speed out as well as in; a departure should stop at the hold-short bar clear of
  the runway and then swing onto the centreline along a curve rather than
  snapping round; an arrival should be a distant speck on final rather than
  popping into existence. Ground vehicles, stand equipment and people are parked
  by `AirsideFocusMode.AircraftOnly` — flip that to `false` to bring them back.
- **In progress / half-done:** Aircraft-focus mode is a deliberate temporary
  simplification at Bailey's request, not a deletion — one flag, one place.
  **Not done, and it is the real answer to "there aren't enough taxiways / map
  isn't big enough":** the field still has a single A1/A2 taxiway, so a departure
  holding short and an arrival vacating the runway share the same chord. The
  simulation stops them colliding on the runway, but they pass close on A1. That
  needs a parallel taxiway with separate arrival-exit and departure-entry
  connections, a longer runway and multiple exits — markings, lights and
  thresholds all need eyes on them in Unity, so it was not attempted here.
  Presentation performance P0–P2 and art sourcing carry over unchanged.
- **Watch for / assumptions:**
  - Combined pads keep wet-surface collector names (`Runway W`, `Apron `, `Taxiway A`, `Infield grass`, `Taxi centre`, `Taxi exit centre`)
  - High path must not drop bloom/SSAO/shadows; Medium is the cheaper ladder
  - `scripts/test-domain.sh` does not compile Presentation; Unity EditMode is required for `PresentationLayoutTests`
  - Because of that gap, `work/flightcheck` is the only thing that compiles the
    real flight geometry here, and any `PresentationLayoutTests` assertion it does
    not mirror is unverified. One had already gone stale that way: it compared the
    landing rollout at a progress that `TouchdownProgress` had moved past. Derive
    sample points from the constants instead of writing literals, and mirror the
    check in the harness
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
  - `AirsideFlightPath` and `TaxiVisualPath` are presentation-only. Phase timing
    stays in Simulation, so frame rate and these curves cannot change simulation
    outcomes
  - The horizon dome is a background-queue backdrop with no depth write. If it
    goes back to opaque geometry, every aircraft past 165 m disappears again
  - Save schema unchanged
- **Decisions:** this pass is recorded in
  `docs/decisions/0030-aircraft-motion-and-focus.md` — the fractional-clock read
  and the interpolation design that was tried and rejected, the two deliberate
  fall-throughs to the simulated second, the shared holding position, the
  line-up arc, aircraft-only focus, and the deferred taxiway rebuild.
- **Open question for Bailey:** the taxiway/runway rebuild above is a scoped
  follow-up — worth doing next, or is the aircraft loop the priority first?

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

1. **Art sourcing / refine** — work `docs/art/FIRST_PLAYABLE_ART_SOURCING_CHECKLIST.md`
   in priority order (props/gear/wheels/engines → buildings → GSE → trees → CHR).
2. Mac Play: fidelity densify (#167) + collision #170 vs Approved boards — sign
   off or list concrete gaps.
3. Mac overview: day/night readability (#158) noon + midnight sign-off.
4. Mac overview backlog: eucalyptus (#156), forecourt (#155), fence (#154),
   characters (#153) vs refs if not yet signed off.
5. No new economy systems; no Companion/CloudKit.
