## Where to resume — session handoff

- **Last updated:** 2026-09-07 (Cursor — Mac FBX bake; Bailey accepted AIR-001 v05)
- **Branch:** `cursor/air-001-v05-turboprop-8515` (PR #133) — AIR-001 v05 only; no BLD/MAT/F2–F4
- **Do next:** Start **BLD-001 v05** on a **new** branch when ready. Do **not** start the terminal, MAT-001, or F2–F4 on this branch.
- **In progress / half-done:** none
- **Watch for:**
  - PreferArtKit order is v05 → authored → lofted → v04 → …; lofted/authored source files untouched
  - Mac bake wrote ModelImporter meshes into Resources prefabs (11 kits, including AIR-001 v05). Pipeline-proof Cube/Cylinder yield remains only for unbaked Resources proofs
  - Domain EditMode: `scripts/test-unity.sh` 116/116 on this tip. `scripts/test-domain.sh` needs a .NET SDK (not installed on this Mac)
  - Do **not** start terminal v05 / MAT-001 / F2–F4 on this branch
- **Open question for Bailey:** none for AIR-001 v05 — accepted

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

- Anti-aliasing is on: 4x MSAA on the PC pipeline asset plus SMAA (high) on the runtime camera.
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

- `scripts/test-unity.sh`: 116/116 EditMode on Unity 6000.3.23f1. `scripts/test-domain.sh` remains the headless Domain/Simulation/Persistence mirror.
- A fifty-cycle simulation completes without reservation conflicts (single and dual commercial).
- Large and one-second time steps reach identical simulation state.
- When scheduled demand ≥ 4 flights/day a second commercial operates on a half-cycle stagger; fleet yields to any commercial; HUD/world show both.
- Turnaround dependencies, disruptions, priority crews and delay costs are covered by tests.
- Continuous play and offline replay produce matching operational and financial state.
- Save recovery, backward clock handling and a bounded thirty-day absence are covered by tests.
- The airport has a real-world location and a deterministic day/night cycle; a schema-1 save migrates to schema 2 (adding the location) on load.
- Airlines propose routes on a schedule; accepting one is a persisted command that survives reload and offline catch-up and pays out on every completed flight. Acceptance also refuses when the projected schedule would exceed stand capacity (12 flights/day on two stands).
- Reputation moves with on-time vs delayed departures, gates which proposals can be accepted, and raises the per-flight payment locked in at acceptance.
- Weather is deterministic from the timeline; each simulated midnight the airport pays a base running cost, a weather surcharge and crew payroll, identical under live play and offline catch-up.
- Ground-crew headcount is a persisted decision (replayed on load); the baseline leaves turnaround timing byte-identical to before, extra crew shorten it, understaffing lengthens it.
- Each midnight publishes a daily operations report (flights, income, delays, running cost, net cash, reputation); latest seven kept; HUD shows the latest.
- Operations Efficiency research (2500, one simulated day) permanently reduces base daily running cost by 100; start is command-replayed. The daily finance brief subtracts that discount from expected operating cost.
- Passenger Services research (3500, one simulated day) unlocks after Ops Efficiency and permanently adds +$75 route income per departed commercial; start command `start-research-passenger-services` is replayed on load (decision 0023). `scripts/test-domain.sh`: 96 deterministic Domain/Simulation/Persistence tests pass (Unity edit-mode still needs Mac).
- A buildable third stand (8000, `build-stand`) expands capacity; taxi, ground traffic and the HUD use it; two-stand seeds stay identical.
- Three consecutive negative day closes declare insolvency: the simulation freezes, commands refuse, and an `"Insolvent"` event is logged (identical under large and small time steps; rebuilt by replay).
- Named taxi routes connect both stands through shared reserved segments.
- The event history produces an ordered, player-readable account of each flight.
- Taxi movements release shared segments progressively instead of locking the whole route.
- A competing owner cannot enter an occupied segment, and prolonged waits produce a diagnostic.
- A ground-traffic fleet (`GT-201` arrive/depart, `GT-202` repositioning) shares the taxi segments and stands through the reservation table without ever blocking the primary flight; a single-file corridor lock keeps at most one fleet aircraft on the A1/A2 taxiway at a time, and a free corridor goes to the longest-waiting aircraft (30 edit-mode tests, including a forty-cycle soak asserting the corridor invariant, no starvation, and zero primary-flight conflicts).
- Fleet aircraft move identically under large and small time steps.
- The project compiles in Unity 6.3 LTS and builds a macOS player.
- The runtime HUD uses the approved REF-004 palette (`AirsideTheme`: Runway Ink panels, Cloud
  text, Coastal Blue buttons, Safety Yellow caution, Clear Green on-time, Signal Red delay).
  Unity 6.3 Play: no `Arial.ttf` / PanelSettings theme warnings; Toolkit wordmark overlays present.
- Batch C / WLD / PRP glTF kits load at runtime via `ArtGltfLoader` with primitive fallbacks.
  Batch F1 **AIR-001 v05** (`mdl_regional_turboprop_01_v05`) is preferred ahead of authored/lofted/v04;
  Mac **Bake Authored FBX Prefabs** put ModelImporter meshes in Resources; Bailey accepted the v05 result.
  Batch E icons remain as previously integrated. See `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`.

## Next work

1. **BLD-001 v05** on a new branch (AIR-001 v05 accepted; do not start the terminal on this branch).
2. After BLD-001: **MAT-001** shared URP material family (still F1; separate tips).
3. Optional: build Editor Addressables groups so init stops looking for missing player content.
4. No F2–F4 until F1 hero read is accepted; no new economy systems; no Companion/CloudKit.
