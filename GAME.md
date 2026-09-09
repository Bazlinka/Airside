## Where to resume — session handoff

- **Last updated:** 2026-09-10 (Cursor — overnight Adelaide visual rebuild)
- **Branch:** `feature/adelaide-overnight-visual`
- **Do next:** Mac Play in Unity 6.3 LTS — confirm first frame, level ground,
  Adelaide Airport HUD/ATC, dual-runway overview, **Gulf visible to the west**
  (grass deck no longer covers the water), 05/23 paint, one outer fence, ATC
  tower + east satellite, aircraft on final over the gulf then rolling out to
  A1. Night beacon should sit on the tower. EditMode 193/193 earlier this session.
- **In progress / half-done:** Overnight visual pass — Adelaide default, level
  slabs, bigger field, long 23/05 flight path, east terminal pier + satellite
  hall over Charlie, control-tower silhouette, CBD + denser suburb skyline.
  Hills pulled off the gulf. HUD says Adelaide Airport. Alpha now runs the full
  23/05 length inside the fence; paint/lights/holds follow. Grass stops at the
  West Beach dune belt so Gulf St Vincent reads on first overview (~268 m).
  Sand and shallows run the same north-south span as the gulf so the beach
  does not drop out at the edges. Car park fills the bigger pad. Night floods
  cover Stand 3 and the east satellite.
  Approach lights aim at 05 over the gulf. Aircraft glass/GSE polish. Night
  beacon is on the ATC tower. Multi-storey car park sits east of the surface
  bays. West freight shed fills the hangar side. Terminal has an upper hall.
  Dual landing lights reach further down the 05 final. An eastern arterial
  leaves through an east gate toward the CBD. Glenelg/Holdfast towers sit
  south along the gulf. Jetty lamps light both jetties at night. CBD and
  Holdfast windows glow at dusk. Climb vapor on 23. Apron is wider west and
  under the satellite. Save schema unchanged.
- **Watch for / assumptions:**
  - New games are Adelaide (`ADL`). Existing `KGC` saves keep Kingscote ATC/name;
    the 3D airfield is the Adelaide-shaped presentation for all locations.
  - Sim taxi/stand coords unchanged (14/24/34). Cross runway 12/30 is visual only.
  - Airfield construction dropped from ~10k cubes to a few hundred level pads
    so Awake can reach a first frame.
  - After Unity batchmode, revert regenerated `.meta` GUID churn — do not commit it.
- **Open question for Bailey:** none

---

## Current milestone

Toward the first playable airport. The airport sits at a named location
(Adelaide, West Beach by default; Kingscote, Port Lincoln and Coober Pedy also
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

The immediate visual target is a premium stylised-realism miniature of
Adelaide Airport at West Beach. Generated images establish composition, palette, fictional
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

1. **Mac Play** the Adelaide overnight rebuild on
   `feature/adelaide-overnight-visual` — first frame, level ground, dual runway,
   Gulf west actually visible (not under grass), Adelaide HUD/ATC. Merge if clean.
2. **Art sourcing / refine** — work `docs/art/FIRST_PLAYABLE_ART_SOURCING_CHECKLIST.md`
   in priority order (props/gear/wheels/engines → buildings → GSE → trees → CHR).
3. Mac Play: fidelity densify (#167) + collision #170 vs Approved boards — sign
   off or list concrete gaps.
4. Mac overview: day/night readability (#158) noon + midnight sign-off.
5. No new economy systems; no Companion/CloudKit.
