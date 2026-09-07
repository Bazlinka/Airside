# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

<<<<<<< HEAD
- **Last updated:** 2026-09-07 by Cursor (Addressables provider + lofted turboprop + env densify; stacked on HUD fix)
- **Branch / working tree:** `cursor/addressables-provider-env-cc2d` (stacked on #127 HUD fix)
- **Do this next:** Bailey squash-merge #127 then #128. Mac Play soak: no Arial/PanelSettings warnings; lofted turboprop; denser fence/vegetation; Addressables keys load; Toolkit without IMGUI. Then #129 motion/brand.
- **In progress / half-done:** HUD LegacyRuntime + theme TSS; AirsideResourcesProvider; lofted turboprop synced; south fence + vegetation + road shoulders; Toolkit early-outs IMGUI.
- **Watch out for:** Linux cloud has no Unity — Mac `scripts/test-unity.sh` before merge. Do not overwrite `*_v04` with lofted geometry. Keep StreamingAssets sync.
- **Open questions for Bailey:** authored FBX overwrite for terminal/turboprop next, or keep procedural loft density?
- **Visual assets:** Lofted turboprop **Integrated** (prefer `…_lofted_v01`); Addressables Resources provider **wired**; env densify **code-side**; HUD startup **fixed** on this stack.
=======
- **Last updated:** 2026-09-07 by Cursor (motion/brand/wet/coast ambient polish)
- **Branch / working tree:** `cursor/materials-motion-brand-cc2d` (also open: #127 HUD font, #128 Addressables/lofted/env)
- **Do this next:** Bailey squash-merge #127 → #128 → this PR on Mac; run `scripts/test-unity.sh`; Play soak (no Arial/PanelSettings warnings; lofted turboprop; gear/cargo doors; dawn splash + wordmark on overlays; wet probe refresh).
- **In progress / half-done:** Presentation polish only — gear doors, cargo door, coast ambient, wet surface set, apron probe refresh, quieter night bloom, brand wordmark on pause/away/insolvency.
- **Watch out for:** Linux cloud has no Unity — Mac EditMode required before merge. Do not force-push main. Keep StreamingAssets path intact.
- **Open questions for Bailey:** authored FBX next, or more procedural densify?
- **Visual assets:** Brand overlays **strengthened**; motion/life **extended**; materials wet/probe **extended**. Heroes/env densify land with #128.
>>>>>>> aea5782 (Polish motion, brand overlays, wet surfaces and coast audio)


Full start-of-session and end-of-session checklists are in `AGENTS.md` →
"Session handoff protocol".

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

- `scripts/test-domain.sh` compiles Domain/Simulation/Persistence and runs 96 deterministic NUnit tests (including concurrent-flight soak, research progression, and step identity) headlessly via `dotnet test`. Unity edit-mode via `scripts/test-unity.sh` still needs a Mac editor.
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
  text, Coastal Blue buttons, Safety Yellow caution, Clear Green on-time, Signal Red delay) —
  **unverified in Unity**, written and reviewed without an editor available; needs a Play check.
- Batch C / WLD / PRP glTF kits load at runtime via `ArtGltfLoader` with primitive fallbacks.
  Batch E service icons and dark panel regenerated; operation/economy/service icons draw in the
  HUD. **Integrated, not Verified** — Bailey Unity Play soak still required. See
  `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md` and `docs/art/prompts/batch-e-ui-generation-2026-09-06.md`.

## Next work

1. Bailey squash-merge HUD fix (#127) then Addressables/lofted/env PR; Mac `scripts/test-unity.sh` + Play soak.
2. Confirm lofted turboprop + denser fence/vegetation; Toolkit HUD without IMGUI; packaged StreamingAssets still shows Batch C/WLD/PRP.
3. Next code track: URP materials/lighting polish, motion/life (gear/doors), brand splash on menu/load; authored FBX when ready.
   No new economy systems; no Companion/CloudKit; no WorldScale re-layout until presentation PRs flow cleanly.
