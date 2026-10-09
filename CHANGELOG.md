# Changelog

- 2026-10-09 — Docs: sourced real-world airline cost data (Adelaide fees, Airservices charges, fuel, crew) with confidence tiers for Economy v2; no code.
- 2026-10-09 — Docs: roadmap for real-world-scale economy v2, competitor airlines and a walkable airport (proposal for Bailey's sign-off; no code).
- 2026-10-09 — #756: enclose the tower viewpoint with a cab interior; preserve full flight-relative rain speed and elapsed movement.
- 2026-10-09 — #758: directional sunrise/sunset glow, cooler overhead sky and blue-hour evenings; reduced broad orange fog.

- 2026-10-09 — #743: the return climb from an outstation eases its departure offset away within the speed envelope (Saab peak CAS 320 -> 223 kt at Kingscote), not a fixed two minutes (bug hunt #13).
- 2026-10-09 — #742: aircraft turning round at a regional outstation (Kingscote) taxi to the mapped apron, park nose-in in their own slot and taxi back to the departure start, instead of sitting on the runway end (bug hunt #8).
- 2026-10-09 — #727: helicopter legs are drawn at true geographical scale and held over far destinations, so a Bell 412 booked to Kingscote reaches the real Kingscote (bug hunt #12).
- 2026-10-09 — Bug-hunt fix pass (#722-#726) closed out: findings 1/14/7 fixed, 2 not reproduced, 15 not a defect; integration run on main had zero runtime errors.

- 2026-10-09 — #725: bug-hunt finding 2 (Saab right window obstructions) not reproduced on clean main at five times of day; evidence and limits recorded, no code/asset change.

- 2026-10-09 — #726: bug-hunt finding 15 (ATR 42 T-tail) verified not a defect — tailplane rendered in Exterior; evidence recorded, no code/asset change.

- 2026-10-09 — #723: quitting from exterior/cockpit view no longer throws NullReferenceException in EndCockpit during teardown (destroyed camera controller was reached through `?.`).
- 2026-10-09 — #724: aircraft doorway hollows no longer rejected for shells at the generators' 4 mm minimum thickness (float32 rounding); Saab/ATR/737/Dash 8 doors open onto a doorway, not bare hull.
- 2026-10-09 — #729: the "has landed at <outstation>" toast names the outbound flight number, not the return (bug hunt #9); Adelaide arrivals keep the return number.
- 2026-10-09 — #733: a contract card armed by the first click now shows CONFIRM on its button instead of ACCEPT, signalling the deliberate second click (bug hunt #6).
- 2026-10-09 — #739: the Adelaide radar is hidden while watching Parafield, so no unlabelled Adelaide outline/RWY sits beside the Parafield card (bug hunt #11).
- 2026-10-09 — #744: Flight Manual now places the career ring, flights and radar bottom-left and the selected card on the right, and describes aqua/grey/amber destinations, not green (bug hunt #10).
- 2026-10-09 — #735: achievement/tier/contract cards are now modal; the HUD, hotkeys (Tab, H, T, C...) and world clicks behind them are inert; Enter, Esc or the button closes them (bug hunt #5).
- 2026-10-09 — #722: cockpit/passenger interior audio adds its AudioSource before the low-pass filter, ending the per-frame NullReference/Unity add-component spam (finding 1).
- 2026-10-09 — #720: first-flight guide "Away" wording follows the leg (outbound / turning round / flying home) instead of always saying "on its way to" the outstation (bug hunt #3).

- 2026-10-09 — #712: enclose Dash 8 folded wheels, fit curved nacelle doors with independent hinges and smooth the under-wing joins.

- 2026-10-09 — Mac diagnostic frames retain upright orientation; weather probes include the sky; queued remote requests are kept rather than replaced.

- 2026-10-09 — Failed diagnostic actions/state checks capture their visible context; remote agents use the Mac bridge before claiming runtime verification.

- 2026-10-09 — #706: isolate welcome weather and fix title shortcuts, setup codes, compact/recovery cards and Options readability/view targeting.
- 2026-10-09 — Add issue-specific gameplay diagnostics and private Mac execution for Linux agents, with real frames/state/errors and repeatable retesting.

- 2026-10-09 — Cloud shader cost cut: cheap one-octave sun-shadow probes, second probe skipped in deep shadow, fewer steps on short chords, lightning glow only during a flash (appearance and GPU timing unverified; run the agent gameplay weather profile on the Mac).
- 2026-10-09 — Weather delay (storm ground stop, weather-held helicopters) no longer costs reliability or breaks the on-time streak; the delay breakdown still shows it (headless-tested).
- 2026-10-09 — Agents automatically choose necessary focused gameplay checks and build once if needed; full journeys only when warranted.

- 2026-10-09 — Add roadside grass, varied coastal scrub, flush apron drain grates and tapered taxiway-edge wear; retain operational clearance (Unity unverified).

- 2026-10-09 — Add hidden agent gameplay runner: batched feature checks, private saves, real frames, build reuse and optional accelerated round trip.
- 2026-10-09 — Forecast wind now follows the weather (fog near-calm, rain breezy, storm strong and gusty, clear a little lighter) and eases between hours; live-observed wind unchanged (headless-tested; unverified in Unity).
- 2026-10-09 — Clouds gain sunlit thin edges, deeper storm self-shadowing, eased silhouettes and density-based fades (Unity unverified).

- 2026-10-09 — Jet contrails: high jets in the sky traffic (above 24,000 ft) leave a widening trail per engine, hidden under rain, fog and storm and thinner under overcast (unverified in Unity).
- 2026-10-09 — Rain is now relative to the observer: climbing, diving or running down the runway in a follow/cockpit view streams drops past at the right slant and length instead of falling straight down (unverified in Unity).
- 2026-10-09 — Replace Dash 8 overlapping roof shells with one hull-seated wing fairing; preserve all other mesh geometry (Unity unverified).

- 2026-10-09 — Selected-aircraft card: body text (route, live stats, details) drew at the top-left of the screen instead of inside the card; no device-pixel text inside scroll views (unverified in Unity).
- 2026-10-09 — "Your flights" tracker (six-step progress for each booked/moving flight, bottom-left) and per-view HUD layout (overview vs follow) with a Views options tab; L/N toggle per view (headless-checked; Unity unverified).
- 2026-10-09 — Add patchy ground, interrupted mowing, pavement joints/sealed cracks and varied apron repairs; preserve level operational surfaces (Unity unverified).

- 2026-10-09 — Fit all aircraft tails into their hulls, match rudder/elevator hinges and correct per-type proportions; refresh models/thumbnails (Unity unverified).

- 2026-10-09 — Sim weather (fog hours) now follows the airline clock, not the default epoch; fixes civil helicopters held for hours in off-clock fog (Unity unverified).

- 2026-10-09 — Saved live weather/wind now drive airport rules and visuals together; save v23 preserves holding poses across compatible reloads (Unity unverified).

- 2026-10-09 — Parked aircraft stay visible/selectable but leave active counts and automatic follow until two hours before departure; moving aircraft stay active.
- 2026-10-09 — Bug-analysis fixes: credits no longer dropped at 800x600, compact Contracts/Operations/HUD text fits, jet pay and cost scale with size, 12 missing demand entries, loss-making contract routes skipped, rename-box hotkeys, missed daily report, plurals (headless-checked; Unity unverified; see docs/testing/bug-analysis-2026-10-08).
- 2026-10-09 — Use Linear colour rendering; recalibrate far terrain to decoded satellite colours; existing quality budgets retained (Unity unverified).

- 2026-10-09 — Established arrivals fly a visible hold when their ETA is lost/postponed; map/follow use the actual pose and retain clearance handoffs (Unity unverified).

- 2026-10-09 — Anchor clouds to the watched area; retain far-clipped volume proxies; add high wisps, stratiform banks and drizzle/showers (Unity unverified).

- 2026-10-09 — Keep in-range fleet route models after climb-out; ordinary follow streams terrain; aircraft lookup survives camera filtering (Unity unverified).
- 2026-10-09 — Terminal doors and people: a sliding door where each stand walk starts, a boarding queue and gate agent at it, 18 landside walkers and airside staff, person cap 60→110 with distance-throttled posing (Unity unverified).

- 2026-10-08 — Persistent fog/storm clouds, lit tops, altitude-aware weather and lightning; restrained cockpit vibration (Unity unverified).
- 2026-10-08 — Dash 8 main gear now twin wheels side by side (were in tandem); approaching aircraft keep a landing-light glow inside 6 km instead of only a tiny lamp lens (unverified in Unity).
- 2026-10-08 — Mac notifications: register app, retry permission, verify packaged bridge and preserve Unity signature; clearer failure help (Mac unverified).

- 2026-10-08 — Operations board, selection card and map detail show the altitude the aircraft is actually drawn at on regional departures (was the unlagged route profile; unverified in Unity).
- 2026-10-08 — Regional departures climb at a steady ~2,400 ft/min (was up to ~5,200): the route climb starts when the aircraft reaches its start height, and rotation-to-exit uses one steady rate (headless-tested; unverified in Unity).

- 2026-10-08 — Roads: free street ends get a turning head or rounded cap instead of a flat cut; side roads get give-way teeth where they meet a more important road (unverified in Unity).
- 2026-10-08 — HUD stage 3: Map plan pane (profit card, status chip, career note), Fleet roster cards, Contracts cards and one-row chips, Career stat cards (Unity unverified).

- 2026-10-08 — Roads: OSM snapshot refreshed (+55 drivable roads incl. new service roads, 16,068 total) and road/car-park/precinct data regenerated; coverage checked against live OSM count (unverified in Unity).
- 2026-10-08 — HUD stage 2: flight-view HUD as captioned instruments with route/phase chips and progress; Operations rows with severity stripe, bold status and time (Unity unverified).

- 2026-10-08 — Hollow aircraft fixed: a dark inner skin behind each fuselage so windscreens and cabin windows show a dark interior instead of the sky (unverified in Unity).

- 2026-10-08 — Regional landings follow the route speed into the terminal area, settle to the type's approach/touchdown speed and brake to a stop (was a flat 90 kt for every type); new FlightSpeedEnvelope (min speed rises with bank, 250 kt CAS cap, climb/descent angle limits speed change). Headless-tested only.
- 2026-10-08 — Freighters no longer show passenger cabin windows (flight deck only); exterior aircraft glass is a dark near-opaque tint so windscreens/cabin windows are not see-through (unverified).
- 2026-10-08 — HUD chrome redesign (stage 1): floating status capsule and action group, wider rail with a clear selected state, milestone card with progress, richer palette (Unity appearance unverified).

- 2026-10-08 — Route Map: aircraft on the field collapse to quiet dots with one "N on field" count (full icons/labels only when zoomed in or picked), ending the label pile at Adelaide (Unity unverified).

- 2026-10-08 — Airborne turns bank as a coordinated turn (tan bank = V·yaw rate/g, max 25°) from real ground speed and heading rate, also on en-route legs (unverified in Unity).
- 2026-10-08 — HUD Tower button beside Overview/Radar/Menu enters the control-tower cab view (unverified in Unity).
- 2026-10-08 Inbound pitch eases into the approach attitude before the glideslope entry, removing the level-off-to-final step (presentation only)

- 2026-10-08 — Runway 23 approach lights now run sequenced flashers toward the threshold at night, as the real-scale field's approach lights were steady only (unverified).

- 2026-10-08 — Exit glide also turns with the aircraft, so the camera no longer drifts sideways when leaving a flight view mid-turn (unverified in Unity).

- 2026-10-08 Circuit pitch eases to the approach attitude so the go-around circuit-to-approach hand-over no longer steps (presentation only)
- 2026-10-08 — Leaving exterior/cockpit view: the exit glide now travels with the moving aircraft instead of chasing it from a fixed point (unverified in Unity).

- 2026-10-08 — Airport lighting: the real-scale Adelaide field now has its night aerodrome beacon (white/green flashes, lens, light and glow on the tower cab); it was only built on the old miniature (unverified).

- 2026-10-08 — Control-tower view: click the Adelaide tower to look out from its cab (360° look, zoom, Esc to leave); presentation only (unverified in Unity).
- 2026-10-08 — Route Map redesign: filled land (async baked), faint coast/borders, dark-rimmed dots, codes for reachable places, halo labels, one solid range ring; flight-view map cut to the essentials (Unity compile/look unverified).

- 2026-10-08 Smoother regional flight transitions: authored flare, rotation and cruise/approach pitch hand-over; flare lengthened to ~7 s (presentation only)
- 2026-10-08 — Smoother flight-view transitions: the glide travels with the moving aircraft, field of view follows the same glide, and the fuselage hides mid-glide (unverified in Unity).

- 2026-10-08 — Economy: flights cost more (120 base + 1.45/1.65 per km, was 70 + 1.12/1.28), pay and prices unchanged, so margins are thinner (balance unverified; ADR flight-cost-rebalance).

- 2026-10-08 — Switching to cockpit/window view no longer shows the wings floating without a fuselage: the airframe hides only once the camera glide arrives (unverified in Unity).
- 2026-10-08 — Moving map smoothness: textures bake on a worker thread (no open/zoom hitch), crisp anti-aliased coast window at every scale, mipmapped airfield, ring texture, cached route/text (Unity compile/look unverified).

- 2026-10-08 — Approach gear follows height (down ~2,000 ft AGL jets / 1,500 turboprops, lowered over ~22 s) instead of phase progress; sources in lighting README (unverified in Unity).

- 2026-10-08 — Camera drag no longer hits an invisible wall: free-pan limit widened from 3.8 km to 12 km around the airfield (unverified in Unity).
- 2026-10-08 — Jet routing factor: cruise time on jet legs rises linearly from 1.0 at 600 km to 1.08 at 2,500 km+ (routing/headwind); Perth 173→183 min (headless audit only).

- 2026-10-08 — Flight-time audit: ATR 42/Dash 8 planning cruise 510/620 km/h (was max cruise), A350/787-9 practical range 13,500 km, 787-10 11,200 km (headless audit only).

- 2026-10-08 — Bug pass: lamp flare no longer half-hidden in the airframe and caches its pivot (was a per-frame Find); distant-glow table prunes destroyed aircraft (unverified in Unity).

- 2026-10-08 — Moving map in cockpit/window/exterior views: centred own-ship, heading/track, route, airports, fleet, airfield or coast by scale; N toggles, +/− or scroll (unverified in Unity).

- 2026-10-08 — Light audit: all fleet types carry landing/taxi/nav/beacon/strobe lamps; Parafield trainers gained a cowl landing lamp, beacon flash and wingtip strobes (unverified in Unity).

- 2026-10-08 — Visible landing/taxi lights: soft camera-facing flare on each lit lamp so beams read from every play camera (Unity compile/appearance unverified).

- 2026-10-08 — #613: Australian flight terrain, denser real airport maps, mapped runway paint and economical high-altitude streaming (Unity unverified).

- 2026-10-08 — #595: unify terrain/road/pavement lighting and colour handling; reduce daytime washout and retain pavement detail (ADR coherent-world-lighting).

- 2026-10-08 — #585: free A320, vehicle and foliage derivatives; scanned surfaces, softer wear, facade fittings, reflections, conveyor motion and cabin fabric.
- 2026-10-08: Fit fleet exterior lamps to their airframes, carry taxi lamps with nose gear, separate white strobes from coloured nav sectors, distinguish Boeing flashes and extinguish landing beams in cruise. Validation: docs/testing/aircraft-lighting-2026-10-08/.

One line per merged change, newest first: what the player or contributor sees, plus the PR or ADR number.
Keep each entry to **one line (about 160 characters)**; the evidence belongs in the PR, an ADR or
`docs/testing/<topic>/README.md`. Mark behaviour not yet run in Unity as "(unverified)".
Older entries (about 1,000, through 2026-10-07) are in
[`docs/history/CHANGELOG-through-2026-10-07.md`](docs/history/CHANGELOG-through-2026-10-07.md).

## Unreleased

- Fix jagged star-shaped cabin windows where livery bands crossed the window row (A350, 787-9, others): paint is now cut by each pane's outline (`scripts/cut-livery-windows.py`); Dash 8 title table refreshed.

- Hide the folded airstair steps on the Saab, ATR 42 and Dash 8 until the door opens; they poked through a shut door's curved skin as a ladder (compiles; unverified in Unity).

- Fix low bug-hunt findings 18, 24, 25, 27-30: waiting-to-start prep status, "1 flight", ownership goal wording, fleet role line, roster label fit, Bell markings, rotor blur opt-out (compiles; unverified in Unity).

- Fix bug-hunt findings 16, 17, 19-23 and 26: contract bonus forecast, pinned booking time, recovery dispatch, tier gate, overview restore, check-aware contracts, Bell payload, offer paging (unverified in Unity).

- Refine fleet engine mouths with recessed liners and fitted control hinges; preserve aircraft geometry envelopes, animation pivots and saves (#716).

- Hold storm departures at their stands; taxi-released aircraft continue and arrivals already on extended final retain their landing timer (#670).

- Fix night final visibility: haze-aware aircraft light halos, fuselage-safe distant glows and readable close night position lights (#668).

- Improve all 15 aircraft bodies with continuous contours, rounded noses and fitted glazing/doors; retain type envelopes and A320 source detail (#651).
- Add opt-in native Mac notifications for important background airline events, grouped bursts, permission/test controls and click-to-return (#645).
- Restyle toasts with status labels, wrapped text, repeat badges, lifetime bars and eased motion; keep stacks clear of panels and screen edges (#642).
- Redesign the Adelaide T1 opening, restore AIRSIDE with a vector lockup/text fallback, and add a skippable centre-opening Continue reveal (#626).

- 2026-10-08 — #614: layered twin-engine starts/cores, cabin and spatial airport audio, regional landing cues and 68 v02 clips (Unity audio unverified).

- Render at the display's native size: a stale saved window size (1600x900 maximised on a Retina screen) made the game soft and letterboxed; the player now resets it at startup.
- Editor review: preserve approachNN gear poses for inspection; native compile/test attempt stalled (#608).
- Preserve flap/gear rig rest poses across follow-target refreshes and use the actual regional journey phase in cruise (#586).
- **ATR 42 hold door fixed.** The forward-left baggage door no longer sits mirrored a door-width ahead of the cockpit: the half turn is baked into its mesh, not its transform (unverified in Unity visually).
- Keep one previous airline save and recover unreadable/missing primary JSON through Continue, with a clear recovery warning (#599).
- Clearer opening, shorter optional entrance, grouped Options and direct return to title; expose cockpit motion and explain settings (#601).
- Draw HUD workspace text and buttons at real device pixels (fonts and rounded button art scaled, not stretched) so Operations/Fleet are crisp on Retina (Unity tests pass; visual check pending).

- 2026-10-08: Runway/terminal/gate templates for all 19 Australian airports (real OSM gate numbers at 8) plus a runway and gate planner that sends airlines to their own terminal (unverified in Unity).

- Add independent Parafield: four mapped runways, taxiways, hangars and light trainer traffic; watch from Operations (#593; packaged review pending).

- Remove artificial ocean reflection rings by keeping far/outer water meshes planar; land overlap unchanged (native visual confirmation pending).
- Taper fleet climb/descent rates, level cruise, constrain CAS/Mach and fix stale camera V/S, inbound altitude and regional takeoff; allow realistic jet leg time (ADR 2026-10-08).

- Fix parked/bay taxi clearance, rotor queues, sky ownership, inspector/manual buttons and repeated query allocations; add aircraft/save coverage (#576; Unity unverified).
- Lighting beam-aim checks: each family's landing/taxi beam must land on the ground ahead inside lamp range, with an offline beam diagram (unverified in Unity).
- **Aircraft lighting by type.** Turboprops, regional jets, narrowbodies, widebodies and the Bell 412 get their own beam widths, strobe pattern, tail strobe, beacon rate and nose-lamp takeoff light (ADR 2026-10-07; unverified in Unity).
- #577: Operations shows all owned aircraft across bases, live next events and direct Fleet controls; Adelaide movements stay available (unverified in Unity).
- Fleet door seams, latches, thresholds, Bell sliding rails and service hatches follow fitted aircraft skins and existing door motion (#578; unverified in Unity).

- **Smoother model shading.** Runtime model normals are weighted by corner angle so curved panels stop rippling (unverified in Unity; ADR 2026-10-07-angle-weighted-normals).
- **Fix ground-separation CI failure.** Landing gate also keeps the end of the exit clear for 28 s, so a taxi-out no longer passes within 25 m of a stopped arrival (unverified in Unity).

- #552: repair native fixture contracts, validate blank saves strictly and shift camera glide anchors with flight origins (unverified in Unity; owner-authorised merge).

- **Cleaner image.** Full-res SSAO again, near clip scales with distance, ground mip bias 0, 16x aniso against flicker/pixelation (unverified in Unity; ADR 2026-10-07-clean-image-pass).
- Fix landing gear standing up on selected jets in flight: gear rest pose is captured once, not re-read when the view's parts are rebuilt (unverified in Unity).
- Arrivals on final never wait on ground traffic: runway-only landing clearance, 4 min decision point then go-around, inbounds metered in the circuit (unverified in Unity).
- **Wide overview polish (ADR 0251 amendment).** No pale square round Adelaide, smooth far colours, the ring follows the camera out and fades into the sky; soak logs hitches.
- **Fleet/flight clarity (#570).** Available aircraft, airport selection, Melbourne flight views and reviewed bookings/cancellations; compare expected profit.
- **Faster agent loop.** Cloud-session bootstrap (.NET 8), `scripts/test-quick.py --changed`, `scripts/new-meta.py`, docs-only PRs skip heavy CI, `docs/ai/RECIPES.md`, `.cursorignore`.
- **Connected player-flow study (#567).** Interactive local airline journeys, commitment/refusal recovery and a fresh dispatch layout; no Unity runtime changes.

- **Multi-tool workflow.** Pointer files only, per-area `AGENTS.md`, task/PR templates, date-named ADRs and generated indexes (`docs/ai/WORKFLOW.md`).
- **Player-flow contract.** Consolidates 24 player tasks, decisions and recovery paths; visual interface proposals must be checked against it. Documentation only.

- **Grounded visual direction.** Three edits of actual game references propose modest world polish and a fresh dispatch interface; supersede the photographic vision.

- **Presentation map.** `docs/architecture/PRESENTATION_MAP.md`, generated by `scripts/map-presentation.py`, indexes what each of the 39 `AirsidePrototype` partial files owns.
- **Finished-product vision candidates.** Five generated planning views cover gameplay, HUD, aircraft, fleet/routes and turnaround; build notes record realism limits.

- **Headless checks now fail where Unity's compile fails (ADR 0252).** No implicit usings and a compile-only build against Unity's NUnit 3.5 in CI (catches `Is.AnyOf`, missing `using System;`).

- **Wide overview (ADR 0251, unverified).** Zoom out to 450 km and pan across South Australia: the overview streams fine and coarse land-cover terrain from the camera past 60 km.

- **South Australia land cover (ADR 0250, unverified).** Streamed flight terrain is coloured by real state-wide land cover (ESA WorldCover, 123 KB, no imagery) in the Adelaide-ring palette.

- **Repo tidy (ADR 0249).** `GAME.md` is now ~170 lines of current state and `CHANGELOG.md` one line per change; full originals, an ADR index and a branch inventory are in `docs/history/` and `docs/decisions/README.md`.

- **Smooth hand-over from the satellite image to the far land.** Zoomed out, the photo used to
  stop dead at 30 km and the land beyond started in brighter, yellower colours. The photo now
  fades into the land-cover colours from 24 km, and those colours are measured from the photo
  itself, so there is no ring.

- **No more white "cloud" when zoomed far out.** The land beyond the 30 km satellite image was
  baked toward a pale haze colour so strongly that from above it looked like a white sheet with
  blue lakes. The haze now starts at the satellite edge and stays a light tint, so the far
  farmland and hills keep their colour.

- **The aircraft panel only opens when you pick an aircraft, and fits its content.** It used to
  pop up for the most urgent aircraft (any idle one), so it was always on screen and Close
  just brought it back. It was also always 700 px tall; a parked aircraft now gets a short panel.

- **Saved airlines load again.** Unity's JSON saver writes an empty maintenance job or runway
  wake record as an all-blank object, and loading rejected it ("Invalid maintenance job for
  VH-PAX"). A job with no hangar, or a wake with no aircraft type, now means none. Existing
  saves load unchanged; no save-format change.

- **Sharper far satellite image (ADR 0248, unverified).** Far ring uses a 4096 px image from a 20 m Sentinel-2 source (was 2048 px / 40 m); v01 kept as fallback.

- **Sharper Adelaide Hills (ADR 0247, unverified).** Far terrain ring meshed from the full 125 m DEM (was every second sample).

- **Render cost trim (ADR 0246, unverified).** Half-res SSAO, 2x MSAA + SMAA above 2.5 M px, High shadows 3 cascades / 110 m.

- **Unity compiles again.** Three tests used `Math` without `using System;` or NUnit's
  `Is.AnyOf`, which the headless harness accepts but Unity's NUnit does not.

- Implement the refined shared interface and real maintenance journey (ADR 0245):
  normal startup without passengers/loading, traffic-aware taxi, apron shutdown,
  continuous tug positioning into the shed, repair and return to a free stand.
  Wear resets after repair; save v22 resumes the actual phase. Older timed checks
  retain their prior completion semantics. Each active job reserves its shed.

- Simplify overview navigation/status, make radar optional, add a right aircraft
  inspector with fixed actions and scrolling details, and increase Fleet/Operations
  row spacing. Keep command feedback visible below management sheets. Offline
  shared-painter previews cover 1440×900 and 1280×720; native Unity verification pending.

- Refine the maintenance movement and whole-interface proposal with a concrete
  interactive design study, seven retained previews and implementation/acceptance
  packets (ADR 0244). Design only; no runtime game changes.

- Remove synchronous GPU readback, PNG encoding and disk writes from packaged
  review screenshots, a plausible source of the recorded 172/196-second frames.
  Use async readback and background writes; capture failures/timeouts fail QA.
  Historic cause remains unproven; no Unity or full-journey reproduction run.

- Keep stationary aircraft blocking ground clearances permanently, reserve full-airframe
  runway crossings and intersecting-strip occupancy, apply/save follower-specific MTOW
  wake minima, route terminal departures around Adelaide ERSA restrictions with eastbound
  Code C pushbacks, and use compatible widebody arrival exits. Cap shared service vehicles
  at the airport's apron/terminal/aircraft-zone speeds. Arrival estimates share the tower
  guards. Save v21 migrates older deadlines conservatively (ADR 0243); Unity unverified.

- Aircraft views (cockpit, window, exterior) feel better on a Mac and everywhere: the trackpad now taps and
  rumbles with touchdown, gear thumps, runway joints and turbulence (system haptics, no plugin; the Vibration
  toggle governs it and the shake); either mouse button looks round; drag and scroll spikes are bounded and
  drag follows the zoom; entering, switching and leaving a seat glide instead of cutting; the dock hint,
  a once-per-run toast and F1 help say how to look, glance, zoom and recentre (ADR 0242). Headless suite
  passes; Unity compile, trackpad feel and visuals are not verified.

- One fleet across every base (ADR 0239). The Fleet workspace now lists every aircraft at
  Adelaide, Melbourne, Sydney, Brisbane and Perth, grouped by base with SHOW and SORT, and a
  bases strip that filters the roster and picks where BUY delivers. Each aircraft has a profile
  with logbook, check, sell value and its actions: send an outstation aircraft on a route in one
  click, TO ADELAIDE (a visible ferry that earns nothing), SELL (two clicks), and the camera views.
  Selecting a row no longer closes the sheet; the Network screen is gone. Outstation aircraft appear
  on the map and in Stats, the return briefing and My Flights. Selling an aircraft now refuses a
  booked flight; a sold outstation mark is not reissued; a second Saab opens its own planner. Save
  v20 adds the ferry flag and an outstation logbook. Code only: nothing compiled or run (Bailey's
  instruction), so build, headless tests and visuals are unverified.
- Refine the maintenance movement and whole-interface proposal with a concrete
  interactive design study, seven retained previews and implementation/acceptance
  packets (ADR 0242). Design only; no runtime game changes.

- Distinguish jet family windshield shells, shape pilot/passenger seats and regional
  yokes, add family bin/PSU fittings, and enable an analog Bell 412EP cockpit with
  fitted front frames and retained rotor visibility. Correct Bell tail geometry
  to two opposed blades in the shaft plane (ADR 0241). Unity-free
  verification only; native appearance and switching remain unverified.

- Add family-specific cockpit controls, panels, overhead fittings and pilot seats
  across all thirteen fixed-wing types; fit every passenger camera to individual
  glazing and bound cabin sections to real window belts (ADR 0240). Unity-free
  checks only; native visuals remain unverified.

- Stop stars, sun/moon discs, the stratus deck, the horizon band and the rain volume sliding while the
  camera pans, orbits or zooms. They were re-centred in `Update` but the camera moves in
  `LateUpdate`, so they trailed one frame behind; a new `CameraShellAnchor` re-applies their
  offsets after the camera has moved. EditMode tests added; not yet verified in a Unity run
  or visually.

- Fit passenger windows independently from seat rows, add curved cabin walls/
  ceiling, recessed trim, shaped bins/PSUs, deeper inward views and nearby seat
  fittings. Align ATR42/A320/B789 cameras to individual kit panes; distinguish
  cabin materials/cloth and ceiling light, with decorative 787 dimmer fittings
  (ADR 0239). Code/headless checks only; native visuals unverified by instruction.

- Preserve the historical #534 visual-code audit and six evidence packets: 16
  concrete source/data findings already implemented in merged #535/#536, two
  optional lighting gaps, corrected claims and explicit implementation ownership.
  Documentation only; no new native checks or game behaviour changes.

- Smooth the watched takeoff-to-climb-out handoff instead of jumping 174–218 m
  onto the enroute profile; match vertical rates through rotation and climb-out.
  Add shared flight status, location, height, true heading and journey progress /
  arrival-area estimates to every aircraft view (ADR 0238). Code-only checks;
  Unity tests/builds/player execution excluded by Bailey, native visuals unverified.

- Complete the nine remaining concrete #534 code findings: noncollapsed kit UVs
  and tangent frames, fixed geographic foam and roof texture coordinates,
  port-red/starboard-green lights, altitude-adjusted wipers, downwind weather flow,
  and vertex-coloured stars with background depth/additive fade. No Unity tests,
  builds or player execution by Bailey's instruction; native visuals unverified.

- First visual-audit code batch (#534 findings): show first scheduled flights on
  the map, clear stale inspection on explicit planning/selection and project field
  headings; rotate service tyres at their own axles and place boarding stairs/walks
  on pavement; size jet fan discs to blade radii and trigger touchdown effects at
  each aircraft type's contact threshold. Code/headless validation only; Unity,
  builds and player execution explicitly excluded by Bailey.

- Put flight status and camera actions on the full map in a dedicated inspector;
  show all operators by default, add a South Australia shortcut and tidy labels,
  routes and opaque map chrome. Refresh shared icons/buttons across the game;
  return the mini map to a polished airport scope; keep the new scenery credits
  on a separate manual page so all attribution remains visible (ADR 0237).

- **Mac builds recover from Unity's script-build hang.** `scripts/build-mac.sh` watches the
  log; if it stops growing for 60 seconds (`AIRSIDE_BUILD_STALL_SECONDS`) right after Unity
  starts `bee_backend`, it kills that run, clears the `Library/Bee` build graphs and retries
  once, then fails clearly instead of hanging forever.
