## Where to resume — session handoff

- **Last updated:** 2026-09-09 (Codex — free CC0 rain intake)
- **Branch:** `art/free-touchdown-audio` (PR pending)
- **Do next:** Merge the rain PR, then continue the checklist in priority order:
  P0 aircraft parts → buildings → vehicles/GSE → veg/characters. AUD-002 wind,
  AUD-004 rain and AUD-005 click are integrated; source distinct cleared alert,
  coast, touchdown and engine assets after higher-priority visual kits.
- **In progress / half-done:** First-playable **refine / art sourcing** — no new
  economy or Companion. Deferred polish in `BUGFIX_PASS_100` stays optional.
- **Watch for / assumptions:**
  - Checklist rows marked Placeholder still need authored replacements; AUD-005 remains partial (click only)
  - Register + StreamingAssets sync required with every new runtime art commit
  - The generated `double` to `float` source errors in `AirsidePrototype.cs` were repaired in this intake; local Unity EditMode is still blocked while the package resolver initialises UGUI/Input System/URP dependencies, so do not treat the import as a green test run yet
  - Save schema unchanged
- **Open question for Bailey:** none

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
