# Airside active brief

## Vision

Airside is a real-time, persistent airport management game for Mac. The player designs and manages the system while aircraft, passengers and ground services operate automatically. Watching the airport work should be satisfying, and every delay should have an understandable cause.

## Where to resume — session handoff

This block is the first thing to read and the last thing to update. Any tool
(Claude, Cursor, ChatGPT via a person) overwrites it when it stops work, so the
next session can continue without seeing the previous conversation. Keep it short.

- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: ops-event toast)
- **Branch / working tree:** open draft PRs into `main`:
  - #20–#23 overnight look / Stand3 / insolvency HUD / follow cycle
  - #24–#27 HUD audio / research toast / status colours / autosave chip
  - #28 night aerodrome beacon + dual-flight phase HUD
  - `cursor/ops-event-toast-38b9` — flash latest ops event on the HUD
- **Do this next:** Merge overnight drafts (any order; #21 is sim). Bailey Batch C/E review.
- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: aerodrome beacon + dual HUD)
- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: autosave indicator)
  - #24 research bar + mute + StandZ presentation
  - #25 research-complete toast
  - #26 cash / reputation / day-est colour cues
  - #27 autosave Saved chip
  - `cursor/beacon-dual-hud-38b9` — night aerodrome beacon + dual-flight phase line
  Unity Play soak for HUD + night beacon.
  - `cursor/autosave-indicator-38b9` — brief Saved chip after autosave
  Unity Play soak for HUD polish stack.
- **In progress / half-done:** Batch C Generated/Modelled. Batch D greybox + WLD in #20.
  Batch E UI candidates Generated (not integrated). Passenger Services shipped. Harness green.
- **Watch out for:** fleet corridor invariants (0006–0009). Art **0022**. Research **0023**.
  Primitives until Batch C Approved+Verified. Batch E not in runtime Assets until Approved.
  Do not merge #19 without Bailey Approve.
- **Open questions for Bailey:** Approve Batch C and/or Batch E looks, or request `_v02`?
- **Visual assets:** Batch A Approved; Batch B Approved (surfaces Integrated); Batch C Generated/Modelled; Batch E UI Generated — review required
- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: HUD status colours)
- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: research toast)
- **Last updated:** 2026-09-06 by Cursor (overnight keep-going: HUD audio + research bar)
  - #20 `cursor/overnight-polish-38b9` — WLD greybox + miniature look
  - #21 `cursor/stand3-ground-traffic-38b9` — Stand 3 fleet lead-in/Z
  - #22 `cursor/insolvency-hud-38b9` — insolvency / cash warning HUD
  - #23 `cursor/follow-cycle-38b9` — F cycles dual commercials
  - #24 `cursor/hud-audio-polish-38b9` — research progress bar, M mute, StandZ presentation
  - #25 `cursor/research-toast-38b9` — research-complete HUD toast
  - `cursor/hud-status-colours-38b9` — cash / reputation / day-est colour cues
- **Do this next:** Merge overnight PRs (any order; #21 is sim). Bailey review of Batch C /
  Batch E when free. Unity Play soak for look + HUD + mute + research toast + status colours.
  - `cursor/research-toast-38b9` — research-complete HUD toast
  Batch E when free. Unity Play soak for look + HUD + mute + research toast.
  - `cursor/hud-audio-polish-38b9` — research progress bar, M mute, StandZ presentation
  Batch E when free. Unity Play soak for look + HUD + mute.
- **In progress / half-done:** Batch C Generated/Modelled. Batch D greybox + WLD polish in #20.
  Batch E UI candidates Generated (not integrated). Passenger Services shipped. Headless harness green.
  Keep primitives until Batch C is Approved and Verified. Batch E must not enter runtime Assets until Approved.
  Do not merge #19 Batch E runtime integration without Bailey Approve.
- **Last updated:** 2026-09-06 by Cursor (follow-camera cycle for dual commercials)
- **Branch / working tree:** `cursor/follow-cycle-38b9` → PR into `main`
- **Do this next:** Bailey review of Batch C look and Batch E UI candidates at 24 px /
  nine-slice previews. Unity Play soak for HUD theming when free.
- **In progress / half-done:** Batch C Generated/Modelled. Batch D greybox shipped on main.
  Batch E UI-ICO-001–004 and UI-PNL-001–003 are Generated candidates (not integrated).
  Passenger Services shipped. Headless domain harness + HUD palette on main.
- **Last updated:** 2026-09-06 by Cursor (insolvency HUD overlay)
- **Branch / working tree:** `cursor/insolvency-hud-38b9` → PR into `main`
  (also open: #20 overnight polish, #21 Stand 3 ground-traffic)
- **Do this next:** Merge open overnight PRs (#20/#21/#22). Bailey review of Batch C / Batch E when free.
  Unity Play soak for HUD, insolvency overlay, and miniature look.
- **In progress / half-done:** Batch C Generated/Modelled. Batch D greybox on main; WLD polish in #20.
  Stand 3 ground-traffic fix in #21. Insolvency HUD on this branch. Batch E UI candidates Generated
  (not integrated). Passenger Services shipped. Headless harness 96/96 on this tip.
- **Last updated:** 2026-09-06 by Cursor (merge wave: #20–#21 in; continuing)
- **Branch / working tree:** `main`
- **Do this next:** Continue merging #22–#30 then #19. Batch C Integration after wave. Unity Play soak.
- **In progress / half-done:** Batch C/E **Approved**. Overnight polish merge wave on `main`.
- **Open questions for Bailey:** none on C/E Approve.
- **Visual assets:** Batch A/B Approved (B Integrated); Batch C **Approved**; Batch E UI **Approved**


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

## Next work

1. **Merge open overnight PRs** (#20 polish, #21 Stand 3 lead-in, #22 insolvency HUD) then continue Batch D look.
2. **Bailey review of Batch C** when convenient (not blocking further work).
3. **Keep building** — remaining Batch D / miniature look polish; no unapproved economy systems.
4. **Unity Play soak** whenever Bailey has the editor (textures, lights, dual commercials, HUD, insolvency).
5. **Batch C Integration** after Approve (wire glTF prefabs; primitives stay fallback).
1. **Merge open overnight PRs** (#20 polish + Stand 3 ground-traffic lead-in) then continue Batch D look.
1. **Merge overnight polish** (`cursor/overnight-polish-38b9`) then fix Stand 3 ground-traffic lead-in.
4. **Unity Play soak** whenever Bailey has the editor (textures, lights, dual commercials, HUD).
