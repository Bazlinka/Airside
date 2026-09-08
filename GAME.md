## Where to resume — session handoff

- **Last updated:** 2026-09-08 (Claude — audit items 6 + 8)
- **Branch:** `feature/visual-perf-p3`, stacked on `p2` → `p1` (PRs #140, #141, #142)
- **Do next:** **Mac Play verify all three PRs together**, then merge #140, #141, #142
  in that order. Look at: the aircraft silhouette (wing/tail are lofted now, gear
  sits under the nacelles), small props popping (2% small-mesh culling), and any
  colour bleed between objects (would mean a shared material got mutated).
- **In progress / half-done:** none — items 1–8 landed, tests green, look unverified.
- **Watch for / assumptions:**
  - Anything mounted on the wing must come from `wing_station` / `wing_slab` in
    `scripts/generate-air-001-v05.py`. Hard-coding a y is exactly how the flaps,
    ailerons, spoilers, tracks, fairings and wicks ended up floating under the wing.
  - `m_BrgStripping: 1` is **StripAll**, not KeepAll. KeepAll is `2`.
  - Materials from `AirsideMaterialLibrary.CreateShared` are **shared** — never
    mutate one; per-object tinting goes through `Renderer.material`, which clones.
  - The art generators need `assimp` for the best FBX output but now fall back to
    `scripts/write_ascii_fbx.py`. The fallback writes per-face normals, so lofted
    surfaces are faceted — consistent with the fuselage, which is faceted too.
  - **Open art-direction question:** the AIR-001 docstring says "high-wing regional
    silhouette", but the wing sits at y≈1.22 against a fuselage centred on y≈1.18 —
    that is a mid-wing. Not changed here; it is Bailey's call, and it also decides
    whether the main gear belongs in the nacelles (as now) or on fuselage sponsons.
  - Still open from the audit: item 7 (cache aircraft part transforms; ~20k string
    compares/frame), item 9 (emit `material:` per mesh from the generators), item 10
    (replace the regex glTF parser). Plus ~9 per-frame `EnableKeyword("_EMISSION")`
    sites, and `door_frame_fwd` being a solid box that swallows `door_fwd`.
  - Do **not** run `scripts/rebuild-and-open-mac.sh` on a feature branch
- **Open question for Bailey:** the high-wing vs mid-wing call above.

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

- `scripts/test-unity.sh`: 124/124 EditMode on Unity 6000.3.23f1. `scripts/test-domain.sh` remains the headless Domain/Simulation/Persistence mirror.
- Batch F1 (AIR/BLD/MAT) and Batch F2 (vehicles/people) are on `main`.
- Batch F3 setting modules (eucalyptus, scrub, fence/gate, forecourt, context terrain)
  prefer authored kits with procedural fallbacks; operational geometry unchanged.
- Batch F4: UI-ICO-005 system icons on Toolkit chrome; VFX-001…004 Resources/Art
  prefabs; `AirsideReusableMotion` for ANM rates.

## Next work

1. Merge **visual polish** (#139) when ready.
2. Keep pushing first-playable **visual polish** (lighting soak, presentation bugs,
   art fidelity) — standing goal; no new economy / Companion.
3. Optional: Editor Addressables groups for player catalog.
4. No new economy systems; no Companion/CloudKit.
