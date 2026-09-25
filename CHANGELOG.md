## Unreleased

- **Aircraft glazing stays translucent in play.** The per-frame cabin/flight-deck glow
  now keeps each pane's authored tint and alpha instead of replacing it with opaque
  white. A day/night Unity regression covers the property block; the packaged Saab
  close-up shows the openings again, though final visual polish is still pending.

- **Route decisions now show clearer immediate trade-offs (ADR 0119).** The Route Map refunds an existing booking when checking affordability, blocks departures during an aircraft check, and previews return and net with current reliability and matching-contract pay. It flags due checks and the reliability cost of cancelling a matching contract booking. The separate-save packaged soak now records frame times and memory, and can follow and zoom in on a registered aircraft for visual review. The simulation and save format are unchanged.

- **Aircraft glazing now has real depth (ADR 0118).** All 13 aircraft have apertures
  under single-layer translucent cabin and flight-deck panes, recessed dark interiors and
  simple pilots. Side paint follows the operator accent instead of a universal blue;
  Virgin Australia's overly pink accent is corrected to red; main wings and engines
  return to neutral paint. Packaged visual approval pending.

- **Fallback sky traffic creates less per-frame garbage.** Rendering reuses its
  temporary flight and ID collections, caches repeating corridor callsigns, and
  skips redundant aircraft activation changes. The snapshot APIs and flight
  paths are unchanged.

- **Baggage vehicles no longer clip through the terminal wall.** The authored
  baggage-hall spur now passes through a real 9 m × 3.4 m opening in the
  airside facade. The roof, other walls and existing ground-service timing stay
  unchanged; a mesh regression checks the opening and its lintel (ADR 0115).

- **Aircraft windows have a fitted glazing pass (ADR 0117).** All 13 fleet models now carry
  separate raised surrounds, dark recessed gaskets and restrained upper-edge reflections
  around the existing cabin and flight-deck panes. Hangar thumbnails and packaged runtime
  art have been regenerated from the same geometry.

- **Clouds no longer blanket the airport and takeoff rotation is stable.** Adelaide weather is
  back to the 16 atlas clusters approved by ADR 0075, with a higher 650–950 m deck, smaller cards
  and restrained translucency instead of 24 low cards expanding past a kilometre wide. Aircraft
  look-ahead now supplies horizontal heading only; the authored phase curve remains the sole
  pitch source, removing the double-pitch twitch around rotation and from the follow camera.

- **Unity art imports are stable on a clean checkout.** The 70 Quaternius character materials
  externalised from the nine passenger/ramp-worker FBXs now ship with fixed Unity GUIDs, rather
  than being recreated differently and left untracked on each Mac. The catering truck's two
  packaged glTF files also have committed metadata. A new read-only asset audit checks every
  asset/directory metadata pair, rejects orphan or duplicate GUIDs, and confirms every one of
  the 336 runtime art files is a byte-identical `StreamingAssets` mirror. The accompanying
  50-check whole-game pass is green at 1047/1047 Unity EditMode tests.

- **Air traffic no longer drives through runway movements or lands onto a full apron.**
  Ground-control prediction now includes the ground roll during takeoff and landing, checks
  the complete vacate-to-stand path, and reserves a fitting stand for each AI arrival before
  final. If the apron is full, AI traffic remains airborne until a stand frees instead of
  blocking the runway exit through the overnight curfew. Player arrivals retain their stand
  choice window and automatic fallback. Arrival ETAs now include the same ground-route hold
  used by the tower, and saved games safely round-trip optional arrival reservations.

- **Current fleet and aircraft presentation checks are accurate again.** The Adelaide layout
  test derives its parked Q400 count from the four-aircraft fleet, terminal compatibility
  distinguishes code C from code E gates, and the Saab dispatch check recognises its renamed
  integrated airstair door and verifies the generated treads.

- **The catering truck now exists (ADR 0116).** It was the one turnaround vehicle built with no
  art path, so it fell back to a flat-shaded box while the fuel truck, baggage tug and apron bus
  all loaded authored kits — and there was no catering model on disk at all. VEH-004 gives it
  the scissor-lift hi-loader silhouette: chassis and cab, crossed lift legs, a raised box body
  and a bridge platform at cabin-door height.

- **There are people on the apron now (ADR 0116).** Passengers were drawn for stairs boarding
  only, which is right as far as it goes — you cannot see anyone inside an aerobridge — but
  every Terminal 1 jet gate uses a bridge, so at a terminal gate nobody was visible at all.
  ADR 0114 had also imported two hi-vis ramp workers and never placed them. They now work
  whichever vehicle is active: at the fuel panel and under the wing, steadying the hi-loader
  and at the galley door, at the hold and on the baggage cart, and one marshaller clear of the
  door during boarding.

- **Fuel, catering and baggage vehicles now drive to the aircraft (ADR 0115).** They used to
  appear beside the stand when their prep stage began and vanish when it ended. Each now
  leaves a depot 90 seconds ahead of its stage, drives the real Adelaide airside frontage road
  — 1,122 m along the Terminal 1 face, imported from OpenStreetMap — works while the stage
  runs, and drives home. The baggage vehicle works out of the hall beneath the terminal, so it
  drives out through an undercroft on every trip.

  The undercroft is **authored, not imported**, and the generated file says so: OSM has no
  tunnel, covered or layer tag at Adelaide, and the real frontage runs about 91 m off the
  airside wall — 44 m clear even of a fully extended aerobridge — so nothing in the real data
  passes under the building.

- **Enable Unity Animation module for boarding walk clips.** ADR 0114 samples
  `AnimationClip`s; the built-in package was missing from `Packages/manifest.json`,
  so Mac batch builds failed with CS1069.

- **People board and deplane; turboprops use their own airstairs; stair trucks for jets on
  stands without a bridge (ADR 0114).**
  - Nine CC0 Quaternius characters walk between the terminal and the aircraft.
  - Arriving passengers get off after the door opens, and boarding finishes before the
    door shuts.
  - The Saab 340, Dash 8 and ATR forward doors fold down into airstairs.
  - Jets on stands 20R, 22R and 27–29 get a stair truck.
  - Widebody front doors now open and close.

- **Terminal 1 aerobridges (ADR 0113).**
  - 17 gates get a moving bridge that drives out to the parked jet's front-left door
    once the beacon is off, and pulls back before the push.
  - The door opens and closes with the bridge.
  - Remote stands (20R, 22R) and the western stands (27–29), which lie off the
    terminal footprint, keep stairs.
  - Pushback tugs are planned in `docs/architecture/PUSHBACK_TUGS_PLAN.md`.

- **Liveries on every type; titles no longer show through the wings (ADR 0112).**
  - E190, A220, A321neo, A350 and 787s now wear the fitted operator sash, so no type
    uses the barcode decal.
  - Engines are painted grey and white instead of blue.
  - Airline titles are generated from each mesh: above the windows, tangent to the skin,
    starting behind the flight deck and clear of the wing root.
  - Titles are drawn with a new lit, depth-tested `Airside/FuselagePaint` shader that is
    always included in builds.

- **Realistic Adelaide traffic: 38 AI aircraft, about 110 airline departures a day
  (ADR 0111).**
  - Qantas has 9 aircraft, Virgin 7, Jetstar 5, Rex 6 and QantasLink 4.
  - Aircraft with no free stand start away and fly in; overnight arrivals come in over
    06:00–08:30.
  - Most evening domestic frames night-stop, so 05:00–06:59 is the big dawn wave.
  - The afternoon is quieter but no longer empty.
  - Saabs use the walk-outs.
  - Player-facing "Airfield" wording now says "Airport", for example "Airport map" and
    "N at the airport".

- **Operations realism: 05:00–23:00 day, banked departures, gates sized to aircraft
  (ADR 0110).**
  - Commercial AI now flies from 05:00, and the last flight is 23:00; RFDS and the
    player are still exempt.
  - Departures cluster on shared published times (for example, several at 06:00).
  - Aircraft that night-stop leave in a 05:00–06:30 first wave.
  - A parked aircraft stays on its stand until its own departure: no off-season Cathay
    vanishing from GATE-18, and no timetable-only flights appearing in the sky.
  - Gates carry an ICAO code letter, so widebodies only use code E gates.
  - The board shows Boarding, Final call and Gate closed.

- **The headless test gate cannot silently break again.** `scripts/test-domain.sh` now checks
  the `Harness.csproj` exclude list before building and names any EditMode test that imports
  UnityEngine but is not listed, instead of letting one `CS0246` replace every result. That
  break has happened twice — `MapLabelLayout`/`GroundSeparation`, then `AirsideFramePacingTests`
  — and each time it hid the whole suite until someone noticed.


- **737 fitted livery + titles no longer show through wings (ADR 0109).** 737-8 and
  737-800 get the same skin-conforming operator sash as the A320; the barcode traffic
  decal is off for both. Fuselage wordmarks use a depth-tested cutout material so they
  no longer read through the wing from follow camera.

- **Full-res SSAO again (ADR 0101).** Half-resolution ambient occlusion made the field look
  flat on Retina. Mac still paces to 60 fps focused / ~30 unfocused, skips day landing-lamp
  shadows, and keeps the light/tint CPU skips — those do not soften the image.

- **Beginner aircraft shape pass (ADR 0108).** Corrected the ATR 42's reversed
  nose-profile station, rounded Dash 8 and Saab radomes, and replaced the Saab's
  square wing-root blocks with curved fairings. Updated runtime kits and Hangar
  thumbnails without changing flight operations or saves.

- **Starter turboprop livery polish (ADR 0107).** ATR 42, Dash 8 Q400 and Saab
  340B now carry fitted, operator-coloured fuselage bands; the repeating
  traffic decal no longer barcodes those aircraft. Runtime models and Hangar
  thumbnails were refreshed without changing simulation or saves.

- **AIR-015 A330-900 appearance pass (ADR 0106).** Replaced the scaled-A350
  pointed nose and flight deck with a rounder fuselage and four fitted panes;
  added a skin-conforming operator-coloured ribbon, removed the repeating
  fuselage decal and lowered the protruding engine pylons. The Hangar thumbnail
  follows the authored model. No simulation or save change.

- **AIR-011 A320 appearance pass (ADR 0105).** Replaced its scaled 737 fuselage,
  flight deck, cabin windows, wing and split tips with a rounder A320 silhouette,
  single sharklets and a skin-conforming operator-coloured ribbon; the repeating
  fuselage decal no longer produces dark barcode bands on this type. Its Hangar
  thumbnail now matches the model. Flight state,
  controls, saves and fallback are unchanged.

- **Player can choose a stand after landing (ADR 0103).** Auto-stand no longer fires on the
  landing tick. The selection card and Operations detail list assignable stands (BEST first);
  after 90 s the tower still parks for you. Toast and first-flight guide match the real choice.

- **Arrivals / Departures boards keep recent Landed and Departed rows (ADR 0103).** Six-hour
  history from frozen fleet events; departure TIME no longer shows an airborne ETA as "est";
  taxi-in shows touchdown plus stand ETA; EVENT HISTORY shows five airport lines.

- **Desktop HUD hierarchy pass (ADR 0104).** Operations now leads with one Today's Priority,
  five immediate apron movements, an optional full movement history and a selected-aircraft
  turnaround timeline; Route Map is a
  quieter planning desk with one live destination dossier and plan action; Fleet combines a
  compact roster, selected operational detail and three gated market choices; Career centres
  the real four-stage base roadmap, one next milestone and recent achievements. Runway Ink
  remains translucent over the airport. Workspace toast feedback sits beside the desk, while
  tiny windows yield to the objective. Simulation, commands, saves and real data are unchanged.
  The first-flight guide shares the priority treatment; Map starts with rival routes hidden
  (one-click reveal), and Fleet starts with other operators folded away (one-click reveal).
  Career's secondary operation and local activity summary is quieter so the real roadmap
  and next milestone stay visually primary.

- **Daily Service Pattern (ADR 0102).** Each campaign chapter now has a local-day "fly this
  today" target on the objective card (`TODAY · Kingscote 0/2`, and so on). Completing it
  pays a once-per-Adelaide-day bonus via existing settlement keys; incomplete days just miss
  the bonus. No new save schema.

- **macOS performance pass (ADR 0101).** Frames are paced to the game, not the panel: 60 fps
  on ProMotion/120 Hz+ displays (Options → Frame rate · Display max to lift it) and ~30 fps
  while the window is in the background. SSAO renders at half resolution, landing lamps cast
  shadows only at night, static light tints stop rewriting every frame, and the per-aircraft
  light pass no longer looks components up by type and name every frame.

- **Buy aircraft → plan first flight.** Purchasing no longer stops at a toast. If the new
  airframe parks immediately, Fleet opens the Route Map with a career-suggested destination
  (active contract, then chapter target, then Kingscote) and tells you how long fuelling and
  boarding need before pushback. Delivery inbound says to wait until it parks. Objective card
  matches: "wait for VH-xxx to park, then schedule".

- **Busier Adelaide apron and ADL-shaped departure banks (ADR 0100).** Opening used to
  put ~16 aircraft Inbound (invisible) and leave ~5 on stands. Now a short ~7-aircraft
  arrival bank keeps short final alive while most metal stays parked; Rex gains two Saabs
  on the spare walk-outs. Opening pushbacks cluster with intentional same-minute doubles
  like a real ADL morning peak; later AI bookings snap onto 5-minute bank marks. Still not
  live flight times — simulation density only (ADR 0071 / 0086).

- **Operations "on field" matches visible metal.** The day caption counted every Inbound as
  on field even though those aircraft are off the map until short final
  (`FleetVisual.Hidden`). Opening traffic seeds a bank of them, so the strip could read
  "17 on field" over an apron that looked empty. The count (and each board row's OnField
  flag) now follows `FleetVisual.Visible` — same rule as drawing.

- **Live Adelaide weather and complete sky (ADR 0099).** The fixed YPAD forecast now drives
  presentation-only cloud cover, rain strength, fog visibility, wet surfaces, puddles, tyre
  spray and wind-reactive visuals, with a 15-minute poll, two-hour stale limit, Options toggle,
  visible Open-Meteo attribution and deterministic offline fallback. The real Adelaide scene
  now actually builds its astronomical sun, moon and a denser one-draw-call star field; stars
  follow the overview camera and fade behind daylight and cloud. Operational weather, runway
  selection, clearances, saves and replay remain deterministic.

- **Operations board honesty.** Departures no longer print destination arrival as "est" under
  a Departed row (the SIA488 07:46 / est 13:55 lie), and COMING UP never advertises an already-
  departed player aircraft. Enroute-to-destination progress bars stay off the departures FIDS.
- **The E190 and A220-300 are real aircraft now, not scaled 737s (ADR 0098).** Both were built
  by taking the 737-8 mesh and scaling it on three axes, which reproduces a bounding box and
  nothing else. Each now has its own generator lofted from its own dimensions: the E-Jet's
  four-abreast tube, aft wing, deep root fairing, small CF34-class engines and canted winglet
  fences; the A220's five-abreast tube, long pointed drooped nose, high-aspect-ratio wing with
  **raked tips and no vertical fence**, and oversized geared-fan nacelles. Envelopes, nose
  datum and tyre radii are unchanged, so no C# or save change.

- **Aircraft skin finally has surface detail (ADR 0097).** The aircraft glTFs carry no UVs, so
  the loader generates them — and the generic unwrap normalised each part's own bounding box to
  0..1, making texel density vary by over a hundred times across one airframe while unwrapping
  the fuselage straight down its own length. Aircraft kits now unwrap cylindrically in metres,
  so one metre of skin is one metre of UV everywhere, and a new 1024² `tx_aircraft_skin_*_v02`
  carries real frame (0.508 m), stringer, rivet and lap-joint spacing. The v01 it replaces was
  256² with a near-flat basecolor and a completely constant mask.

- **The 737-8 reads as a 737 (ADR 0097).** A blunt drooped radome in place of a near-conical
  nose, a flight deck that reads as one wraparound band instead of two dark specks, a
  flat-bottomed lower cowl, and a 4 cm fuselage waist removed. Four other narrowbodies
  (A320, 737-800, E190, A220-300) are axis-scaled copies of this mesh, so all five improve.

- **Aircraft wheels now turn while taxiing (ADR 0096).** The tyre roll read the circuit's
  phase speed, which is non-zero only during the takeoff roll and the landing rollout, so
  aircraft crossed the whole Adelaide taxi network on stationary wheels. Tyres now roll at
  the same authored ground-leg pose speed that already moved the aircraft, stop when the
  aircraft stops in a queue, and counter-rotate on the tail-first pushback.

- **Aircraft no longer roll on livery-painted nose wheels (ADR 0096).** The fuselage livery
  filter accepted any part whose name contained "nose", which swept up the nose gear's own
  tyres, wheels, rims, oleo, scissors and doors. All 23 shipped aircraft models were
  affected, between 1 and 12 parts each; landing gear now keeps its own rubber and metal.

- **Propellers stay visible at power (ADR 0096).** Individual blades switch off once the
  blur disc takes over, so at takeoff power the disc is the whole propeller — and at its
  previous 0.11 peak alpha it was close to invisible. Peak disc opacity is now 0.30 for
  propellers and 0.26 for turbofan intakes, still translucent.

- **`scripts/test-domain.sh` builds again.** `MapLabelLayoutTests` and
  `GroundSeparationTests` arrived without their harness entries, so the headless check had
  been failing to compile. `MapLabelLayout` is UnityEngine-free and is now compiled and
  covered; `GroundSeparationTests` needs `AirsideFlightPath` and is deferred to the Unity run.

- **Busy route maps keep aircraft labels readable.** Nearby aircraft labels now fan into
  non-overlapping in-map slots; when a local cluster is truly full, lower-priority text is
  omitted while the aircraft icons remain visible.

- **High quality no longer over-allocates anti-aliasing at Retina/4K sizes.** High retains its
  lighting, shadows and scene detail, but above five million display pixels uses 2× MSAA alongside
  its existing high-quality SMAA instead of multiplying every HDR/depth target by four.

- **Hidden fleet audio no longer floods the player log.** Away aircraft still retain their
  cached visual model, but the engine voice only starts once its root and AudioSource are active.
  This removes the per-frame `Can not play a disabled audio source` error and keeps real runtime
  faults visible in a packaged-player log.

- **Gate lead-ins release at Holding Short (ADR 0095).** A departure still holds its
  physical gate through taxi-out, but no longer blocks another gate movement after it has
  cleared the lead-in and joined the runway queue.

- **Base growth is now part of the campaign.** Eyre Peninsula requires the Expanded Regional base, Interstate requires Jet Gate, and Going Global requires the International base. Campaign progress derives from the existing persisted PlayerBaseLevel, so no save migration is needed.

- **Base upgrades explain their payoff.** Career now names current maintenance capability, ground-service speed and stand/gate access, and previews the next base upgrade as concrete operational benefits before the player spends the money.

- **Visible turnaround GSE.** The existing Adelaide service equipment now follows the player’s real active turnaround stage: fuel truck, catering truck, baggage train, then boarding equipment. With no player turnaround active, the previous ambient terminal servicing loop resumes.

- **Turnaround v2.** Player departures now run Fuel → Catering → Baggage → Boarding. Base growth speeds the same operational chain (Starter 100%, Expanded Regional 90%, Jet Gate 80%, International 70%), and every Fleet/Operations prep surface shows baggage and the same base-aware timing.

- **Career shows physical base access.** The base summary names the actual Adelaide positions/access currently leased (50D, regional apron, gates 27/29, pier 28 as applicable).
- **Player Base v2 — operational maintenance.** Routine checks are now facility-dependent: Starter outsources them (+40% cost, +50% time), Expanded Regional handles turboprops locally, Jet Gate adds narrowbody jets, and International adds widebodies. Fleet shows the exact service mode/cost/time.
- **Player Base v2 — allocated Adelaide stands.** The player's base now maps to real parking access:
  50D starter home, 50G/10A plus shared regional apron at Expanded Regional, gates 27/29 for jet
  operations, and pier 28 at International. Player jets cannot silently use unrelated terminal gates,
  while AI fallback allocation skips the player's dedicated positions.

- **Player-base save hardening.** Pre-v12 saves infer enough Adelaide base capacity for their full
  existing fleet (including 4–6-aircraft careers) as well as jet/widebody capability. v12 saves
  reject an unknown/corrupt base level instead of silently resuming with inferred state.

- **Player Adelaide Base v1 (ADR 0091, save v12).** A persisted operating footprint now gates fleet capacity and jet/widebody acquisition. Career can spend normal funds to expand it, Fleet mirrors the same locks, older saves infer enough capacity for aircraft already owned, and a livery-coloured leased operations compound grows visibly at Adelaide.

- **Career-aware Route Map.** Inspecting a destination now explains when an authored contract
  there would advance the current campaign chapter, using campaign-owned rules rather than
  presentation guesses. No save, economy, route timing or aircraft behaviour changes.
- **Adelaide base capability roadmap (ADR 0090).** The Career overview now translates existing
  operating tiers into concrete base growth — regional starter, expanded regional operation,
  jet-gate operation and international handling — and tells the player what the next tier unlocks.
  The roadmap is derived from existing career state and adds no new save data or currency.
- **Cleaner career navigation.** The top-level workspace is now labelled **CAREER** rather than
  the prototype-era **STATS**, and Route Map lists identify current campaign target destinations
  before the player opens their detail.
- **Objective progress now matches the objective.** When the next action is to accept a contract,
  the persistent card measures the current campaign chapter and names the current Adelaide base
  capability instead of showing unrelated next-aircraft purchase progress.
- **Career targets read on the map.** Current-chapter destinations now use the caution treatment
  directly on the Australia map as well as the destination list/detail.
- **One capability language across Career, Fleet and Contracts.** Fleet identifies the current
  Adelaide base capability, while locked aircraft/contracts name the base capability and tier
  actually required instead of presenting tier labels as unexplained game levels.
- **Career base roadmap.** The old generic next-tier block now reads as current Adelaide base →
  next base, with the existing real progress bar, exact remaining requirements and resulting
  operating capability.
- **Map → Contracts shortcut.** A selected career-target destination now offers **VIEW CONTRACTS**
  so the player can move straight from route intent to the contract workflow.

- **Mute actually silences, and engines are not a wall of pitched-down takeoff (ADR 0089).**
  M / Options set the audio listener immediately. Recorded beds only play while
  engines run, stay 3D, and keep native pitch.
- **Evening last flights sit near curfew, not 40 minutes after launch (ADR 0088).**
  Launching at ~21:00 no longer dumps a fake morning peak that dies at 21:38.
  Arrivals stretch to 22:50; Qatar and Emirates keep the real 22:00 slot.
- **Adelaide curfew 23:00–06:00, player and RFDS exempt (ADR 0087).** Commercial
  AI no longer pushes or lands in the real closed window. You can still fly out
  at night; so can RFDS (`VH-FDA`).
- **Short final no longer freezes at ~130 ft** while another aircraft lands.
  A small S-turn on the 80 % pin so a vacate wait is not a statue.
- **Recorded engine audio** for Dash 8-400 (PW100), other turboprops (Dash 8-300
  twin), and jets (CC0 turbine), pitched through startup and takeoff.
  Unity EditMode **868/869** (only the known gate lead-in decision test).
- **Operations board and pavement are sim aircraft only (ADR 0086).** The board lists
  the player and AI fleet that actually exist — no timetable "Listed" rows for planes
  that are not on the map. Live ADS-B defaults off and, even when enabled, stays in
  the sky; the ground is AI/player only.
- **Real Adelaide sun and moon.** The key light and visible discs follow a celestial
  path for YPAD: noon is north of the runway, the sun walks east → west, the moon has
  its own place and phase rather than sitting opposite the sun.
  Unity EditMode **864/865** (only the gate lead-in decision test).
- **Routine aircraft checks (ADR 0085).** Each aircraft wears a little every rotation and
  needs a check every eight. You choose when: send it from Fleet while it is parked (2 hours
  and $400 for the Saab; four hours and 6% of list price for a jet). Flying on past due costs
  2 reliability per extra rotation. The objective card tells you when a check is overdue or
  under way. Save v11 (older saves load as freshly checked).
  Unity EditMode **855/856** (only the gate lead-in decision test).

- **Twelve more career contracts, now actually on offer (ADR 0084).** Mount Gambier, Ceduna,
  Coober Pedy, Mildura, Broken Hill, Canberra, Sydney, Brisbane, Perth, Auckland, Singapore and
  Hong Kong. The Contracts page now leads with the next two your tier allows, then the rotating
  market; hand-written contracts used to show only as an objective-card fallback.
  Unity EditMode **845/846** (only the gate lead-in decision test).

- **Career campaign: five chapters (ADR 0083).** Island Hopper → Eyre Peninsula → Regional
  Network → Interstate → Going Global. Each is a checklist of real goals (contracts and
  where, tier, fleet, reliability) with a one-off cash reward; the objective card is headed
  by the current chapter, the Stats page ticks its goals, and a toast celebrates each
  completion. No save change (rewards use existing settlement keys). Unity EditMode
  **843/844** (only the gate lead-in decision test).

- **Aircraft no longer drive through each other on the ground.** Taxi legs were fixed paths
  on fixed clocks with no ground control: ~36 collision episodes a busy day (taxi-in head-on
  into taxi-out, taxi-outs into the holding queue, vacates into waiting arrivals). New
  `GroundTraffic` ground controller: pushbacks and taxi-ins wait until their whole route is clear
  of moving traffic (re-checked on a 5 s grid so the result never depends on step size; stops
  waiting for stationary aircraft after 3 min so nothing deadlocks); taxi-outs and vacates stop
  behind the queue ahead and shuffle up at taxi pace; the tower sends a waiting departure first
  (or briefly holds the arrival) when a landing's vacate would cross taxiing traffic or a
  holder; wide jets prefer gates that do not crowd a parked neighbour. New
  `GroundSeparationTests`: a whole busy day with zero moving collisions.
  Unity EditMode **837/838** (only the gate lead-in decision test). The first version made the 30-day soak time out: full taxi-pose evaluation cost ~64 µs × ~500k per 3 days; legs now carry a cached 1 s position table for conflict checks (3 days: 33 s → 0.7 s). Also fixed `AircraftCatalogueTests` broken by #361 (A223/A21N shared a test registration; 16L/16R share a pier).
- **Six missing real Adelaide passenger types now have genuine models:** A320-200, 737-800,
  E190, A220-300, A330-900neo and 787-9. Each has a true-scale procedural glTF/FBX kit,
  dedicated catalogue/performance/profile data and a rendered Hangar thumbnail. Live ADS-B
  traffic now maps those ICAO codes to the correct silhouette instead of an A321, 737-8,
  A350 or 787-10 stand-in. Existing Qantas, Jetstar, Malaysia, Emirates and Fiji equipment
  assignments are corrected from current Adelaide Airport evidence. Charter-only and one-off
  visitors remain deliberately out of scope (ADR 0083).

- **Live traffic on the maps and on the ground (ADR 0082).** The feed now covers 250 NM, so
  the Route Map shows real airliners across South Australia (pale icons under your flights;
  callsign, type and height when zoomed in). Live aircraft on the field get "LIVE · callsign"
  tags and mini-map marks. Real aircraft taxiing, landing and taking off are drawn 1:1 on the
  pavement, but always stand aside for the game: hidden while they would overlap one of the
  game's aircraft or sit on a runway the game is using, with a 10 s hold against flicker.
  Unity EditMode **836/837** (only the gate lead-in test awaiting a decision).

- **Arrivals no longer freeze in mid-air on final.** An arrival waiting for the runway (a
  departure rolling, the 2–3 min wake gap after a jet, a storm hold) was pinned motionless
  about 750 m out and 40 m up until cleared — Bailey saw one "land, freeze and sit there, then
  land 30 seconds later". Every arrival also popped into existence at that point. Now the
  tower's expected clearance (`AirlineOperations.ExpectedLandingClearance`, replaying its own
  queue, departure-priority and storm rules) places the arrival back along an extended final so
  it flies in at approach speed and reaches the hold point as it is cleared; inbounds appear up
  to 18 km out. Estimate matched the tower exactly for 43/43 arrivals over two simulated days.
  Unity EditMode **833/834** (only the gate lead-in test awaiting a decision).

- **Live Adelaide traffic in the sky (ADR 0081).** Real airliners within 60 NM of YPAD, from
  the free adsb.lol feed (ODbL, no key), drawn in 3D: Qantas, Virgin, Rex, QantasLink and
  internationals at their real positions, heights and tracks, eased between 10 s updates. Sky
  only and presentation only: never a simulation input, never saved; aircraft on the ground or
  low over the field are left out so they never cross your fleet. Offline or switched off
  (Options → Live Adelaide traffic) the authored sky traffic returns. Credit line adds
  "Live traffic: adsb.lol (ODbL)" while shown.
- **Authored sky traffic was mirrored across the runway** (Melbourne traffic over the gulf) and
  **pitched the wrong way** (departures nose-down). Both fixed; new `YpadFrame` shares the
  layout generator's exact frame.
- Unity EditMode **831/832**; only the gate lead-in test awaiting a decision.

- **Save migration test fixed (test only, no save format change).**
  `VersionFiveSave_MigratesSingaporePlaceholderTo787WithoutLosingRotation` compared the save
  record ("" = no stand) to the restored live aircraft (null = no stand); it only passed while
  seed 73 left the jet at a gate. Now compares a re-capture to the original record. The
  migration itself was always correct. Unity EditMode **822/823** (only the gate lead-in
  decision test remains).

- **Taxi, takeoff and landing: four visible snaps fixed.** A new seam test drives every
  hand-off between ground legs (rollout → vacate → wait → taxi-in → stand → pushback →
  taxi-out → hold → lineup → takeoff roll) for every runway end, type and stand. Before: 256
  seams snapped. (1) **12/30 arrivals** drove on to E2 down the very taxiway their taxi-in
  climbs, flipped 180° and drove back; the vacate now joins the bay corridor and the taxi-in
  starts there. (2) **12/30 lineups** ended 30–41° off the centreline, so the takeoff roll
  snapped straight; they now finish with a 20 m centreline run-up. (3) **Walk-out bays
  10A–10D** (where the starter Saab parks) had a parked heading up to 186° off, and the taxi
  smoothing cut off the painted U-turn; the Saab now follows the line and parks facing out, no
  snap on arrival or at pushback. (4) **Every ground leg now starts on its own tangent**
  (look-ahead ramps up from 1 m), so a curved pushback no longer twists the parked aircraft.
- **HUD: "an ATR 42-600", "an Alice Springs contract".** Objective card, Fleet, Contracts,
  Route Map and refusal messages wrote "a ATR" / "a Airbus". New `Article` helper.
- **HUD: Route Map RIVALS toggle no longer covers the LOCKED pill** on narrow windows
  (under ~700 px wide); it drops below the pills when the row is too short.
- Unity EditMode **821/823**; only the two known baseline failures (`VersionFiveSave_…`, `Reservations_GateLeadIn…`).

- **HUD fix: build stamp and map credit no longer draw over panel edges.** Both labels sat
  at `height − 22` while every airline HUD panel ends at `height − 16`, so the new `sha ·
  branch` stamp ran across the bottom border of the selected-aircraft card and every
  workspace. They now have real rects in `HudLayout` (`MapCredit`, `BuildStamp`) in the
  16 px strip under the panels, and the stamp hides on windows too narrow for both. New
  layout test covers seven window sizes. Unity EditMode **811/813**, only the two known baseline failures (`VersionFiveSave_…`, `Reservations_GateLeadIn…`).

- **Build identity stamp (ADR 0080).** Each editor Play and each `scripts/build-mac.sh`
  rebuild writes a gitignored `build-identity.txt` (commit, branch, dirty, commit time,
  built time). The running game shows `sha · branch` on the bottom edge and the full line
  in the pause menu; the Mac app's Finder version is the same short SHA. Domain 570/570.

- **Hangar goal on the objective card + on-time reliability (ADR 0078).** No active contract
  → objective shows "Save for a …" with `$funds of $price · N of M rotations` toward the next
  hangar buy (usually ATR). Pushback lateness vs booked time adjusts reliability (+1 / 0 / −1 /
  −2); contract pay stays exact. Save v10. Domain 565/565.

- **Saab starter, bit-by-bit fleet unlocks, light finance pressure, opening redo (ADR 0077).**
  New careers start with a Saab 340B and $2,800 — not an ATR and $4,000. ATR is the first hangar
  step ($5,200); Dash 8 / jets climb on stepped prices and rotation gates. Intro contracts need
  a Saab; dispatch costs a bit more so you fly to earn the next type. Setup + intro sell the
  ladder (Saab → ATR → Dash 8 → jets). Domain 559/559.

- **NOW follow, imperative objective next-action, smoother pushback.** Operations NOW no longer
  sticks on live Outbound rows (09:55→11:55 bug) and the board scroll keeps following NOW until
  you scroll. The objective card's yellow line is a concrete verb (finish fuelling / schedule to
  Kingscote / accept contract / follow for pushback / buy in Fleet). Pushback paths are
  `DrivablePushback`-smoothed; disconnect heading check tightened. Domain 558/558.

- **Aircraft-aware competitive Route Map (ADR 0076).** Replaced three generic distance rings with
  the selected aircraft's real practical-range ring. The static network now draws only the selected
  route (plus the existing hover preview), destination labels reveal progressively with zoom, and
  actual airborne AI competitors can be shown or hidden through `RIVALS n · ON/OFF`. Ambient sky
  traffic no longer masquerades as strategic competition. Domain 555/555; shared 1440x900 Route Map
  render inspected.

- **Authored cumulus atlas and production cloud cards (ADR 0075).** Replaced the still-obvious
  connected-sphere cloud clusters with 16 distinct transparent cumulus silhouettes. Each cluster is
  now one camera-facing renderer rather than three geometry layers, while seeded placement, real-wind
  drift, weather reveal and ground umbras remain deterministic. The custom shader cleans low-alpha
  colour fringe, applies day/weather tint and is explicitly retained in player builds. Packaged review
  caught both initial shader stripping and compressed cards/over-dark shadows before final tuning.
  Domain 553/553; Unity 787/789 with the same two unrelated baseline failures; fresh Mac build and
  forced-Overcast 1920x1080 capture inspected.

- **Distance-aware Adelaide ground clarity (ADR 0074).** Rebuilt the 2048px runtime surroundings
  image from a 4096px ESA WMS request so runway alignment no longer throws away source information
  before the final downsample. The runtime texture size and memory stay unchanged. Near the airport,
  the shader now favours authored terrain detail; beyond 5.2 km it returns to stronger satellite
  context, with an exact edge match that avoids revealing the rectangular authored-ground boundary.
  Authored ground layers retain a little more detail at oblique camera angles through conservative
  quality-aware mip bias. Domain green; Unity 787/789 with the same two unrelated baseline failures;
  fresh Mac build and final 1920x1080 overview inspected.

- **Airport-scale ground rhythm and honest route-range rings (ADR 0073).** Adelaide's procedural
  infield now carries subtle 34 m runway-aligned mowing bands, softened along their length and
  suppressed where worn-dirt weight takes over. This breaks up the former single washed carpet
  without painting detail over runways, aprons or taxiways. The Route Map now draws true geodesic
  500, 1,000 and 2,000 km rings from Adelaide, with restrained dashed strokes and labels, so range
  and network expansion can be judged spatially rather than from a list alone. Domain suite green;
  Unity 787/789 with the same two unrelated pre-existing failures; shared map and fresh Mac ground
  captures inspected.

- **Layered cloud silhouettes and weather-review control (ADR 0072).** Replaced each stretched
  one/two-sphere cloud with a deterministic three-layer cluster: a broad shaded underside, five
  to seven irregular body lobes and smaller sunlit crowns. The result keeps the existing real-wind
  drift, cover-based reveal and moving ground umbras, but reads as a cloud bank instead of isolated
  translucent blobs. Opaque depth-tested massing avoids exposing every lobe intersection from the
  overview camera. The three meshes are combined per layer, so the Adelaide sky remains bounded
  at 48 cloud renderers rather than using expensive realtime volumetrics. Added
  `-airsideReviewWeather` for deterministic packaged-build weather captures. Domain 550/550;
  Unity 784/786 with the same two unrelated pre-existing failures; fresh Cloudy Mac build captured
  and inspected.
- **Operations board honesty, day orientation, taxi weave, schedule feel (ADR 0071).**
  Published day-plan rows no longer invent Gate/Bay occupancy when nothing is parked —
  STAND is "—" and STATUS is "Listed"/"Expected" until a live aircraft covers the slot.
  Day caption reads "N on field · M listed ahead"; NOW markers on the strip and board;
  cancelled morning slots no longer steal the NOW divider. Taxi presentation weave reduced
  (~0.55 m → ~0.18 m). AI turnarounds lengthened toward real Adelaide dwells. Live Adelaide
  traffic feeds declined (determinism/offline/licence) — static authored snapshot remains the
  only approved path to a more timetable-shaped day. Numbered 0071 after main claimed 0070 for
  the competitive Career HUD. `scripts/test-domain.sh` 548/548; Operations HUD re-rendered via
  hud-mockup.

- **Competitive Career HUD and calmer navigation (ADR 0070).** Career now shows an honest
  Adelaide activity rank derived from the completed rotations already recorded for every player
  and AI aircraft. Wide layouts show up to five leaders, always retain the player row and name the
  next carrier plus the rotations needed to pass it; narrow layouts retain the rank summary without
  forcing in a cramped table. Selected navigation tabs now use a precise blue underline instead of
  a large filled block. Workspaces below 680 points take the full width, fixing the pre-existing
  Career overlap at 1024×640. The offline HUD renderer now finds native macOS fonts as well as its
  Linux defaults. Domain 550/550; Unity 784/786 with the same two unrelated pre-existing failures;
  1440×900 and 1024×640 draw-list renders and a fresh Mac build checked.

- **Real Adelaide operational buildings and painted stand references (ADR 0069).** Added a
  reproducible ODbL snapshot and generated runtime geometry for 78 real YPAD operational
  footprints, including the 44 m control tower, airport fire station and 12 hangars. The detailed
  terminal/RFDS shells remain authoritative; retail/residential clutter and the airport boundary
  are filtered out. Stand/gate references now use the existing stroke alphabet as actual painted
  mesh geometry instead of world-space font labels. Also fixed two current-main Unity compile
  blockers exposed by the verification pass (`reilFlash` local-name collision and the Stats test
  assembly's inaccessible `CloseBox`). Domain 546/546; Unity 780/782 with the same two unrelated
  pre-existing failures; fresh Mac build and deterministic overview/tower/stand visual checks
  completed.

- **Clearing the standing backlog: seven items fixed (ADR 0068).** Everything previously
  flagged and deferred, in one pass. (1) The go-around teleport — the visible jump when a missed
  approach's racetrack ends is now a smoothed 6-second blend to the pinned holding position
  instead of an instant 400-1,100m/210-250m snap; not a physically accurate rejoin (the two
  flight-path systems have no shared parameterisation), but the jump itself is gone. (2) Terminal
  glazing now follows the real curved wall (`AdelaideTerminalArchitecture.AirsideWallZAt`,
  interpolated from the actual OSM footprint) instead of one hardcoded Z that put the near end
  2.14m off the building. (3) Rain and clouds now drift with the actual surface wind instead of
  a fixed direction — previously only the windsock reacted to wind at all. (4) Overcast now
  shows visibly more cloud than a partly-cloudy day (each cluster has its own cover-based reveal
  threshold) instead of the same fixed cluster count at every cover level. (5) Airline rename has
  a real HUD control now — a text field in the Stats page header. (6) The Stats page's empty
  lower-right now shows a milestones-reached count and a lifetime contracts-fulfilled total
  instead of blank space (a real overflow bug in the first draft of this was caught by a new
  test before merge, not after). (7) A stroke-painted alphabet (0-9 plus the letters Adelaide's
  real stand/gate references use) now exists, extending the runway's own numeral system — not
  yet wired into the actual stand labels, since that swap is real integration risk for a
  cosmetic win with no way to verify it without seeing it rendered. `scripts/test-domain.sh`
  542/542 (16 new tests); terminal glazing and the stroke alphabet are the first Presentation
  fixes this session with genuine automated proof rather than inspection-only review.

- **Livery repaint wired into the Stats HUD (ADR 0067).** `AirlineOperations.SetLivery` (ADR
  0066) had no way to actually be reached in-game. The Stats workspace's Overview column now
  has a LIVERY row of six clickable swatches — the same palette offered at airline creation,
  now a single shared source of truth instead of two copies — with the current livery
  outlined. Rename still has no HUD control (needs a text field, a riskier piece of IMGUI than
  a row of buttons; left for its own pass). Verified by re-rendering the Stats page via
  `scripts/hud-mockup` at two viewport sizes, not just reasoned about. `scripts/test-domain.sh`
  527/527.

- **Career Stats workspace, and nine supporting features (ADR 0066).** A fifth HUD tab
  ("STATS") gives a real, accurate view of career progress: funds, lifetime revenue (never
  reduced by spending, unlike the balance), reliability, tier, fleet size, exactly what the
  next tier still needs (rotations/reliability/a specific aircraft type — a direct answer to
  "what is Regional?"), 11 career milestones, and recent fulfilled contracts. Built and
  rendered the same way as the other four workspaces (ADR 0057) and actually re-rendered via
  `scripts/hud-mockup` to confirm it lays out cleanly, not just reasoned about. Supporting
  features: airline rename and livery recolour (`AirlineOperations.RenameAirline`/`SetLivery`,
  validated, player-only); aircraft resale (`SellAircraft`, refunds 55% of purchase price,
  parked aircraft only, surfaced as a "Resale value" line on the Fleet detail panel — also
  re-rendered to confirm); and a reliability-linked pay multiplier (neutral at and above 70%
  reliability — so a fresh career is completely unaffected — a real penalty below it, contract
  pay itself untouched). Save format bumped to v9 for the new lifetime-revenue/contract-history
  fields; a pre-9 save loads with both empty, same tolerance as every earlier version bump.
  `scripts/test-domain.sh` 526/526 (26 new tests; full suite re-run to confirm the reliability
  multiplier doesn't disturb any existing exact-funds assertion, since 100% starting reliability
  keeps it neutral everywhere else).

- **Departure-turn jump, unrealistic departure spacing, stand-queue overflow, apron light
  starvation (ADR 0065).** Four bugs from one play session. (1) Departing aircraft jumped
  sideways mid-climb then snapped back — `ApplyDepartureTurn` compared Takeoff's own progress
  against a threshold (0.88) that only means anything on Departed's progress scale; fixed to
  only ever apply past that point during Departed itself. (2) The next queued departure waited
  through the whole previous departure's climb-out (not just its ground roll) plus wake
  separation before it could even start rolling — the runway-occupancy calc was counting
  already-airborne climb time; now uses ground-roll-to-rotation only, matching the pattern the
  arrival side already used correctly. (3) A landing aircraft whose gate was full queued at a
  taxiway holding point that ran out of room past a certain queue depth, at which every further
  aircraft collapsed onto the same spot instead of a real place to wait — now extrapolates past
  the taxiway's end in a straight line (an existing helper built for exactly this, just not used
  here). (4) Apron floodlights were correctly placed and lit but starved out by URP's
  per-object light cap (12, 4 on Medium) — with hundreds of runway/threshold/ALS lights and
  stand markers competing for the same budget on nearby ground meshes, the 10 apron floods could
  easily lose the competition; raised to 24/12. `scripts/test-domain.sh` 500/500 for the two
  Simulation-layer fixes; the other two are Presentation-only, reviewed by inspection.

- **Night moonlight for form shading, and a backwards vignette fixed (ADR 0064).** Direct
  follow-up to ADR 0063, same "can't see anything at night" report. Raising the ambient floor
  made the field brighter but ambient light is flat — nothing to differentiate an aircraft from
  a hangar from open grass, just a lighter grey wash. Raised the night key ("Sun") light's floor
  intensity 0.18 → 0.30 (still far under every flood/runway light, and under daytime's 2.05) so
  surfaces get an actual lit/shaded side. Also found the vignette was backwards: stronger at
  night (0.1) than day (0.05), darkening the corners hardest on exactly the frame already
  reported as too dark — flipped to `Lerp(0.04, 0.07, daylight)`. Both Presentation-only,
  reviewed by inspection; `scripts/test-domain.sh` 500/500 (unchanged, no Domain-layer change
  this round). Still not confirmed by an actual look at the game — highest-priority open item.

- **Night visibility floor raised; Fleet copy names real destinations (ADR 0063).** A real
  play session reported "I can't see anything" at night. Every light source's own intensity
  was confirmed correct — the actual cause: the default Fleet/career overview camera sits
  2400m out over a ~3900x2800m field while every flood/runway light only reaches 9-115m, so
  from the player's actual starting view almost the whole frame was ambient-only, which a
  -0.12 EV night exposure and ACES tonemapping plausibly crushed toward black. Raised the
  night ambient floor and exposure (light sources themselves untouched, so floods/runway
  lights should still read as the brightest features) — a reasoned, conservative correction,
  not a confirmed-by-eye fix; still needs a real look. Separately: buying an aircraft's "flies
  Domestic routes" / "Requires Regional tier" copy used the word "Regional" for two unrelated
  systems (a route-destination band and an unrelated career-progression tier) with nothing to
  tell them apart. New `RouteAccess.ExampleDestinations` names real places
  ("Domestic capability (Melbourne, Sydney, +closer)"), and the tier line now says "career
  tier" to disambiguate. Verified via `scripts/hud-mockup` re-rendering — the first version
  clipped against its own text box, caught immediately and fixed. `scripts/test-domain.sh`
  500/500.

- **Contracts card fill, terminal roof fixes, weather fog tint (ADR 0062).** First real use of
  the offline HUD mockup renderer this session to *see* a change instead of reasoning about it
  blind: found the Contracts market's offer cards pinned to a fixed 86px height regardless of
  the column's actual height, leaving most of a tall window empty below just three cards (the
  market only ever runs three at a time). Cards now grow to fill the space (capped at 172px),
  content centred rather than stretched. Terminal: the roof brow's last segment cantilevered
  17.2m past the building's own real footprint into open air — narrowed to fit; roof
  plant/equipment screens now share the brow's corrugated-metal texture instead of rendering
  flat colour beside it. Weather: Cloudy/Overcast/Rain/Fog/Storm all rendered the *identical*
  fog colour (only density differed) — now Storm/Rain/Overcast darken toward slate grey with
  Gloom, Fog blends toward a pale near-white haze instead (the one weather kind whose
  visibility loss genuinely outruns its own gloom). Also found and thoroughly documented, not
  fixed: a go-around's re-entry to the landing queue teleports position/altitude/gear
  instantaneously (400-1100m and ~230m in one tick) — a real bug needing two different
  flight-path systems blended correctly, judged too risky to attempt without being able to
  watch the result. `scripts/test-domain.sh` 499/499 (unchanged, no new Simulation behaviour).

- **Road lane markings, bolder apron labels (ADR 0061).** Landside roads had no lane markings
  at all — a flat asphalt ribbon blended 92% toward the real satellite photo underneath it, so
  it read as a grey band cut out of the aerial image rather than a marked road. Added a dashed
  white centreline (own mesh/material, so the paint isn't diluted by the road's satellite
  blend) on every road wide enough to already draw a ribbon. Also bolded the painted apron
  stand/gate identifier labels, which used Unity's default `TextMesh` weight next to the
  runway's own purpose-built stroke-drawn designation numerals. Runway appearance was checked
  and left alone — paint, texture, rubber wear and lighting were already comprehensive; no
  building a stroke-based letter alphabet for stand references attempted here (a much larger,
  Unity-verification-dependent job, flagged as a real follow-up instead of guessed at).
  Presentation/Unity-only, outside the headless harness; `scripts/test-domain.sh` unaffected
  (499/499).

- **Departures actually turn after the SID establishes (ADR 0060).** Once the destination-track
  turn locked in, the aircraft used to keep translating along the *original* runway heading
  forever — the nose held the new heading while the ground track kept going straight down the
  extended runway line, a steady crab/drift for most of the visible climb-out rather than a
  momentary artifact. New `DepartureTurn.EstablishedTrackMetres` (pure, unit-tested) decomposes
  any further along-track distance onto the established heading instead. Also fixed two latent
  sign bugs found while working this out: `YawDegrees`/`Forward`'s doc comments (and `Forward`'s
  actual return value, dead code though it was) claimed a right turn is +Z in the runway-05
  frame; it's −Z, matching `LateralMetres`. `DepartureTurnTests` (+5); `scripts/test-domain.sh`
  499/499. Presentation-only change unverified in Unity — flagged explicitly given the risk of
  a sign error here being highly visible.

- **Two graphics/UX bugs fixed after a targeted audit of maps, aircraft, taxiing and the HUD.**
  A parked, gate-side aircraft's nose taxi spotlight used to switch on at night for up to two
  minutes before every departure and 35s after every arrival — engines spool up while still
  `AtStand`, and the taxi-light condition's `night ||` made the ground-movement-phase check
  meaningless after dark. Now requires an actual taxi/pushback phase regardless of day or night.
  Separately, a contract offer's "first click arms, second click commits" safeguard could stay
  armed after leaving the Contracts workspace — a forgotten arm plus one exploratory click on
  the card (not just its Accept button) later would silently sign a real commitment; now clears
  centrally whenever the Contracts workspace isn't the active one. Maps and taxi/ground-motion
  audits came back clean — see `GAME.md` for what was checked. Both fixes are
  Presentation/Unity-only and reviewed by inspection; `scripts/test-domain.sh` unaffected
  (494/494, unchanged).

- **Two performance bugs fixed in the storm lightning work (ADR 0059 update).** A requested
  performance review found: `Lightning.StrikesAt` re-walked its whole storm-block ladder from
  scratch on every call (quadratic over the block, though never expensive in absolute terms) —
  now memoises the last two adjacent strike times, so most seconds answer with zero hashing.
  The procedural thunder clip was built synchronously the first time it was needed (mid-storm,
  on the audio hot path) instead of pre-warmed like the wind/rain/coast beds — now generated
  eagerly alongside them. Same behaviour, `scripts/test-domain.sh` still 494/494.

- **Storm lightning/thunder, and a sun/moon that hides behind cloud (ADR 0059).** New
  deterministic `Lightning.StrikesAt` (same pure-hash-of-time style as `Weather.At`) fires a
  flash-plus-thunder every 5-13s during a storm: a ~0.5s double-pulse brightens the sun/
  ambient/fog/sky, and a procedurally synthesised crack-and-rumble plays after a distance-based
  delay (sound lags light). Also fixed a real gap found while checking the rest of the weather
  visuals: the sun/moon discs used to stay fully bright regardless of forecast, so a storm or
  overcast sky still showed a crisp sun — they now fade out under Cloudy/Overcast/Rain/Storm.
  The rest of the cloud/fog/rain system was already weather-reactive on inspection (cloud
  alpha, tint, ground umbras and rain speed/thickness already scale with `WeatherLook`) — no
  changes needed there. `LightningTests` (+6, new file); `scripts/test-domain.sh` 494/494.
  No real thunder sample sourced yet (procedural only, see `docs/data/ASSET_AND_DATA_REGISTER.md`
  AUD-006); Presentation changes are unverified in Unity, pending `scripts/test-unity.sh`.

- **Storms now hold the runway (ADR 0058).** Weather was cosmetic plus a flat daily
  surcharge (ADR 0013); a `Storm` block now withholds every *new* landing/takeoff clearance
  on both strips until it clears — arrivals hold, departures wait short — while anything
  already rolling, on final or taxiing continues undisturbed. The Operations subtitle names
  the current weather and shows "GROUND STOP" while a storm is holding traffic. Deterministic
  under any step size: `NextEventAt` now also stops at the next weather block boundary while
  a runway is wanted and storm-held, so a big catch-up jump cannot land past the moment the
  storm actually cleared. `RunwayWeatherTests` (+2), `OperationsWorkspaceTests` (+1).

- **Aircraft: no gaps, flush doors, proper windows (all seven types).** Doors, windows,
  windscreens and cockpit panes are now curved shells that follow the fuselage a few mm
  proud, instead of flat quads that sank into the skin (a 737 door showed only its top and
  bottom edges; the Saab's flat-box doors stood 20 cm off the curve). Windows are tall
  rounded panes at the real 0.51 m pitch and cabin height. Every part now chains back to
  the fuselage: 737-8/A321neo wicks and nav lights were ~1.7 m above the winglets and the
  main gear hung ~1 m under the wing; A350/787 mains sat 4.6 m behind the wing (now on the
  published 28.66 m wheelbase with a gear-bay pod); the Q400's whole wing/engine assembly
  floated 10 cm above the fuselage crown; wheels now sit on axles; pitots, landing lights,
  taxi lights and nose-gear doors were buried or hovering. The 737 nose is rounded and the
  hood that swallowed the cockpit side windows is gone; Saab and 737 tailplanes sit on the
  fuselage. `scripts/audit-aircraft-geometry.py` and `scripts/test-aircraft-connectivity.py`
  keep it that way.

- **Hangar thumbnails no longer show a slate slab.** The offline renderer sorted triangles by
  centre (painter's algorithm), so the 737-8/A321neo livery stripe painted over the fuselage
  top. It is now a depth-buffered rasteriser, and all seven thumbnails are re-rendered.

- **Smoother, more logical pushback and taxi.** Measured by driving every taxi-in,
  pushback + taxi-out and vacate leg at 0.25 s: worst nose swing 396 → 37 deg/s, worst yaw
  acceleration 2880 → 43 deg/s², legs with a mid-route crawl 39 → 0. Fixes: the pushback tug
  turn blends from the heading the aircraft already has (it snapped back ~95° on curving
  pushes such as bay 10D) with its turn direction fixed at the start; hairpins, spurs and
  loops in the baked and router-built routes (the 12/30 crossing that dropped to 0.4 m/s
  and swung 150°, a gate taxi-out that drove 28 m the wrong way and reversed, a loop before
  Gate 20R) are rounded to no tighter than the type's wheelbase; the presentation weave now
  fades in with speed instead of popping the airframe up to 0.55 m sideways at start/stop.
  Locked in by `GroundMotionSmoothnessTests`.

- **Fix the Unity compile break on main** (`using System;` in `EngineStartSequence.cs` and
  `AdelaideDayPlanTests.cs`, an NUnit constraint, `IsMissedApproachLanding` visibility, and a
  `RunwayWeather.At` clock argument).

- **Arrivals queue on final.** Holding traffic stacks in wait order
  along the approach — number-one on short final, later aircraft
  further out — instead of sitting on a registration hash.

- **T1 traffic side is a road, not a lawn.** OSM arterials now draw
  through the terminal-north notch the operational core used to skip,
  plus an authored drop-off, return loop, short-stay pad, cars and
  lamp posts on the real Sir Richard Williams side.

- **Weather look is one profile.** Cloudy / overcast / rain / fog /
  storm share CloudCover, Precipitation, Gloom, Visibility and
  Wetness. Adelaide now gets kilometre-scale clouds and rain. Flight
  timing is still unchanged (ADR 0013).

- **More of Adelaide's real airlines, on a 05:00–23:00 day.**
  Malaysia, Emirates, Qatar and Fiji join the terminal. Jetstar
  flies Bali; Qantas flies Auckland. The field has no curfew —
  domestics from 05:00, late internationals after 21:00. Widebodies
  turn 50–89 minutes. The day-plan now lists Qantas and Jetstar.

- **The opening bank has more arrivals, at peak spacing.** A third
  Virgin 737, a third Qantas 737 and a second Jetstar join. Twelve
  aircraft are already inbound, spaced 2–37 min (about one every three
  minutes across both strips). Opening departures go every ~5 min
  instead of every three.

- **12/30 takeoff and landing match the stated knots.** Stations shift
  so the 05 threshold lines up with the 12 threshold; one metre of 05
  stays one metre of 12/30. The old squeeze made regionals crawl at
  about half speed while the HUD still read 05 knots.

- **The field is a bank, not a single-file queue.** Qantas and Jetstar
  join Virgin, ANZ, Singapore and the regionals. Several arrivals are
  already inbound at a new game, two aircraft may taxi on the same
  apron, and 05/23 can move while 12/30 does.

- **Stands look like stands.** Each gate and bay gets a lead-in that
  follows the taxi-in, a stop bar, an envelope box and a readable
  painted number.

- **Trackpad drag is slower.** Orbit and pan no longer leap when a
  finger moves a few points, and a grazing horizon grab cannot teleport
  the camera.

- **International career is reachable.** A321neo buys at Domestic. The
  International tier unlocks on jet ownership + rotations/reliability —
  not on already owning a widebody (that was a deadlock). A350/787 stay
  International purchases.

- **Tower keeps the lined-up / short-final runway end.** Clearance no
  longer flips 05↔23 or 12↔30 and teleports the aircraft. Empty-stand
  holding uses the 23 lineup start, not the 05 hold. Cross go-arounds
  start on a native short final outside the threshold.

- **Fifteen more Adelaide play fixes.** Cabin doors stay open through
  boarding; cancelled bookings stay cold and tag as cancelled; windsock
  follows sim wind; approach pick/follow uses the assigned runway frame;
  AwaitingStand shares one E2 queue; cross clear-of-runway is longer to
  reduce exit conflicts; gate lead-in held through hold/takeoff; tower
  refreshes the live end on clearance; AtDestination off Arrivals;
  TaxiIn TIME is ETA STAND; holding finals get look-ahead facing; bad
  saved runways fail restore; DepartureStand required for outbound
  ground states; empty-stand holding uses the runway hold not bay 50D.

- **12/30 landing and lineup no longer teleport.** Vacate starts at the
  remapped rollout end; lineup ends at the remapped takeoff start.

- **Arrivals follow the wind, not the away city.** Dest-aligned runway
  preference stays for departures only. 12/30 go-arounds fly a native
  cross-strip circuit instead of remapping the 05 racetrack through the
  terminal.

- **Save mid-vacate frees 12/30 at clear-of-runway.** Delayed day-plan
  rows mute from EstimatedAt, not ScheduledAt.

- **12/30 frees when clear of the strip.** Vacate still taxis toward E2,
  but the tower clears the next 12/30 movement after ~280 m off the
  pavement — not after the full kilometre exit.

- **FIDS names the movement on the field.** Landing reads On final /
  Landing / Vacating; takeoff reads Lining up / Departing; a missed
  approach reads Go-around before the circuit starts.

- **Go-around aborts off short final.** Missed approaches no longer
  teleport 4 km out for a full long final. Rejoin reassigns the live
  runway end. Both strips reconcile free-at on every save load.

- **Day-plan cover survives a late stand wait.** CoveredBy pins late
  AwaitingStand / go-around rows to the planned ETA so ghost sky
  arrivals do not reappear. One live aircraft covers only its nearest
  same-route slot. Soak assigns fitting stands only.

- **Cathay leaves when the season ends.** Parked CPA frees GATE-18 after
  ~27 Mar; airborne jets finish the trip first. Prep no longer starts at
  book time — fuelling begins TotalSeconds before pushback. Rebook
  recomputes PrepStartedAt; a taken GATE-18 falls back to another gate.

- **Operations opens near now.** Arrivals/Departures snap the scroll to
  the first live or upcoming row instead of a wall of morning Departed
  lines. Day-plan stands respect StandFits (no Q400 on walk-outs).

- **International tier needs a real widebody.** A321neo no longer counts
  as wide. Hold-short poses use the aircraft's own type. Pre-dual-strip
  saves reconcile the cross-runway free time on load.

- **Operations shows where you are in the day.** A day strip under the
  header tracks the 06:00–21:00 operating window with a now caret, bank
  density, and done / live / to-go counts. Past planned rows mute and
  read Landed or Departed instead of Expected forever.

- **05/23 and 12/30 move in parallel.** The tower keeps a free time per
  strip, so a regional on 12 no longer waits for a jet wake on 05.
  Go-arounds only count holders on the same strip.

- **Regional bays stay held through taxi-out.** A landing cannot take
  the same bay while the departure is still on the apron.

- **Post go-around landings resume from short final.** Only the missed-
  approach state draws the long inbound; the real landing after that
  no longer teleports 4 km out.

- **Day-plan rows no longer vanish under the wrong half.** An outbound
  to Port Lincoln does not suppress the planned arrival from Port
  Lincoln. Idle parked aircraft stay off the Departures board.

- **Hold queues, planner ETAs and walk-outs match the field.** Queue
  slots are per runway; trip previews use 12/30 for regionals; AIP
  walk-outs are SF340-only. The board subtitle names both active ends.

- **Adelaide now has the current Terminal 1 stand map.** Aerobridges
  12L–29 (L and R of the same pier cannot both be occupied), regional
  50A–G, and walk-out 10A–D / 2A all have taxi routes and markings.
  Opening traffic still uses a handful of them; the rest sit empty the
  way a real morning apron does.

- **Departures fly the remaining runway before they turn.** Climb-out
  stays wings-level over the strip; the SID bank starts after the far
  threshold and rolls the wings with the heading change.

- **Options is a real settings page.** Sound, aircraft tags, the
  airfield map, follow-on-select, invert orbit and camera speed persist
  and change the session. Esc opens the Adelaide Airport menu.

- **The field is named Adelaide Airport.** Intro and pause title drop
  the generic regional-airport line.

- **Live stands stay the operating subset.** Gates 13, 15, 18L and 20L
  plus regional 50A–F match the OSM parking that already has taxi
  routes. Extra Adelaide parking lines are mapped, not live.

- **The field edge no longer draws a bright hairline.** Airport and
  paddock share the same satellite dissolve, and the join is lit from
  the land, not the tuck-under cliff.

- **Overhead traffic faces the path it is drawn on.** Near the field
  it moves and descends at a readable speed. The opening bank is four
  regional arrivals plus Air New Zealand; AI turns are 10–24 minutes.

- **Operations matches the field.** Taxi-in reads Taxiing, not Landed.
  Takeoff TIME is DEPARTING. Arrivals waiting for a bay say parking.

- **12/30 arrivals wait on their own exit.** A second arrival no
  longer queues back along the 05 vacate.

- **The speed readout is off until you pick a plane.** Selected or
  followed only — a parked Rex does not keep the strip up.

- **12/30 taxi follows the taxiways.** Regional taxi-out and vacate
  walk the OSM centreline graph (T4–K–A–G1 to 12, A6–D1 across 05/23
  to D2 for 30) instead of a five-point chord across the grass.

- **Operations reads like a movement board.** Status is Taxiing,
  Holding, Departing, Departed, Inbound or Landed — not "returning"
  or "taking off". Planned arrivals say Expected. The aircraft column
  is gone; the operator sits under the flight number.

- **The field opens on a bank.** Four aircraft are already inbound,
  the rest of the AI push inside half an hour, and turnarounds are
  18–40 minutes so the apron does not sit idle.

- **Departures turn toward the destination.** After rotate the nose
  yaws onto the booked track instead of climbing forever along the
  runway heading. Climb-out banks with that turn.

- **Fuselage titles are short wordmarks.** REX, VIRGIN, AIR NZ and
  SOUTHERN CROSS replace the full legal name; pale accents drop to
  dark ink so the paint reads on white metal. The cheatline tint is
  stronger.

- **Speed and live stats stay on screen.** Airline mode shows kt,
  altitude and heading on the bottom readout and on the selected /
  followed aircraft card.

- **Pushback no longer faces the stand and then snaps 180°.** The tug
  keeps the parked heading and turns the nose onto the taxi heading
  through the last part of the push, so disconnect is a turnout.

- **Taxi looks like a human is steering.** Ground heading looks 16 m
  ahead, visual corners use an 18 m fillet, yaw damps slower on the
  ground, and the weave is large enough to read.

- **Runway names are painted on the pavement.** 05 and 23 sit after the
  main-strip thresholds; 12 and 30 sit on the cross strip, the same way
  they do at a real field.

- **Jets use 05/23; everyone else uses 12/30.** Regionals taxi to the
  cross-strip holds, line up, take off, land and vacate there. Jets stay
  on the 3 100 m strip.

- **The default field has night lighting.** Apron floods, stand markers,
  denser runway-edge and taxi lamps, and 12/30 threshold/approach lights
  come up at dusk. Landside streetlights stay off.

- **The board now shows delays and cancellations.** A slice of the
  published Adelaide day is late or cancelled (more in the banks), and
  AI rotations pick up the same. Cancelled live aircraft sit the slot
  and rebook; delayed ones push back later.

- **Arrivals fly the assigned final, not the land-side circuit.**
  Holding traffic sits on short final — over the gulf for 05, from the
  north-east for 23 — so the status line and the approach path match.
  A normal landing continues from there instead of teleporting 4 km
  out and crawling the last seconds (the fleet centreline drift is no
  longer treated as a number-two hold).

- **Each flight has its own track.** Two services to the same city no
  longer share one great circle; the flown line is offset per callsign
  and meets again at the airports.

- **The square line around the airport map is gone.** The mini-map no
  longer draws a bevelled panel box, and the rectangular perimeter
  fence stays off unless `-airsidePerimeterFence` is set.

- **Runways are named 05/23 and 12/30, and jets stay on the long strip.**
  Status lines say the assigned end (holding short 05, landing 23). Jets
  take 05/23 and will take the destination-aligned end when the wind is
  close — a Perth 787 does not climb out inland. Turboprops follow the
  wind more tightly. The mini-map labels both strips.

- **The mini-map paints the gulf.** A 23 climb-out over the ocean no
  longer sits on grass just past the 05 threshold. Bounds extend west and
  the OSM sea polygon is filled as water.

- **Sky traffic is no longer one shelf.** Overflights are converted into
  the runway frame (so they sit over the gulf and the hills correctly)
  and display height separates turboprops, narrowbodies and widebodies.

- **The passenger terminal is back on the default field.** It sits on the
  landform height instead of runway Y, so it does not float over the
  dropped plateau.

- **Pushbacks no longer drive into the next stand, and they wait their turn.**
  Generated taxi-out polylines that hooked the wrong way along T4 (BAY-3 into
  50A, and the same kink on 50E/50F) are trimmed so the tug hands off toward
  the hold. Same-apron pushbacks now wait until the first aircraft has
  finished the push, disconnected, and cleared the stands — not a flat 60 s —
  so two airframes are not on the shared taxilane together. A 180° heading
  snap at tug disconnect stops the visual spin that looked like taxiing the
  wrong way.

- **The HUD stamp is live Adelaide date and time.** The top bar reads
  weekday, date and 24-hour clock from the same wall-clock alignment the
  simulation already uses, so a Monday morning session shows Monday.

- **Traffic follows the banks.** The published day and AI ready-times bunch
  on the 06–08 and 16–18 peaks (and a smaller midday bank). Overflights thin
  out overnight and in the afternoon hole.

- **Departures turn toward the destination.** After rotate, the climb-out
  yaws and drifts off the centreline toward the booked city instead of
  climbing forever along the runway axis.

- **The Operations board now shows the whole Adelaide day, and the sky is
  busier.** Arrivals and Departures list every planned movement from the
  official AI networks between 06:00 and 21:00, with live aircraft overlaying
  their own slots. Planned flights that are in the air right now are drawn
  inbound or outbound over the field (without taking a stand). Overflight
  corridors run more often so the overview is not an empty bowl.

- **Floating terminal blocks and apron floods are gone from the default
  field.** OSM terminal prisms and curtain-wall extras sat on runway Y above
  the dropped landform; apron-flood spots had no poles. They stay on the
  explicit `-airsideFullAirport` path. Runway edge, PAPI and HIAL stay.

- **Fix fuelling (and the other prep stages) never finishing.** The planner
  showed "in 5 min" from *now* every frame and clamped that delay to the prep
  lead, so the pushback clock walked forward and updating the plan restarted
  fuel. Booked remaining now counts down; an in-progress prep clock is kept;
  a missing prep-start is inferred from the booked slot. Stage remaining is
  shown counting down.

- **Mac player compiles again after #318.** `ContractsWorkspace` used `Math.Min`
  without `using System;`, so `scripts/build-mac.sh` died with CS0103. Also
  committed the Unity `.meta` sidecars for the new HUD/career files (they were
  generated on first Editor/batch import and never tracked) and the URP SSAO
  prefilter re-serialisation from that build. IMGUI panels now nine-slice their
  8px bevel (`PanelStyle.border`) instead of stretching the whole 128×128; Follow,
  Overview and Resume draw the approved Batch F4 system icons (leftover from #299).

- **Fuselage titles read as paint again.** The dark plate behind each airline name
  was sized in TextMesh character-size units instead of metres, so it came out
  6.4x too small — a small dark rectangle sitting across the middle of the name
  on every aircraft. It is gone: real titles are paint on the skin, not a panel.
  The registration is no longer near-black on a near-black plate, and a long
  airline name is now shrunk to fit the type's real fuselage length instead of
  running off the end of the aeroplane.

- **The HUD now draws the four workspaces from the shared painters (ADR 0057).**
  Operations, Map, Fleet and Contracts are rasterised from the same draw lists
  the headless tests and the offline mockups use, so what is reviewed is what is
  drawn. `C` opens Contracts. The Hangar's aircraft-types catalogue tab and the
  long-dead fleet roster sidebar are gone; purchase information moved to the
  Fleet workspace's market strip. `scripts/hud-mockup` plays a real headless
  airline and `scripts/render-hud-mockups.py` renders each page to PNG.

- **Contracts separate the commitment you made from the market (ADR 0057).**
  The active contract has its own column with real progress, per-rotation and
  completion payment, the cancellation reliability penalty and which of your
  registrations can actually fly it. The three rotating offers sit apart with
  their real totals and a countdown to the market refresh. A locked offer names
  the actual missing capability — a tier, an aircraft type you do not own, or an
  active contract. A fulfilled contract is not drawn at all, so it can never be
  clicked.

- **Fleet shows your aircraft first and a market that cannot lie (ADR 0057).**
  The roster lists the player's aircraft above the other operators; selecting one
  gives its route-band capability, rotations flown, planning range, current
  assignment and live turnaround — no invented maintenance, wear or upgrades. The
  aircraft market reads AircraftAcquisition for price, tier, reliability and
  rotation gates, names the first gate you have not met, and says whether the
  airframe would park on a free bay or ferry in. A purchase is only offered when
  AirlineOperations.BuyAircraft would accept it.

- **The Route Map prices a destination from the rules that charge you (ADR 0057).**
  Available and locked come from the selected aircraft's real range and route
  band, and the detail pane reads dispatch cost and estimated return straight
  from FlightEconomics. A locked destination says whether it is out of range or
  above the type's band. Plan flight is offered only when the simulation would
  accept the booking. Zoom, pan, aircraft tracking and planning are unchanged.

- **Operations is a real movement board (ADR 0057).** Departures and arrivals each
  sort by the TIME column they print instead of by a hidden next-event key, and
  carry flight number, registration, route, stand, status, type and operator.
  Your own airline reads at full contrast and other operators stay visible but
  subordinate. Player exceptions — a landing with no free bay, a departure
  running late — are pinned above the board; when nothing is wrong the band shows
  the next commitment instead. Selecting a flight gives its live turnaround and
  the one action that fits it.

- **One HUD draw list, one palette, one persistent shell (ADR 0057).** The top bar
  and the current-objective card are now described by UnityEngine-free painters
  that emit a shared draw list, which IMGUI rasterises at runtime. The card stays
  in the same place on every page instead of only on the overview, so the pages
  read as one screen; a window too narrow for both gives the width to the
  workspace. Colours come from one `AirsidePalette`, so the runtime HUD, the
  headless layout tests and the offline mockup renderer cannot drift apart.

- **Airline HUD shell matches the career overview reference (ADR 0053).** A slim
  navy top bar holds the airline, Adelaide time, funds, reliability, tier and
  workspaces. The current-objective card and compact player Operations sit on
  the overview; the selected-aircraft card shows one primary action plus Cancel.
  The mini-map is smaller. Follow / Overview left the airline overview (Esc/R
  and F remain). Domain, simulation, saves and economy are unchanged.

- **Departure prep now shows how far through fuel, catering and boarding you are.**
  Each stage keeps its own 0–100% bar (done / in progress / waiting). The
  follow card, fleet panel, Flights board and field tags all read the same
  simulation progress — presentation still does not decide when prep finishes.

- **Career fleet, rotating contracts, departure prep and auto-stand (ADR 0056).**
  Player types unlock route bands (ATR/Saab Regional, Dash 8 Domestic, 737
  National, A321 Tasman, widebodies long-haul) and those legs pay more. Hangar
  buy uses authored prices and gates; a free stand parks the new aircraft,
  otherwise it ferries inbound. Contracts are a 6-hour market of three offers
  drawn from owned types, not a fixed ladder. Player landings auto-take a stand;
  a booked departure runs fuel → catering → boarding and will not push until
  ready. Save schema 8 (prep start, market-contract snapshot, rotation count).

- **Living airport and first-playable career loop (ADR 0055).** Holding
  traffic now flies a visible right-hand circuit south of runway 05 instead
  of vanishing; a go-around flies the approach, aborts off short final and
  joins that circuit. The first cut of that go-around trapped the aircraft
  in a forever-circuit (`WentAroundThisTrip` also meant "this landing is a
  missed approach," so the real landing after the abort was sent around
  again); a missed approach is now only the short approach, and the following
  full landing still reaches a stand. New airlines start with $4,000; dispatch
  is charged when you book and refunded if you cancel before pushback; every
  completed rotation pays, and contracts (Kingscote, Port Lincoln, Whyalla,
  Melbourne) add a bonus. Completed contracts cannot be re-accepted; Kingscote
  or Port Lincoln unlock Regional, Melbourne is a Dash 8 Domestic goal.
  Other-airport corridors (PER–MEL, PER–SYD,
  DRW–MEL and the east-coast pairs) draw on the map always and in 3D when
  they pass within 260 km of Adelaide. Save schema 7.

- **Fixed a stale-time bug delaying Cathay Pacific's seasonal arrival by one `Update()`
  call at the season boundary.** `AirlineOperations.Update()` gated its Cathay-season
  backfill on `IsCathaySeason(target)` (the time being advanced *to*) but the callee,
  `AddMissingTerminalOperators()`, checked the season against `_processedTo` (the time
  being advanced *from*, not yet updated) — so on the exact `Update()` call that crossed
  into season, the backfill was skipped and only succeeded on the next call. Gave
  `AddMissingTerminalOperators` an explicit `at` time parameter; `Update()` now passes
  `target`. New regression test, mutation-tested (reverted the fix, confirmed it failed
  with the exact expected assertion, restored it, confirmed 320/320 again). Found by a
  targeted bug-hunting pass over career/settlement/scheduling logic — that same pass
  traced `AirlineCareerState` settlement idempotency and `NextEventAt`'s skip-to-
  next-event logic in detail and found both correct, already well covered by existing
  tests.

- **Static discharge wicks added to the Saab 340B and A350-900 wingtips** (the 787-10
  inherits them automatically since it derives from the A350-900's generator module).
  Every real airliner has these small trailing-edge antennas; previously only the 737-8
  (and its A321neo derivative) and the Dash 8-400 had them modelled, so half the fleet's
  wingtips read as slightly unfinished up close. Placed from each aircraft's own already
  -verified wingtip navigation-light position (a known-good anchor) rather than
  re-deriving wingtip geometry from scratch. `scripts/test-air-007-saab-340b.py` and
  `scripts/test-air-009-a350-900.py`/`-010-787-10.py` still pass; all three regenerated
  models were rendered and visually inspected (no bounds violation — the wicks sit safely
  inboard of each wing's existing extremity, confirmed against each file's own dimension
  tolerance before regenerating, not after).
- **Every aircraft's nacelles, wheels, landing gear, propeller hubs/spinners and radome/
  belly-fairing lofts are meaningfully rounder** — raised the Python geometry generators'
  segment counts on the smallest, closest-to-camera round parts across all 7 types
  (737-8, A321neo, A350-900, 787-10, ATR 42, Saab 340B, Dash 8-400), typically +25-60%
  per part (e.g. engine nacelles 40->56/48->64, wheels/tyres +33%, gear oleos +50%).
  Combines with the runtime mesh-smoothing fix (separate branch) — more segments gives
  that smoothing pass more geometry to work with, so the two changes compound. Triangle
  counts rose 10-25% per aircraft (e.g. 737-8 17,404->20,156; Saab 340B, the smallest,
  6,868->8,564 at +25%), all well within normal game-asset budgets. Left main fuselage
  segment counts alone (64-72, already high per prior research; further gains there are
  marginal/invisible at gameplay camera distance) and focused entirely on the parts that
  were still low. **Real, not just reviewed-by-inspection, verification:** every regenerated
  model passed its existing Python dimensional-envelope/part-inventory regression test
  (`scripts/test-air-*.py`, all 8 pass) and was actually rendered and visually inspected
  via `scripts/render-aircraft-thumbnails.py` (a real offline rasterizer, not a text
  description) before being called done — caught nothing wrong, all 7 read as correct,
  intact aircraft. `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`'s triangle counts updated
  to match.

- **The terminal's 28 airside glazing bays now read as a framed curtain wall, and the
  roof canopy has real volume and a corrugated-metal material.** Both were flat boxes
  before — the panes butted up against each other with nothing marking the 3 m gaps
  between them, and the roof brow was a 0.7 m-tall, untextured slab. New
  `AdelaideTerminalArchitecture.GlazingMullions()` places a dark structural mullion in
  every gap; the roof brow is thicker (0.7 m -> 1.1 m) and deeper (3 m -> 5.5 m) with a
  real `tx_corrugated_metal` material. The main shell prism's walls were deliberately
  **not** textured — checked `SurfaceMesh.Add`'s UV generation first
  (`AirsidePrototype.YpadPavement.cs`) and its world-planar `(x/9, z/9)` mapping is
  ground-plane-only; applying a tiled texture to a vertical wall face with that UV
  formula would smear rather than tile correctly, so it stays a flat colour rather than
  shipping something that would look visibly broken.

- **World rendering quality: real-scale reflection probe for the shipped Adelaide world,
  a finer ground mesh, and corrected SSAO settings.** The apron/terminal realtime
  reflection probes from Decision 0025 item 5 only ever existed on the legacy 1:20
  miniature circuit — the real Adelaide bare-field world (the shipped default) never got
  its own, so wet asphalt and the 28 terminal glazing bays picked up no local floodlight
  reflections at all. New `BuildBareApronReflectionProbe()` adds one real-scale,
  box-projected probe sized from `AdelaideTerminalArchitecture`'s actual coordinates,
  covering both the stand apron and the terminal glass. `AirsideAdelaideGround`'s vertex
  grid roughly doubles in linear density (High 225x161 -> 337x241, ~17 m -> ~12 m
  spacing; Medium now gets the old High values, so both tiers step up) for tighter
  pavement-shoulder blending, verified safe under `AirsideAdelaideGroundMesh`'s existing
  16-/32-bit index-format switch. Also corrected two SSAO settings in `PC_Renderer.asset`
  that were left at their non-default tier (`Samples` Medium->High, `NormalSamples`
  Medium->High) after checking the actual URP 17.3 enum values in the cached package
  source — the other settings researched as "low" (`BlurQuality`, `Downsample`) turned
  out to already be at their best value once checked against the real source, so were
  left alone rather than "corrected" on a wrong assumption.

- **Every procedurally-loaded model (aircraft, vehicles, buildings, props) now shades
  smoothly on continuously curved surfaces instead of reading as faceted/"triangular."**
  `ArtGltfLoader.ParseKit()`'s bare `Mesh.RecalculateNormals()` only smooths across faces
  sharing the exact same vertex index; most of this project's procedural geometry
  generators (fuselage tubes, nacelles, gear, wing slabs, fan blades) emit fresh, unshared
  vertices per quad, so even a 72-segment "round" fuselage rendered with a hard facet per
  quad regardless of segment count. New `MeshNormalSmoothing.cs` welds vertices by
  position and smooths by a 60° angle threshold instead (matching this project's own
  authored-FBX import convention), preserving genuine hard edges while smoothing the
  continuously curved surfaces that were faceted. Verified against a minimal UnityEngine
  API stub (3 tests, mutation-tested) since no Unity editor is available this session —
  not the same as compiling/running under the real engine; `scripts/test-unity.sh` and a
  close-up Play-mode look at a widebody fuselage/nacelle is the outstanding step.
- **Fuselage operator titles and registrations now dim/warm with the same day/night grade
  as the rest of the airframe, and sit on a small painted backing panel instead of
  reading as flat, unlit text floating in space.** The identity marks are legacy Unity
  `TextMesh` objects on the unlit built-in font shader, so they never received any of the
  aircraft's own lighting. `AircraftIdentitySideVisibility` (already touching every label
  once per aircraft per frame after the recent perf pass) now also tints each label's
  colour by the live daylight value, and a new subtle anti-glare-style backing plate sits
  behind each title/registration. Presentation-only, reviewed by inspection.

- **Individual terminal gates and regional bays are now lit at night**, not just the 7
  uniform terminal roof floods. Each stand gets a lit marker at its stop position and a
  short blue lead-in trail along the final ~32-40 m into the stand, matching the same
  emissive-lens + sparse-point-light pattern the runway/taxi lighting already uses.
  Also backfills a `GAME.md` entry for the ERSA-sourced runway/PAPI/approach-lighting
  system (`AirsidePrototype.YpadLighting.cs`) that shipped previously with no changelog
  trail. Presentation-only, reviewed by inspection — no Unity editor in this session.

- **Each jet now taxis a gate turn with its own wheelbase, not a shared 19 m constant.**
  `AdelaideGround.GateTaxiOut`/`GateTaxiIn` steered every jet's main gear through terminal
  turns using one flat `JetTrackMetres = 19f` figure (the 737's own nose-to-main-gear
  distance) regardless of type — so the A350-900 and 787-10 widebodies tracked corners as if
  they had a narrowbody's gear geometry. `AircraftPerformanceProfile` gained a sourced
  `NoseToMainGearMetres` per jet type (737-8 17.68 m, A321neo 16.90 m, A350-900 28.66 m,
  787-10 28.88 m — sources in `docs/data/AIRCRAFT_SPECIFICATIONS.md`); `AdelaideGround` now
  reads each type's own value. Also removed the confirmed-dead `AdelaideGround.ApronZone
  (StandClass)` overload (zero callers anywhere, including tests) and documented the
  previously-unlabelled A350-900/787-10 taxi-speed figures instead of letting them silently
  inherit the narrowbody-jet column. Evidence: `scripts/test-domain.sh` **320/320** (one new
  test, mutation-tested — reverted the fix, confirmed it fails, restored it, confirmed green).

- **Presentation CPU performance pass, no graphics/quality changes.** Six per-frame aircraft
  passes (control surfaces, lights/gear, cabin doors, cabin window glow, engine heat,
  propeller/jet-fan spin) each re-scanned and re-classified every named child on every
  visible aircraft by string, every frame — now classified once per aircraft view and cached,
  same pattern applied to the apron ground-crew animation. Each aircraft's 4 camera-facing
  identity labels (operator title + registration, both sides) ran independent `Camera.main`
  lookups and camera-side math every frame; now one shared, per-frame-cached lookup drives all
  4 from a single calculation per aircraft. The Flights board panel rescanned and re-sorted the
  whole fleet on every OnGUI pass instead of once per frame (same class of bug as the earlier
  Fleet/Hangar panel fix). Compiled clean in Unity 6.3.23f1 (928 scripts, no errors); EditMode
  suite not re-run this pass (see GAME.md handoff) — Presentation animation behaviour not yet
  visually verified in Play mode.

- **Make zoom and drag pan actually fast (ADR 0054 follow-up).** Scroll magnitude is not
  portable: the rate assumed one wheel notch is ~120 units, but macOS reports single digits,
  so a real notch was worth 0.24-0.7 % and it took 500-1500 notches to get from the 2400 m
  overview to the apron. Scroll samples are normalised to notches first and the rate is per
  notch (~39 % closer), making it 8 notches on any platform, with the easing landing in
  0.15 s. Drag pan now tracks pointer positions through the camera's own ray instead of
  reconstructing them from `mouse.delta`, whose scale can differ from `mouse.position` on a
  Retina display and silently halved the drag. Keyboard pan at the overview: 220 -> 650 m/s.

- **Fix the known 320×240 airline HUD overlaps.** Bottom-band toast no longer sits on
  the map/fleet, and the left column (guide/nav) reserves space so stacked fleet/map
  rects stay above the speed readout on tiny windows. `AirlineHudLayout_PanelsFitAndNeverOverlap`
  now asserts toast≠map and toast≠fleet at every target size including 320×240.

- **Field camera zoom and navigation mechanics (ADR 0054).** Free-camera scroll
  zooms toward the ground under the pointer; left/middle drag grabs that ground
  the same way the destinations map does; WASD drops follow then pans; soft pan
  limit keeps the orbit centre within ~3.8 km of the overview. Follow-mode zoom
  bias and #278 rates are unchanged. `scripts/test-domain.sh`: 316/316.

- **5 more fixes: 2 latent lookup bugs, a wrong dead constant, and 2 real perf/behaviour
  bugs.** `AircraftType.TryFromId`/`AircraftCatalogue.TryFor` never returned on a match and
  silently kept the *last* one instead of the first — harmless while catalogue ids are
  unique, now fixed and locked in with tests before it isn't. `AirportLocation`'s UTC offset
  was a flat `9` for every South Australian airport (real ACST is +9:30, and the field was an
  `int` so it couldn't even represent that) — confirmed dead/unread elsewhere, fixed to
  `9.5f`. The gate-servicing GSE team could get permanently stuck on one terminal aircraft
  while every other jet on the gates got no service vehicles at all; now rotates between
  every currently-parked terminal aircraft. The always-visible Fleet sidebar and the Hangar
  panel each scanned the whole fleet twice per airline per frame (once for content height,
  again to draw); both now group the fleet once and reuse it. `scripts/test-domain.sh`:
  304/304, up from 302.

- **Bring `RouteMap.cs`'s great-circle/zoom math into the headless test harness.**
  UnityEngine-free like its neighbour `AustraliaMapLens.cs` (already covered) but excluded
  from `scripts/dotnet-harness` along with its 3-test suite, apparently only because it
  hadn't been added to the harness's compile list. `RouteMapTests.cs` now runs headlessly.

- **Fix a compile-breaking bug on `main` plus 9 more performance, realism, logic and taxi
  fixes.** The workspace nav strip had a tuple-arity mismatch (`WorkspaceTabs[i]`
  destructured into 3 variables from a 2-element array) left over from an earlier commit
  this session that trimmed the array but missed the call site — the project would not
  compile. Also: sister aircraft went around in lockstep because the go-around "random" seed
  hashed registration *length* (identical for every "VH-XXX" rego) instead of the
  registration itself; 3 of 7 jets (A321neo, A350, 787-10) planned cruise altitude using the
  turboprop formula instead of the jet one; the flight planner's departure/return-time
  preview used an ATR 42's taxi and takeoff timing for every aircraft type; a bay pushback
  and a gate pushback could delay each other despite sharing no pavement; wake-turbulence
  separation was reclassified to derive from the catalogue's own wingspan data instead of a
  second, disconnected type-ID list; the "quickest stand" ranking now accepts an aircraft
  type instead of always assuming an ATR 42; and two allocation/O(n²) spots in the fleet
  render path (a per-click list allocation, an O(n²) livery-slot scan) now reuse fields.
  14 new tests, all mutation-tested where the fix is Simulation/Domain-testable;
  `FlightPlanner.cs` and its 10-test suite now run in the headless harness for the first
  time. `scripts/test-domain.sh`: 299/299, up from 285.

- **Keep modal screens modal.** The airline setup and away-summary screens no longer leave
  the live speed readout or Follow / Overview buttons visible and clickable underneath them.
  Opening the Escape menu also hides the underlying airline panels, preventing the setup form
  and menu labels/buttons from stacking while retaining full-screen camera-input capture.

- **Label the speed/altitude readout with the aircraft it's for.** The HUD's persistent
  speed/altitude box always showed a reading (the followed aircraft, else the first visible
  one) with no indication of whose it was — harmless with the old single-aircraft demo
  circuit, but with an airline running and AI traffic sharing the field, the box could show
  a competitor's speed with nothing to say so. Now prefixed with the aircraft's flight number
  (or registration), same as the map and flight board already show.

- **Fix the landing camera "stutter" and a touchdown-smoke speed bug.** Following your own
  aircraft in would silently drop the camera follow the instant it entered its Approach visual
  phase — well before the runway — because the far-approach pick-distance filter was also
  gating whether an already-followed aircraft stayed a valid follow target. Fixed so an active
  follow is never dropped by that filter (new-follow/cycling candidates are still filtered as
  before). Also fixed the touchdown wheel-smoke effect using the ATR 42's speed curve for every
  aircraft type regardless of what actually landed — Boeing/Airbus touchdowns now use their own
  speeds like every other render-path call site already did. Audited all 7 aircraft types'
  derived circuit timings (approach/landing/takeoff seconds, rotate/touchdown/flare fractions):
  all sane and correctly derived, no accuracy issues found in the underlying numbers.

- **Bug-hunting pass: 6 fixes across the codebase.** Fixed the away-summary misreporting a
  save migration (v5→v6) as reliability/funds change that happened while away; deduplicated
  `FlightNumber`'s hash function to reuse the canonical `StableNameHash`; fixed the Map nav tab
  dead-clicking into an empty planner on first use instead of routing through
  `TogglePlanner`; fixed a style-cache collision between the intro title and its fallback mark;
  removed a dead hotkey field and corrected a stale comment. New regression test for the
  away-summary fix; 285/285 passing under `scripts/test-domain.sh`.

- **Theme every button in the HUD.** Every button in the game used Unity's stock grey
  `GUI.skin.button` background with only its text colour ever touched — no button background
  in the entire codebase was themed. New `AirsideTheme.ButtonStyle` (Tarmac at rest, Coastal
  Blue on hover/press, matching the palette's existing selection colour) is applied at the two
  places in the whole game that construct a button style, which every other button derives
  from — themes essentially the entire HUD in one change. Also gave the persistent status line
  a panel background to match the guide card it replaces, instead of floating as bare text.

- **Fix nav strip text overflowing the window.** The workspace nav strip was locked to the
  300 px clock column's width, giving four tabs ~75 px each — nowhere near enough for
  "Operations", which overflowed clean off the left edge of the window (confirmed from a
  screenshot). It now sizes itself using the same room the destinations map already gets,
  capped at 420 px; tab labels dropped their hotkey suffixes. Also replaced the destination-map
  marker glyph (a ring with crossed runway bars) with a plain antialiased dot — the original
  risked reading as a target/"no entry" symbol at the small size it actually draws at. New
  regression test locks in a minimum tab width at all 6 tested resolutions.

- **Reliability cost for cancelling a contract flight.** Cancelling a scheduled departure that
  would have counted towards the active career contract now costs reliability (closes the
  `CancelDeparture` TODO from #291); cancelling anything unrelated to the active contract
  still costs nothing. 2 new tests; 284/284 passing under `scripts/test-domain.sh`.

- **New Airside app identity and opening.** The app now uses an original, unbranded
  approach-runway mark: deep Runway Ink, coastal-blue rails, a Cloud centreline and three
  safety-yellow approach lights. Launch is a 4.8-second runway-signal sequence rather than a
  long distant fly-over: the mark leads, local operations information settles in, then the
  ink wash gives way to the playable overview. Any key or click still enters immediately.

- **Wire the airline career into the HUD (Task 3, ADR 0053).** The Contracts workspace shows
  real terms and an Accept button, then a progress card once accepted; the clock panel shows
  funds and reliability; the objective line shows contract progress; completing a rotation
  shows a toast (a distinct one on contract completion); the away summary reports
  funds/reliability earned while gone. All read-only against the Task 2 domain layer — no new
  simulation logic. 1 new test (`AwayCatchUpTests`); 282/282 passing under
  `scripts/test-domain.sh`.

- **Airline career domain and save v6 (Task 2, ADR 0053).** New deterministic career state —
  funds, reliability, operating tier, an acceptable/accepted route contract — plus a real
  `AcceptContract` command and one authored contract (`REG-KGC-INTRO`, Adelaide↔Kingscote).
  Completing an eligible player rotation settles it against the active contract exactly once
  (`SettlementId` = registration + trip number, guarded against repeats); save schema moves to
  v6, and a pre-6 save migrates to a fresh Provisional career without retroactively paying
  historical trips or losing any existing fleet/schedule/stand data. Domain/Simulation and
  tests only — no HUD wiring yet (Task 3). 7 new tests; 281/281 passing under
  `scripts/test-domain.sh` (the .NET SDK was installed this session specifically to run it).

- **Persistent status/objective line.** Once the first-flight guide finishes, its card slot
  under the clock now shows a quiet one-line objective instead of disappearing: the most urgent
  player aircraft and what it needs, severity-tinted, or a calm fleet-wide line when nothing
  needs attention. New `OperationsSummary` pure helper; `AirlineHudLayout`'s guide/status region
  is never zero-sized any more. Presentation-only.

- **Flight numbers and airport-glyph map markers.** Aircraft now read by a deterministic
  flight number (airline code + a stable 3-digit number derived from registration and route)
  in the map's in-flight labels, the Flights board and floating field tags, instead of a bare
  registration — the map's detail line and the Flights board still show registration and type
  alongside it. Destinations on the Australia map, including Adelaide, draw as a small airport
  glyph (ring + crossed runway bars) rather than a plain square, growing gently with zoom.
  Presentation-only, deterministic, nothing stored: new `FlightNumber` pure helper with tests.

- **HUD shell cleanup, Task 1 (ADR 0053).** The four independent booleans behind the
  Plan/Hangar/Flights panels are one `_activeWorkspace` field, and a new nav strip under the
  clock replaces the three ad hoc buttons with the four ADR 0053 workspaces (Operations, Map,
  Fleet, Contracts — Contracts is a placeholder until Task 2/3 add career state). Existing
  hotkeys (Tab/H/T), panel content and behaviour are unchanged; the mini-map no longer risks
  sitting under the new strip on short windows. Dev Tools gets a red frame + "DEV" badge so it
  reads as a diagnostic overlay, not a player workspace. Presentation-only: no simulation, save,
  route or traffic change. `PresentationLayoutTests` extended for the new nav-strip rect at the
  existing 6 resolutions; Unity EditMode, a packaged build and the 1280x720/1440x900/Retina
  visual pass are still open (no Unity editor in this session).

- **Singapore Airlines 787-10.** Singapore Airlines' Gate 20 rotation now uses its own genuine
  AIR-010 Boeing 787-10 instead of borrowing the A350. The 227-part, true-scale model has the
  787-10's narrower 5.77 m cabin, four-pane Boeing flight deck, swept/raked wing, chevron
  nacelles and ten-wheel gear inside the official 68.30 × 60.12 × 17.02 m envelope. Catalogue,
  performance, heavy wake spacing, Hangar thumbnail and save-safe type lookup are type-specific.
  Existing v5 saves migrate the temporary `9V-SMA` A350 entry without losing its operational
  state. Headless tests: 274/274; Unity EditMode: 492/492; packaged Mac build passed.

- **Genuine A350-900 silhouette.** AIR-009 is no longer a uniformly stretched 737. Its
  purpose-built 229-part model now has a 5.96 m widebody cabin, long tapered nose, dark
  wraparound flight-deck mask, strongly swept high-aspect-ratio wing with raked tips, large
  18-blade turbofans and the A350-900's ten-wheel undercarriage. The exact 66.80 × 64.75 ×
  17.05 m envelope, schedules, routes and saves are unchanged; the Hangar thumbnail was
  regenerated from the runtime model. Headless tests: 274/274; Unity EditMode: 490/490.

- **Adelaide night readability.** The focused real-airport world now keeps its terminal window
  glow and seven roof-mounted apron floods instead of discarding all decorative lighting. A
  restrained cool night key, fill and trilight lift preserves aircraft, terminal and pavement
  silhouettes at 23:30; separated interior cards warm the airside glazing only after dusk, while
  daytime glass remains blue. Headless tests: 252 passed; packaged day/night captures passed;
  Unity EditMode 448/449 with only the existing Gate 13 tolerance miss.

- **Adelaide terminal facade.** The real OSM terminal shell now has a segmented glass airside
  frontage, projecting roof brow, skylight strips and rooftop plant instead of reading as one
  blank block. The shell uses a restrained neutral finish; the detail is presentation-only and
  does not affect routes, collision or saves. Repeatable packaged-build review shots can now set
  their overview centre as well as yaw, pitch and distance. Headless tests: 251 passed; packaged
  day/night captures passed; Unity EditMode 447/448 with only the existing Gate 13 tolerance miss.

- **ATR high-wing hierarchy.** Player and Emu Air ATRs now keep airline colour on the fin and
  compact wingtips while the broad high wing, moving surfaces and horizontal tail use a neutral
  finish. Darker glazing remains readable across both white and generated player liveries.
  Geometry, scale, six-blade prop rig, routes and saves are unchanged. Headless tests: 249 passed;
  packaged daylight capture passed; Unity EditMode 445/446 with only the existing Gate 13 miss.

- **Q400 high-wing hierarchy.** QantasLink's Q400 now keeps operator red on its fin and compact
  tip devices while its high wing, moving wing surfaces and horizontal tail use a neutral
  painted-metal finish. Darker glazing strengthens the long regional-turboprop read. Geometry,
  scale, propeller rig, routes and saves are unchanged. Headless tests: 249 passed; packaged
  daylight capture passed; Unity EditMode 444/445 with only the existing Gate 13 tolerance miss.

- **737 livery hierarchy.** Wattlebird Jet's 737 now reserves its blue accent for the fin and
  split winglets. Wings, moving wing surfaces and the horizontal tail use a neutral painted-metal
  finish, with darker glazing for better narrowbody readability. Geometry, scale, fan animation,
  Gate 13 routing and saves are unchanged. Headless tests: 249 passed; packaged daylight capture
  passed; Unity EditMode 444/445 with only the existing Gate 13 tolerance miss.

- **Saab 340 visual hierarchy.** Rex's Saab now uses restrained neutral wings and horizontal
  tail surfaces, keeping airline colour on the vertical tail instead of turning the full
  silhouette into a bright colour block. Cabin and cockpit glazing is darker and slightly more
  prominent at follow-camera distance while the exact AIR-007 dimensions, 120-part mesh budget,
  propeller rig, routes and saves remain unchanged. Headless tests: 249 passed; packaged Mac
  daylight and 23:30 captures passed; Unity EditMode 444/445 with only the existing Gate 13
  tolerance miss.

- **Taxiway edge weathering.** Adelaide taxiways now have sparse, warm-grey dust/scuff strips
  just inside both pavement edges, breaking up the perfectly cut black ribbons without reading as
  paint or changing pavement geometry. The combined mesh is deterministic, presentation-only and
  naturally disappears at overview distance and at night. Headless tests: 249 passed.

- **Adelaide apron slab structure.** Concrete aprons now carry a restrained 18 m expansion-joint
  grid clipped to the real OSM polygon boundaries, so large paved areas read as built slabs rather
  than flat sheets. The combined detail mesh is matte, deterministic and presentation-only;
  routes, stands, collisions and saves are unchanged. Packaged Mac captures passed at noon,
  golden hour and night in overview/follow views. A review-only `-airsideReviewTime HH:mm` launch
  flag makes lighting checks repeatable without changing the simulation clock. Headless tests:
  246 passed.

- **Verified taxi speeds.** Straight taxi rises from 15 kt to the published operating
  bands (turboprop/jet ~25 kt on long taxiways, ~10 kt turns, apron 15/10 kt, 5 kt stand
  lead-in, 3 kt pushback, 10 kt lineup). Sources recorded in
  `docs/data/AIRCRAFT_SPECIFICATIONS.md` and ADR 0045. Domain tests 242 passed after rebase onto #278.

- **Camera feel + taxi props.** Scroll zoom and drag pan are snappier on the real-metre
  overview (one mouse notch ~25 % closer; wider trackpad zoom queue; faster drag pan
  and orbit). Turboprop and jet-fan blur discs engage at taxi RPM so blades no longer
  strobe while taxiing. Headless `CameraFeelTests` lock the rates.

- **Bug sweep 11 (10 fixes): known issues cleared, live-world details.** The painted stand
  identifiers are depth-tested, so they no longer show through a parked aircraft, and the fallback
  windsock lies along the wind instead of standing upright. The hangar door only opens for aircraft
  actually near the hangar, so it closes at night again. The touchdown cue no longer drags the
  prototype transform (and the tyre-smoke pool) across the field on every landing. Resuming a save
  with no fleet array works, the unattended soak survives an aircraft that can reach nothing, and
  the quality ladder stops editing the tracked URP asset in the Editor. The fallback ambience beds
  loop without a click, and missing art textures are remembered rather than re-read from disk every
  time. Unity EditMode 430/430.

- **Bug sweep 10 (8 fixes): loading, saves, tooling, hidden aircraft.** Combined-kit cache keys are
  built with the invariant culture, so a comma-decimal locale can no longer give two kits the same
  key and the wrong materials. A corrupt save timestamp falls back to the default clock instead of
  throwing from the start screen every frame. The build, terrain-bake and test scripts now say why
  they failed and name the failing tests instead of aborting silently. Aircraft away on a leg no
  longer show a field tag over empty tarmac. The scene index drops destroyed objects rather than
  throwing, and a fleet view's cached child list is refreshed when it gains children. Unity
  EditMode 427/427.

- **Bug sweep 9 (10 fixes): mini-map, camera feel, weather, sky.** The mini-map no longer keeps a
  press that never saw its release (which turned later field drags into camera jumps), and your
  own aircraft and the selection draw on top of other operators' dots. M says on screen that it
  muted the sound, and hotkeys no longer open the hangar or planner behind the controls help.
  Weather gloom eases in over about ten seconds instead of snapping the sun, ambient and grade in
  one frame. Rain falls around the camera rather than only at the world origin. The seagull flock
  keeps its orbit spacing instead of jittering and bunching. Stars fade out at dawn instead of
  blinking off, the coastal plain takes soft shadows like the airfield, and the hangar screen
  stops allocating lists every GUI event. Unity EditMode 424/424.

- **Bug sweep 8 (10 fixes): materials, grading, camera, editor.** Water is drawn as translucent
  water instead of opaque wet asphalt. Translucent colours on authored templates now render
  transparent. Tyre smoke, skid marks and engine heat are no longer drawn as glass. MAT-001
  templates build from the newest reviewed maps. Noon contrast and golden-hour bloom fade in
  instead of popping as the live clock crosses a threshold. Follow camera: pitch no longer fights
  a right-drag, and follow ends when the aircraft view is hidden. Keyboard pan and lift speed
  scale with zoom, and the orbit centre height is bounded. The editor only auto-opens the
  prototype scene once per session. Unity EditMode 421/421.

- **Bug sweep 7 (6 fixes): menu input, camera drags, per-frame cost.** With the Esc menu open, the
  camera no longer pans or orbits behind it, from the keyboard or from drags and scrolls beside
  it, and Esc closes the menu before touching the selection. Right- and middle-drags only move
  the camera when they start on the field, not over HUD panels. Per-frame work drops: fan and
  elevator presence are cached per aircraft, and each fleet aircraft's current ground pose is
  computed once per frame. Unity EditMode 417/417.

- **Bug sweep 6 (5 fixes): fence gates, runway crossing, sleep hitch, tooling.** The Adelaide
  perimeter fence now has openings at its three vehicle gates (panels used to run straight
  through them). Runway 12/30 no longer z-fights 05/23 at the crossing, and its markings stop at
  the main runway edge instead of being painted across it. Airline mode no longer re-flies hours
  of the hidden demo circuit after the Mac wakes. `test-unity.sh` fails rather than showing a
  stale results file, and the art sync reports real counts and removes orphan metas. Unity
  EditMode 417/417.

- **Bug sweep 5 (8 fixes): terrain at source, fleet lamps, sky.** The Kingscote terrain layers are
  now matte at source: the baker writes each layer's authored smoothness into the mask remap,
  and the baked assets carry it. Parked and taxiing fleet aircraft no longer burn landing lights
  (shadowed spot lights) or show flap all day. Taxi lights stay off on cold aircraft at night.
  Full-airport clouds no longer turn opaque as their alpha compounded, the moon sits on the
  camera sky sphere, the hangar bay light keeps its daylight tint, and coast water no longer
  allocates names per frame. Unity EditMode 416/416.

- **Bug sweep 4 (8 fixes): props, shadows, buildings, roads, tools.** Rex Saab 340 and QantasLink
  Dash 8-400 propellers no longer vanish at takeoff and approach power (no blur disc was built).
  Aircraft shadows on the bare-field grass no longer break along shadow cascade splits (the
  ground shader sampled shadows per vertex). Concave YPAD terminal and RFDS walls face outward
  instead of leaving 15 holes. Coastal road ribbons follow the ground instead of floating up to
  3 m above the beach. The F8 Dev Tools list no longer throws once an aircraft departs. Pick
  volumes move as kinematic bodies and sync before a click. Missing prefab keys are remembered,
  and the engine audio clip is built once. Unity EditMode 415/415.

- **Bug sweep 3 (10 fixes): live lighting, foliage, emission, perf.** The field is now lit by the
  real Adelaide clock the HUD shows. It used to start at 08:00 on every launch, so evening
  sessions were lit as morning and night rarely appeared. Tree crowns on the Resources prefabs
  no longer render as translucent blue glass (the binder treated "canopy" as glass). Kit trunks
  no longer take the grass material, and bark and rock are matte instead of painted metal.
  Landing lamps now glow (their emission keyword was never enabled). Hidden fleet aircraft skip
  part animation; shadow/marker/profile lookups are cached per aircraft; shared emission
  keywords are no longer rewritten every frame. Map clicks ignore culled dots, and
  -airsideSoakMinutes parses with the invariant culture. Unity EditMode 414/414.

- **Bug sweep 2 (12 fixes): camera, fleet queue, HUD, saves.** Two aircraft holding short or
  waiting for a stand are now queued 60 m apart instead of drawn inside each other. The follow
  camera releases a destroyed target, no longer hard-cuts on a frame hitch, and F follows the
  selected aircraft (as the controls sheet says) or the first visible one. Stand buttons wrap
  inside the fleet panel; the map tooltip stays on the map; field tags no longer steal clicks
  from panel buttons; the Esc panel says "Menu" and hides the no-op restart in airline mode;
  both joiner toasts show on an old save. Saves with a non-name fleet state are rejected, and
  ground-path sampling can no longer produce NaN. Unity EditMode 406/406.

- **Bug sweep 1 (14 fixes): materials, lights, allocations.** Full-airport props no longer read
  near-black: the painted-metal catch-all (bark, rocks, benches, unnamed kit parts) was borrowing
  bare corrugated metal at 55% metallic. Textured blocks now keep their own albedo instead of the
  authored template's (runway shoulders were corrugated metal), cables/cabinets/tail-lights/
  mudflaps/terminal parts no longer take aircraft paint, and clear-weather damp no longer paints
  every paved slab with wet concrete. The 737's fan spool no longer fights the prop spool;
  touchdown/rotate cues no longer dereference hidden aircraft; tyre smoke sits on each type's
  tyre radius. Daytime decorative lights switch off, per-frame name reads are cached, night-glow
  collection is linear, and runtime textures drop their CPU copy. Unity EditMode 401/401.

- **Diagnostic full-airport view no longer blown out.** Under `-airsideFullAirport` the legacy
  terrain was clipped white: its mask maps made the grass three to four times too glossy and its
  untinted textures too bright for the noon lighting. The terrain now gets runtime layer copies
  with matte smoothness and calibrated albedo, and reads as grass. The default bare circuit is
  unchanged (packaged noon capture within 2 levels). 389/389 EditMode.

- **Aircraft motion and ground-read polish.** The 737-8 now has independently
  spooling, articulated turbofan faces with a restrained high-power intake blur,
  rather than static nacelles while the turboprops animate. Each aircraft type now
  uses the wheel radius authored into its own kit, keeping 737, Q400, Saab and ATR
  ground-roll speed visually proportional. Presentation only: aircraft routes,
  phases, schedules, saves and the established unbranded art direction are unchanged.
  Unity EditMode 385/385; all four deterministic aircraft-kit checks pass.

- **Graphics/performance audit fixes.** The focused bare circuit remains the release default,
  but full-airport QA is now explicitly launchable with `-airsideFullAirport` and gets the
  correct miniature framing instead of an effectively empty 3.1 km view. Startup no longer
  requests the apron reflection cubemap twice on its first frame. Retired scene switches and
  daylight/fence capture paths are runtime flags, so the project compiles without unreachable
  branch warnings. Unity EditMode 383/383; packaged default and diagnostic full-world captures
  completed. The legacy full-world terrain/material exposure remains diagnostic-only and is not
  release-ready.

- **Bug hunt: follow camera, regional backfill, save restore.** Following an aircraft that leaves
  the field no longer snaps onto someone else. Continue can finish adding Rex/QantasLink aircraft
  on a later load and uses the same 50D/50E stand preference as live choice. A mid-trip save with
  no destination is rejected. Headless `scripts/test-domain.sh` compiles again (Unity-only tests
  excluded). 226 domain tests.

- **Dash 8-400 nacelle-bay polish.** AIR-006 keeps its long high-wing Q400 silhouette, but the
  former blocky rear nacelle fairings are rounded gear-bay continuations and the open main doors
  are thinner. Scale, propellers, type profile and QantasLink operation remain unchanged. 182
  named meshes / 27,288 triangles; deterministic geometry regression green. Unity EditMode and
  packaged camera QA remain open.

- **ATR 42-600 close-view polish.** AIR-001’s flight-deck panes now follow the curved nose rather
  than flattening it, and its widened tailplane saddle better joins the T-tail to the fin. The
  existing v03 scale, six props, fuselage-side gear and player/Emu Air presentation remain
  unchanged. 183 named meshes / 19,704 triangles; geometry regression green. Unity EditMode and
  packaged camera QA remain open.

- **Saab 340B close-view polish.** AIR-007’s four flight-deck panes now follow its rounded nose
  instead of projecting as a dark box; the nacelle gear bays are curved continuations rather
  than blocks, and the hubs/spinners are scaled down. The low-wing Saab silhouette, true-scale
  footprint and existing Rex operation remain unchanged. 120 named meshes / 6,868 triangles;
  deterministic geometry regression green. Unity EditMode and packaged camera QA remain open.

- **737-8 close-view polish.** AIR-005 keeps its real-scale footprint and operational loop, but
  replaces the dark projecting cockpit mask with a skin-coloured flight-deck crown and three
  compact fitted panes, shortens and tapers the wing-body keel fairing, and reduces the split
  winglets to a believable overview proportion. The Hangar thumbnail is regenerated from the
  shipped glTF. 199 named meshes / 17,404 triangles; deterministic geometry regression green.
  Unity EditMode and packaged camera QA remain open.

- **Fairer runway and smarter stands.** A departure held short for six minutes now gets the
  runway ahead of newer arrivals, and aircraft (and the player's Best stand button) avoid
  parking beside a Dash 8-400 on the tight 50D/50E pair when another bay is free (ADR 0052).
  376/376 EditMode.

- **Less per-frame garbage.** Aircraft part animation reads cached names, and the fleet view sync
  and follow-target refresh stop allocating every frame, cutting GC pressure that grows with
  every detailed aircraft on the field. 370/370 EditMode.

- **Better-looking ground and runway.** Grass gets large natural light and dark patches and
  (High quality) a second texture scale so the tile grid no longer shows from the overview;
  the ground edge is lit like its neighbours; runway rubber is streaks on the gear tracks
  instead of four black slabs. 369/369 EditMode.

- **YPAD surroundings P3 — OSM land cover + arterial roads.** Overview land past the airfield is no longer noise-fake: a 50 m class grid (±6.5 km) painted from real OSM landuse / leisure / water / parking polygons tints the surroundings heightfield (suburbs, parks, car parks, sand, scrub), inland water such as the Patawalonga sits at its own level, and motorway–secondary ribbons draw as asphalt strips. Coast (P2) and pavement unchanged. Snapshot `docs/data/osm/ypad-landcover-2026-09-15.json`; generator `scripts/generate-ypad-landcover.py`. Domain EditMode land-cover tests green; Unity EditMode / packaged overview QA pending Mac.

# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- **Airfield mini-map.** A corner map of Adelaide shows every aircraft on the field and what the
  camera is looking at; click an aircraft to select it, click or drag to fly the camera there,
  N to hide. 356/356 EditMode.

- **ATR 42-600 visual fidelity pass (v03).** AIR-001 gains a new `mdl_atr42_starter_v03` kit
  while v02 stays the approved fallback: four clean fitted cockpit panes with credible pillars,
  even Hangar-readable cabin windows, a smoother blunt nose into the cabin, compact nacelles
  blended into the high wing, clear fuselage-side main-gear sponsons (not Q400 nacelle gear),
  tighter wing-root / fin / T-tail joins, and six readable 3.93 m props with connected hubs.
  Same 22.67 × 24.57 × 7.59 m envelope, centred root, tyre contact and moving-part names;
  catalogue/runtime prefer v03 → v02 → v01. Hangar thumbnail regenerated from v03. 183 meshes /
  19,704 tris. No simulation or save change. Generator validate + multi-angle review renders;
  Unity EditMode pending Mac (`scripts/test-unity.sh`).

- **737-8 looks like a 737.** AIR-005 rebuilt in place: slender fuselage, fitted cabin windows,
  pitched flight-deck panes, low swept wing with dual-feather winglets, large forward-hung
  turbofans with chevron nozzles, deep wing-body fairing, joined conventional tail. Same
  39.47 × 35.92 × 12.42 m envelope and asset path; Hangar thumbnail regenerated. 204 meshes /
  17,464 tris (was 180 / 6,988). No simulation or save change. Generator validate + offline
  mesh review; Unity EditMode / packaged QA still open.
- **Dash 8-400 visual quality pass.** AIR-006 tightened in place: continuous wing-root
  saddle, single aerodynamic nacelle with gear bay, framed four-pane flight deck, pitched
  six-blade props with readable hubs/spinners, longer nacelle-mounted gear and doors, soft
  fin-root fillet and rounded T-tail saddle, even cabin windows. Same 32.83 × 28.42 × 8.34 m
  envelope and asset path; Hangar thumbnail regenerated. 182 meshes / 26752 tris. No
  simulation or save change. Generator validate + `work/review/` orthographic renders;
  Unity EditMode re-run pending Mac (`scripts/test-unity.sh` unavailable here).

- **Dash 8-400 looks like a Dash 8.** AIR-006 rebuilt in place: slender fuselage, fitted cabin
  windows, pitched flight-deck panes, continuous nacelles with gear bays, six-blade props, high
  wing with tip fences and root saddles, joined T-tail. Same 32.83 × 28.42 × 8.34 m envelope and
  asset path; Hangar thumbnail regenerated. 174 meshes / 18,856 tris (was 139 / 6,728). No
  simulation or save change. Domain pre-check + offline mesh review; Unity EditMode / packaged
  QA still open.

- **Statuses that flag trouble.** Long runway or circuit holds turn yellow then red, a stand you
  need to choose is red, waits show how long, and messages stack instead of overwriting each
  other, with the last ten kept on the Flights board. 343/343 EditMode.

- **Clicks land where you aim.** Aircraft tags and the toast are part of the HUD, so clicking or
  dragging on them no longer selects or pans the field behind; clicking empty ground deselects.
  337/337 EditMode.

- **Genuine Saab 340B.** Rex's existing aircraft now use their own original, unbranded
  AIR-007 model rather than the ATR stand-in: exact 19.73 × 21.44 × 6.97 m scale (standard
  wing), compact low wing, four-blade propellers, nacelle-mounted twin main gear and a
  conventional tail. The Hangar type card has a thumbnail rendered from that runtime model,
  and selection, shadow and follow-camera framing use the Saab footprint. No flight plan,
  stand, timing, reservation or save data changed. Domain harness green; Unity EditMode
  not run here (no Mac editor). Not rebuilt or manually launched.

- **Honest parked-clearance test and aircraft dispatch tests.** The regional wingtip test now
  measures parked outlines from the runtime models (it had assumed the stop was the nose) and
  covers the Dash 8-400; it records the one known shortfall — a Q400 beside a turboprop on
  50D/50E is 3.3–3.5 m apart, under the 4.5 m code C clearance — pending a stand-rule decision.
  New tests prove each type is built from its own model and QantasLink flies the Dash 8-400.
  331/331 EditMode.

- **Genuine Dash 8-400.** QantasLink's existing aircraft now uses its own original,
  unbranded AIR-006 model rather than the ATR stand-in: exact 32.83 × 28.42 × 8.34 m
  scale, long high wing, six-blade propellers, nacelle-mounted main gear and T-tail.
  Its Hangar type card has a thumbnail rendered from that runtime model, and selection,
  shadow and follow-camera framing use the Q400 footprint. No flight plan, stand,
  timing, reservation or save data changed. 328/328 EditMode; not rebuilt or manually launched.

- **Aircraft catalogue and Hangar types.** One cited catalogue holds every type's dimensions,
  cruise, planning range and stand class. The Hangar gains an *Aircraft types* tab with
  thumbnails rendered from the runtime models (ATR 42-600, 737-8) and clearly labelled
  placeholders for the Saab 340B and Dash 8-400. Your aircraft now carry a livery accent and
  "YOURS" badge, with other operators quieter, across the Hangar, fleet panel, Flights board,
  route map, field tags and selection card. Dash 8-400 planning range corrected to 1,500 km.
  327/327 EditMode; not rebuilt.

- **Handoff corrected.** GAME.md records the Gate 13 737-8 work as merged (#246) rather than
  on its deleted feature branch.

- **Gate 13 737-8 now operates.** The parked preview is one real AI aircraft: fictional
  Wattlebird Jet's VH-WTJ taxis in nose first to Gate 13, parks, pushes back tail first onto
  T1, taxis out and departs on a Melbourne/Sydney/Brisbane/Perth/Canberra rotation. Gate 13 is
  its own terminal stand system (never a regional bay), with a paved apron link, gate and
  lead-in reservations and the normal runway sequencing. It is selectable and followable with
  its registration, airline, type and live state. Existing saves gain it once. No save-schema
  change; regional traffic unchanged. 319/319 EditMode; not rebuilt.

- **First terminal jet: AIR-005 737-8.** An original unbranded, true-scale 737-8-class
  model is parked at Adelaide Gate 13 and can be clicked/followed with correctly sized
  camera framing, shadow, selection marker, identity card and hit volume. Gate 13 is a presentation
  anchor only: no fake taxi/pushback route or regional-bay reservation was added.
  305/305 EditMode; no app rebuild or manual playtest.

- **Handoff note.** GAME.md now opens with the current state, owner preferences and open
  follow-ups for the next contributor.

- **More regional traffic: Rex and QantasLink.** Two real carriers now share Adelaide's
  regional apron — Rex with two Saab 340Bs on its SA network plus Broken Hill and Mildura,
  QantasLink with a Dash 8-400 to Port Lincoln and Alice Springs — on real bays 50E and 50F
  (six bays for six aircraft). New games open with a departure about every 12 minutes;
  existing saves gain the carriers on load. 302/302 EditMode.

- **Away summary reads properly.** It names real bays ("parked on Bay 50D") and sizes each
  line to its text so long statuses no longer clip. 298/298 EditMode.

- **Emu Air keeps regional hours.** AI departures fall between 06:00 and 21:00 Adelaide
  time (a plane ready later waits on its stand for the morning) and follow a weighted
  regional network — Kangaroo Island, Port Lincoln, Whyalla, Mount Gambier, Melbourne,
  then the outback ports — instead of any reachable airport at any hour. 298/298 EditMode.

- **Real bay names and a quickest-stand button.** Stands read as Adelaide's real bays
  (Bay 50D…50A) everywhere; a landed aircraft offers one-click "Quickest: Bay 50C · 3 min
  taxi". The Flights board shows each away flight's level. 296/296 EditMode.

- **Less HUD garbage.** IMGUI text styles and the planner destination list are built once
  and reused instead of reallocated on every OnGUI pass (several per frame, one per fleet
  row), cutting per-frame GC pressure. 294/294 EditMode.

- **Landings roll off the runway.** The rollout brakes to a 12 kt exit speed and turns
  straight onto the exit instead of stopping on the runway first; the vacate taxi starts
  at that speed. Ground paths now report their entry speed at t = 0. 294/294 EditMode.

- **Review-shot launch flags.** `-airsideReviewPanel plan|hangar|flights|devtools|help` and
  `-airsideReviewShot <png> [-airsideReviewDelay s]` (with `-airsideSoak`) open a panel,
  capture the finished frame including the HUD, and quit — for repeatable HUD-fit checks.

- **Route map no longer shows two Australias when zoomed out.** The view centre flipped
  between two positions every frame once zoom eased; it now centres when the view is
  wider than the map. 294/294 EditMode.

- **Gulf St Vincent coast and OSM credit.** The overview now has the real coastline from
  OpenStreetMap: coastal plain, beach and sea past the airfield, fading into the haze.
  "Map data © OpenStreetMap contributors" is on screen. Fixed the Adelaide ground shader
  never being included in builds (packaged games showed a flat fallback) and its
  over-bright lighting. Overlapping aircraft tags stack. 293/293 EditMode.

- **Smoother zoom, real altitude, realistic taxi.** Camera and route-map zoom ease in
  proportional steps (no more leaps; scrolling a panel no longer zooms the camera).
  Away flights follow a climb / cruise / descent profile (FL shown on map and fleet
  status; HUD shows height and climb/descent). Taxi corners ~10 kt, 10 kt on the apron,
  5 kt onto the stand. Added the YPAD surroundings plan. 287/287 EditMode.

- **Live flights on the map + field tags.** Flights are plane icons gliding along their
  great-circle route with distance to go and landing time; zoom to 60× and click a plane
  to track it live. Registration tags float over aircraft on the field (L toggles). The
  planner points to a free aircraft or says when a busy one is back. 280/280 EditMode.

- **Flight planner.** Tab opens a planner with an aircraft switcher (`[` `]`), a
  destination list with distance and flight time, departure chips plus ±5 min, and a
  full trip timeline before Schedule. Map clicks no longer get eaten by panning (pan
  starts after a 4 px drag; clicks pick the nearest dot or aircraft), hovered dots show
  a tip. Esc closes panels first. 275/275 EditMode.

- **`main` compiles in Unity again.** Fixed an ambiguous `Object.Destroy` in fleet
  visuals and two undeclared map-panning fields from the UI stack, and replaced four
  hand-written `.meta` GUIDs that collided with existing prefabs. 269/269 EditMode.

- **Merged UI stack (#224–#228).** Map/Hangar, Flights board, Dev Tools, Controls
  help and live day/night lighting are on `main`.

- **Live day/night lighting.** Sun, ambient, apron floods and aircraft lamps follow
  Adelaide local time again (daylight pin removed). Debug pin override remains available.

- **Controls help (F1).** On-screen hotkey sheet for camera, airline panels and
  playtest tools. Esc closes the sheet before clearing selection or opening the menu.

- **Dev tools (F8).** Playtest panel with fleet status, next-event time,
  auto-schedule for idle player aircraft and assign-free-stands. Shares the
  overlay slot with Map / Hangar / Flights. Live time stays on.

- **Flights board (T).** Time-ordered board of every player and AI movement with
  route, phase and next clock time. Click a row to follow on the field or track
  on the map when away. Shares the map panel slot with Map (Tab) and Hangar (H).

- **Zoomable Australia map and Hangar.** Scroll and drag the destinations map to
  zoom and pan; denser coastline, state borders and region labels appear as you
  zoom in. Hangar (H) lists your aircraft and AI flights with live progress —
  click a row to follow on the field or track on the map when away.

- **Click an aircraft on the field to follow it.** Direct 3D selection (invisible
  pick proxies + click-vs-drag) follows that exact player or AI aircraft, with a
  selection ring and details card. Fleet-panel rows stay selectable with a visible
  Select affordance; dragging to pan does not select; off-field aircraft stay on
  the map only.

- **Aircraft are selectable from the fleet panel.** Click a player or AI
  registration to follow that exact aircraft at Adelaide, with a highlighted
  identity card; aircraft away from Adelaide open on the route map. Overview,
  `R`, or `Esc` clears the selection without changing the simulation.
  Packaged-app reachability of that registration-only control was incomplete —
  superseded for primary use by direct 3D selection above.

- **Real regional stand markings.** Adelaide bays 50A–50D now have yellow apron
  lead-ins, stop bars and world-rendered identifiers derived from the generated
  YPAD bay geometry, without changing aircraft routes or timing.

- **The real Adelaide Airport layout is in the game.** Taxiways, aprons, holding
  points and terminal footprints now come from OpenStreetMap, 12/30 crosses where it
  really does, and aircraft use real regional bays 50A–50D. Pushback is a tail-first
  curve, taxi turns slow down with the radius, and every ground time comes from real
  route lengths at ATR speeds (15 kt taxi, 2 kt pushback). Departures roll from the
  runway 05 threshold.
- **Click and drag to move around.** Left-drag pans the camera across the field;
  clicks on HUD panels and plain clicks are unaffected.

- **Live real time.** The game clock is the real time in Adelaide and runs at 1×;
  pause, time rates and skip are removed. Saves move to v3 with older saves
  migrated. Typing in the airline name no longer moves the camera. Aircraft now
  start their engines before departure (beacon, doors, right then left engine) and
  shut down after parking. The game opens with a short skippable intro.

- **Soak-tested and playtest-ready.** A 30-day simulation soak test, an unattended
  soak mode for packaged builds (30 min at 1× and 10 min at 60× both clean), and a
  script that zips the exact build with a tester note.

- **First-flight guide.** New players get a step-by-step card for their first
  round trip, with the next button highlighted. The destinations map is now
  opaque enough to read over the airfield.

- **The day is a real 24 hours.** Lighting time now matches the airline clock and
  real-length flights; weather changes hourly instead of every five minutes.

- **The airport keeps running while you're away.** Continue catches the airline
  game up by the real time since the save (up to a week) and shows what happened
  and what needs you. Save format version 2 adds the save timestamp; version 1
  saves still load.

- **Airline panels fit every target window size.** New `AirlineHudLayout`,
  tested at six sizes for bounds and overlaps. The destinations map no longer
  scatters its lines on scaled HUDs (small or Retina windows).

- **Props, control surfaces and wheels pivot correctly in packaged builds.** The
  glTF loader made kit meshes unreadable, which silently broke every pivot rebake
  outside the editor. Airspeed readout now follows the aircraft you are watching
  in airline mode. A stale climb-out speed assertion is anchored to the ATR 42
  profile. All 221 EditMode tests pass.

- **Your airline is saved (ADR 0045).** Autosave after every command, on fleet
  events, every 20 s and on quit; the start screen offers Continue. Saves are
  JSON, written atomically, versioned and validated on load; a restored game
  continues exactly as if it had never stopped.

- **Your airline and Emu Air fly in 3D (ADR 0045).** The demo circuit gives way
  to the fleets once an airline starts: apron bays, pushback, taxi on the
  Adelaide taxiways, hold at E2, lineup, takeoff, approach, landing, vacate, a
  stand queue on Taxiway F and taxi in, with airline liveries. Fleet runway
  occupancy now covers lineup, approach and vacate. Emu Air's colour matches its
  brown-and-gold decal.

- **Player airline, first slice (ADR 0045).** Adelaide is the default location.
  New `Airline`, `AircraftType` (ATR 42-600, 1,100 km planning range) and an
  Australia-wide `DestinationCatalogue` with great-circle, real-length legs.
  `AirlineOperations` runs fleets through taxi, a single tower-sequenced runway
  (arrivals first, 90 s separation), off-map legs, a 40-minute outstation
  turnaround and stand assignment; it is event-driven, so any frame size, rate or
  skip reaches the same state. Emu Air flies two ATRs by itself. HUD: start
  screen, fleet panel, destinations map with range locks and live tracking, stand
  choice, 10×/30×/60× and Skip. The 3D aircraft do not follow the fleets yet.

- **Player-airline design agreed (ADR 0045, docs only).** Adelaide starter
  airport run autonomously; the player runs one named airline starting with one
  ATR alongside AI Emu Air; destination and departure time chosen by the player,
  runway by the tower, stand on arrival; Australia-wide destinations map with
  range locks; real-length flights with faster time rates; economy deferred.
  No code changed.

- **The circuit is now flown to ATR 42 performance (ADR 0044).** Phase durations
  were hand-picked and the speeds fell out of them, unchecked: the takeoff roll
  passed rotate at **179 kt** and left the field at **257 kt**, the climb-out ran
  at 208, the last 600 m of final was a 1.53° drag-in rather than 3°, and there
  was no flare at all — just a shallower straight line into the tarmac. New
  `CircuitProfile` (pure C#, in Simulation) holds the stations and the reference
  speeds and **derives** every duration from distance over mean speed;
  `AirportCircuit` reads them and `AirsideFlightPath` only turns them into
  positions, each segment at constant acceleration. Rotate is now 100 kt, climb-out
  170, the glideslope a true 3°. The flare is a real round-out: it begins at 30 ft
  as the threshold passes underneath and floats 300 m onto the touchdown-zone
  markings while the sink is arrested from 584 to 60 ft/min, monotonically.
  Circuit runs 182 s against 162 — a 900 m roll to Vr genuinely takes 35 seconds.
  Attitudes retuned (rotate 9°, climb 7.5°, progressive flare to 6.5°). Tyre spin
  now reads the scheduled airspeed instead of differentiating the position curve.
  A live airspeed readout sits above the control bar, reading the same visual
  progress that places the aircraft so the number always agrees with what is on
  screen. The speed schedule itself lives in `CircuitProfile`, not the flight
  path, so the figure the player reads is covered by the headless tests rather
  than being the one part nothing could check.
  **Verified:** `scripts/test-domain.sh` **140 passed, 0 failed** with 20 new
  `CircuitProfileTests`. The Unity-only path curves were replicated numerically:
  every speed within **2.3 kt** of schedule, all phase seams continuous to
  **0.0000 m**, flare sink falling 581 → 0 ft/min. **Unity Play still required —
  the numbers are right, whether it looks right is a Mac judgement.**

- **Audit of the branch-consolidation graft (ADR 0043, docs only).** `c23cfa1` is
  a 74-parent octopus merge whose tree is exactly parent 1's — all 73 other tips
  contributed zero content, so their commits are reachable history with nothing
  in them. That is why the Pass A wheel fix was missing while its commit was an
  ancestor of `main`. Audited all 73 tips for C# declarations absent from the
  tree: 87 hits, of which only **Pass A was a real, live loss** (already fixed in
  #203). Pass B is superseded by `RebakeAircraftArticulatedPivots`, which already
  rebakes the gear struts to their top hinge; Pass C does not apply to v02, whose
  panes are generated on the analytic fuselage surface and sit a uniform 1.6–3.2
  cm proud by construction; Pass D's belly-door trap no longer reaches the runway
  on v02 (0.18 m drop on the flank doors, 0.56 m on the nose, contact plane at 0).
  The 66 symbols from the Adelaide landside branch are lost but moot behind the
  bare-field flags. Nothing further restored. **Correction:** an earlier figure in
  this session put the cabin panes up to 19 cm proud — that was an artefact of
  interpolating fuselage vertices by height on an ellipse with a varying centre
  height, not a real protrusion.

- **Wheels spin on their axles again, plus tyre smoke.** Every tyre/wheel/rim
  node in `mdl_atr42_starter_v02` ships with an identity transform and its mesh
  baked at aircraft-space position, so spinning about the node's own X axis swept
  each part on a circle about the fuselage centreline — 0.37 m for the forward
  mains, 0.84 m for the aft, and 8.35 m for the nose wheels, carrying them up over
  the aeroplane. This was diagnosed and fixed once before (Pass A, `177c075`), but
  `RebakeWheelPivots`, `RebakeOwnMeshToPivot` and `AirsideAircraftParts` were never
  in main's *tree* — the branch was grafted into main's history without its
  content, so the roll pass survived while the rebake that made it correct did
  not. Restored, with the shared `RollsInPlace` name contract locked by
  `AircraftPartsTests` so the rebaked set and the spun set cannot drift apart
  again. `Rim*` parts were also never matched by the old inline filter, so rims
  stayed still while their tyres moved; they roll now. New pooled tyre smoke fires
  a burst from the real main-gear contact patches at touchdown and thins out
  through the rollout as speed bleeds off.
- **Camera: follow-off releases in place, and zoom works (ADR 0042).** Toggling
  follow off dragged the player back to a fixed overview; it now hands the camera
  back where it is, and R is an explicit reset. The F key was handled by *both*
  `AirsideCameraController.ReadInput` (LateUpdate) and
  `AirsidePrototype.ReadSimulationControls` (Update), so one press turned follow
  off and straight back on — F could never disable follow. Follow and reset now
  have exactly one owner. Scroll while following biases the phase framing instead
  of being erased by the follow lerp. Added middle-drag pan, Q/E keyboard orbit,
  Z/X camera height; pitch opens to 4°–85° with a ground-clearance clamp so a low
  orbit cannot sink the camera through the airfield.
  **Verified:** `scripts/test-domain.sh` **121 passed, 0 failed**; axle centres
  checked against the shipped glTF (every tyre's bounds centre sits exactly on its
  axle, contact patch at 0.0000). **Unity Play still required for the wheel
  rotation, the smoke and the whole camera scheme.**

- **Strip Airside to a bare circuit sandbox.** Remove the objective layer
  entirely: economy, routes, reputation, staffing, research, stand capacity,
  daily reports, turnaround workflows, the event log, the ATC phraseology engine
  and the ground-traffic fleet, plus the whole `Persistence` assembly — no
  autosave, no save-on-quit, no briefing or away-summary overlays; every launch
  starts fresh on approach. `AirportSimulation` becomes a circuit driver for one
  aircraft; the runway reservation and taxi geometry stay because the visible
  pavement and ground path are drawn from them. Both parallel HUDs
  (`AirsideCanvasHud`, `AirsideToolkitHud`) are deleted in favour of one IMGUI
  bar carrying exactly pause, follow and 1×/2×/4×, with a working pause menu
  (Resume, Restart circuit, Quit); `HudLayout` is rewritten to match. Speed
  gains a 2× step and selecting a rate clears a pause. Multiple instances are
  fixed three ways: `forceSingleInstance` 0 → 1, a real `Application.Quit` path
  behind the menu, and a static guard that disables and destroys a duplicate
  bootstrap. Every aircraft visual, the Adelaide ground/pavement/perimeter, the
  terrain field and the camera are untouched (ADR 0041).
  **Verified:** `scripts/test-domain.sh` **89 passed, 0 failed** (154 removed
  tests covered the deleted subsystems); `HudLayout` additionally checked
  against a Rect/Mathf shim from 320×240 to 3456×2168, those cases committed
  into `PresentationLayoutTests`. **Unity compile, `scripts/test-unity.sh`, a
  Mac build and a packaged-player check of the bar, menu, 2× and single-instance
  quit are still outstanding — Presentation cannot be compiled on this Linux VM.**

- **BRD-002 app icon:** 1024² Airside Standalone/macOS icon (runway-A mark on
  Runway Ink, no baked text) generated via ChatGPT image path, registered as
  BRD-002, and set as the default Player icon. Candidate + Brand PNG + prompt
  evidence committed; Dock/Finder appearance still needs a Mac Unity build to
  verify.

- **Consolidate Airside on one validated `main`.** Every remaining branch tip is
  retained in `main` history so obsolete branch refs can be removed without losing
  work, while the current Adelaide pavement and AIR-001 v02 tree stays authoritative.
  Repair the lane-offset compile break, give the circuit landing enough time to
  join the approach and brake smoothly, remove the takeoff/departure speed drop,
  and update stale route/reputation/terrain assertions to their current contracts.
  **Verified:** Unity 6.3 LTS EditMode **300/300 passed** and universal Mac build
  completed successfully. Standalone .NET 8 SDK was unavailable on this Mac.

- **AIR-001 v02 visual finish (draft):** stout cabin, blunt drooped nose and rising rear cone, four broad planar cockpit panes with a narrow centre post, fitted rounded planar cabin glazing,
  correctly located passenger/cargo doors, and a continuous fuselage–dorsal
  fairing–fin–tail-saddle–tailplane assembly with no daylight gap. The duplicated 6.6 m engine shells are replaced by compact single nacelles; the oversized 4.6 m-root-chord wings are rebuilt to a roughly 51 m² tapered planform with matching flaps, ailerons, spoilers and track fairings. Lights, antennae, pitots, exhausts, gear and control surfaces remain. New glTF identity ships through StreamingAssets with v01
  fallback; 150 named parts / 14,456 triangles, original dimensions and moving
  part names retained. Python geometry checks and static review passed; Unity
  compilation, animation and packaged camera verification pending (ADR 0040).

- **Aircraft movement fixes (draft):** reserve the runway when checking entry to
  taxi-in; report actual runway vacation and start separation when the arrival
  clears the holding position, without false vacation calls in circuit mode.
  Brake the circuit rollout to rest, start takeoff from rest and blend departure
  pitch. Four regression tests added; .NET/Unity execution pending because both
  runtimes are absent here. Saves and circuit mode unchanged.

- **YPAD silhouette geometry corrections.** Fillets are now true concave
  fillets tangent to both pavement edges, so a 23 m stub reads as 23 m instead
  of the 80–107 m blob the old corner-centred quarter-disks produced. Taxiway F
  moves to the ICAO code 4E separation (182.5 m) with A at 290 m, which takes F,
  A, every stub and both apron pads out of the 150 m runway strip and leaves room
  for hold-short bars at the real 90 m holding position. The perimeter fence is
  seated on `AirsideAdelaideGround.WorldHeight` instead of world Y = 0, where it
  had been floating ~2.4 m above the boundary lip for its whole 11 km, and its
  panels are batched per side. Taxi paint is a continuous centreline plus double
  edge lines; hold-shorts are ICAO pattern A. Also: fillet builder no longer
  mutates a shared material, fillets carry real UVs and sit on the pavement
  surface, 12/30 and the fillets are back in the wet-surface filters,
  `AllFillets()` is cached, and stub shoulders are in the distance field.
  Simulation, saves and the circuit skip untouched. No buildings (ADR 0039).
  **Verified:** `scripts/test-domain.sh` **236 passed** (21 Adelaide pavement
  tests, up from 11; the same 4 pre-existing failures as `main` —
  `Taxiing_ReleasesEachSegmentBeforeReservingTheNext`,
  `TaxiRoutes_UseDoglegThroatBeforeStandLeadIn`,
  `AwaySummary_ReportsRouteIncomeAndReputationChange`,
  `Weights_KeepDryGrassDominantAcrossTheOverviewCore`). The three blocker
  regressions were each confirmed to fail against the old geometry. Unity Play /
  Mac build still required — `AirsidePrototype` needs UnityEngine and cannot be
  compiled headlessly; its five rewritten builders were Roslyn-parsed and
  type-checked against a UnityEngine shim instead.

- **YPAD bare field gains a denser taxi/apron silhouette.** Taxiway A runs
  parallel north of F; D2/E2 add inner runway exits; A–F links and apron entries
  feed an empty terminal apron pad (west of 12/30, clear of both runways) plus a
  small RFDS pad south of 05/23. Fillets and the site fence remain. No buildings.
  Sim taxi unchanged (ADR 0038).
  **Verified:** `scripts/test-domain.sh` **225 passed** (11 Adelaide pavement
  tests; 4 pre-existing failures also red on main). Unity Play / Mac build still
  required (no editor on this Cloud Linux VM).

- **YPAD taxi/runway joins get Code C/E fillets, and the 785 ha site gets its
  security fence.** 42 m quarter-disk fillets smooth F↔D/E and runway↔D/E;
  Taxiway F gains end caps and 3.5 m sealed shoulders; taxi paint is yellow;
  TDZ rubber bands darken 05/23; a 2.44 m perimeter fence with N/W/S vehicle
  gates follows the published site rectangle. No buildings. Sim taxi unchanged
  (ADR 0037).
  **Verified:** `scripts/test-domain.sh` **223 passed** (9 Adelaide pavement/
  perimeter tests; 4 pre-existing failures also red on main). Unity Play / Mac
  build still required (no editor on this Cloud Linux VM).

- **Adelaide bare field gains a YPAD pavement silhouette.** The single strip is
  renamed `Runway 05/23`; `Runway 12/30` (1 652 × 45 m at 73°) crosses it; Taxiway
  F runs parallel with D/E exit stubs; paint and the ops plateau match the existing
  strip standard. Simulation taxi / circuit skip unchanged (ADR 0036).
  **Verified:** `scripts/test-domain.sh` **221 passed** (7 new pavement tests; 4 pre-existing failures also red on main). Unity Play / Mac build
  still required (no editor on this Cloud Linux VM).

- **Bare circuit HUD and flight presentation match the visible world.** Economy /
  research / stands chrome hides on the bare field; coast ambience mutes with no
  coast in view; engine audio reaches ATR follow distances (~220 m); gear doors
  close when locked up or down and open only in transit; prop discs use a thin
  glass blur; landing follow keeps look-ahead through rollout; a soft rotate
  whoosh fires once on lift-off.
  **Verified:** `scripts/test-domain.sh` **214 passed** (4 pre-existing failures
  also red on main). Unity Play / Mac build still required (no editor on this
  Cloud Linux VM).

- **Adelaide bare field looks authored, and the ATR circuit reads weightier.**
  The 16 m tiled grass cube is replaced by a multi-scale CC0 ground mesh
  (`AirsideAdelaideGround` + `Airside/AdelaideGround` shader) with flat runway
  clearance, dirt shoulders outside the 45 m strip, and multi-scale asphalt.
  Presentation dynamics: eased gear, distance/radius tire spin, brief oleo
  settle, softer prop disc, ATR material/LOD polish, and touchdown smoke that
  fires once even with world props disabled. See ADR 0035.
  **Verified:** `scripts/test-domain.sh` **214 passed** (6 new Adelaide ground
  tests; 4 pre-existing failures also red on main). Unity Play / Mac build /
  packaged loops still required.

- **AIR-001 is now the final ATR 42-class starter aircraft, not another v06
  placeholder.** The new 158-part asset matches the official 22.67 m length,
  24.57 m span, 7.59 m height and 3.93 m six-blade prop diameter. It has twin
  nose wheels, tandem main wheels, working gear/doors, props, flaps, ailerons,
  elevators, rudder, spoilers and cabin/cargo doors with corrected pivots.
  Follow framing, prop blur and touchdown wheel spacing now fit the real-size
  aircraft. Fictional Airside livery only. See ADR 0034.

- **One circuit now explains itself from follow.** Same v06 turboprop: gear,
  props, attitude and landing lights follow the land / roll / takeoff loop.
  Lights stay on in pinned daylight; props keep spinning in the climb; cabin
  doors stay shut on the skipped stand. Restrained touchdown smoke fires once
  when the path meets the runway at the 300 m TDZ, not on short final.

- **The 3 100 × 45 m runway now has real-metre markings.** Threshold bars (12
  per end), aiming points at 400 m, a dashed 30/20 centreline, 0.90 m edge
  lines, and touchdown-zone pairs at 150/300/600/750/900 m. The 300 m pair sits
  under the circuit touchdown. Paint comes from `AirsideRunwayMarkings` (no
  UnityEngine); the miniature WLD-001 kit is not used. See ADR 0032.

- **The aircraft now flies a real-metre land / takeoff circuit on the 3 100 m
  runway.** Approach starts 4.2 km west; landing rolls ~1 050 m and almost
  stops; takeoff rotates after ~900 m and climbs out until the model is off
  the field; the next arrival then appears on long final. Taxi, stand and
  pushback are skipped. See ADR 0033.

- **The visible world is one plane, one 3 100 × 45 m runway, and empty Adelaide
  ground.** Buildings, cars, signs, taxiways, apron, coast, trees, fences,
  decorative lights and a second aircraft are no longer spawned. The ground is
  3 400 × 2 309 m (785 ha, the published Adelaide Airport site). The runway is
  YPAD 05/23 at real metres, not the 1:20 miniature. Overview camera, far clip
  and fog are sized for that field; daylight sun lighting is unchanged.
  Simulation layout, reservations and saves are untouched. See ADR 0032.

- **Adelaide-scale airfield layout with single-aircraft focus and pinned daylight.**
  Runway 05/23 at 1:20 scale (155 m) with parallel taxiways A/B and separate
  arrival/departure routes per stand (`AirportLayout`, `StandTaxiRoutes`).
  Landing rollout uses ~72% of phase for braked centreline roll from west
  threshold to B exit; takeoff rotate at ~68% of runway length. Aircraft-only
  focus shows one v06 Coastline Regional turboprop; ground traffic and second
  commercial visuals hidden. Perimeter fence removed. Daylight pinned for all
  presentation lighting; sun/moon discs disabled. Terrain field enlarged to
  384×300 m. Persistence saves via headless `AirsideSaveJsonCodec`.
  **Verified:** `scripts/build-mac.sh` on Mac (2026-09-10).

- **The airfield ground is one authored Unity Terrain with four CC0 layers
  instead of a flat, repeating grass slab.** The old ground was a single grass
  PNG repeating every 5 m across a dead-flat 210 × 180 m slab. It is replaced by
  a 256 × 220 × 8 m TerrainData (heightmap 257, alphamap 256) blending four CC0
  PBR TerrainLayers — ambientCG Ground 013 dry grass, Ground 003 green grass,
  Ground 030 worn dirt and Poly Haven Coast Sand 01 — on the built-in URP Terrain
  Lit shader, plus a Poly Haven worn-concrete apron. No MicroSplat, no paid
  asset, no runtime terrain plugin.

  Measured on the authored field: the operational plateau is dead level at world
  Y **-0.0450 min and max** across X [-64, 66] and Z [-12, 60], leaving the
  lowest pad **3.5 cm** proud; normalized heights run **0.1053–0.5538** so
  nothing clamps; relief is **3.57 m over 220 m**; the overview core is **67.1%
  dry grass, 15.0% green, 17.8% worn dirt**; the dirt shoulder measures
  **2.20–3.20 m**; and lag correlation decays monotonically from **0.799 at 11 m
  to 0.430 at 32 m** with no resurgence at any layer's tile size, so there is no
  repeat period. Every layer's albedo has its low-frequency luminance divided out
  — tile-scale spread is **0.5–2.0 luminance points** with per-pixel detail std
  preserved at **8.9–23.8** — so all large-scale variation comes from the
  splatmap.

  The terrain is baked once in the Editor (`scripts/bake-terrain.sh`) and the
  player only does one `Resources.Load` and one `Instantiate`; nothing is
  generated at startup, and the terrain is excluded from
  `StaticBatchingUtility.Combine`. The procedural slab remains the fallback until
  packaged visual QA passes.

  **Verified:** `scripts/test-domain.sh` **200 passed, 0 failed** (up from 181;
  19 new terrain tests, mutation-checked so they bite), `sync-art-streaming-assets.sh`
  clean with zero deletions, and both Unity-facing files compile against a
  stubbed engine surface. **Not verified:** `scripts/test-unity.sh`,
  `scripts/build-mac.sh`, packaged screenshots at day/dusk/night/rain,
  `Player.log` and the settled-RSS measurement all need a Mac Unity editor, which
  this pass did not have. See ADR 0031.

- **Running the art sync no longer deletes 282 committed StreamingAssets metas.**
  `sync-art-streaming-assets.sh` rm -rf'd its destination, where Unity's
  generated and committed `.meta` files live, so simply running it deleted
  tracked files and would have had Unity reissue fresh GUIDs for every synced
  asset. It now deletes only the glTF/bin/PNG files it owns, and skips
  `Textures/Terrain` because those maps are Editor-imported and never resolve
  through `ArtRuntimePaths`.

- **The chase camera cuts on a slot recycle instead of flying across the field.**
  When a departed flight's slot is reused, the new arrival appears hundreds of
  metres away on final in a single frame. The follow camera eased toward it at a
  fixed rate, so it dragged the length of the airfield for several seconds. A
  target jump larger than any aircraft can cover in one frame (20 m; the fastest
  phase at 4× and 30 fps moves about 4 m) now snaps centre, distance, yaw, pitch
  and FOV straight to the new framing. A deliberate aircraft switch still eases.

- **Aircraft move like aircraft, and 1× and 4× are both smooth.** The renderer
  sampled the simulation's whole-second phase clock, so an aircraft moved in 1 Hz
  steps: **1475 of 1499 frames** of a taxi were frozen and the 1476th jumped
  1.86 m. At 4× the jumps were four times longer, which is why the fast speed
  looked so much worse. Phase progress is a pure function of time, so
  `AirsideAircraftMotion.PhaseProgress` now evaluates it at the fractional
  presentation clock instead of interpolating stale samples — exact, no lag, no
  per-aircraft history. **Still frames go to 0** at both speeds, the largest
  single-frame step drops from 20.32 m to 0.42 m on takeoff, and the worst change
  between neighbouring frames falls from 1.000 to 0.091. Air-phase distances are
  now sized against the phase durations, so speeds read as an aircraft:
  approach 13.1–14.7 m/s, landing 13.1 braking to 2.1, taxi 1.9, takeoff 1.5
  accelerating to 25.1, climb-out 23.0–28.8. The landing rollout used to be
  *slower* than a taxi. Takeoff no longer snaps 143° at the phase boundary — it
  turns onto the centreline along a constant-radius line-up arc, worst heading
  step 0.72°. Taxi is parameterised by distance in both directions (reverse used
  to mirror only the segment index, a tenfold speed swing within one phase) and
  route corners are filleted, worst heading step 1.28°. A departure holds short
  **3.10 m clear of the runway edge** instead of on the centreline, and a
  departure fly-out replaces the teleport-and-freeze. Aircraft-focus mode
  (`AirsideFocusMode`) parks ground vehicles, stand equipment and people so the
  aircraft loop can be judged on its own. The horizon dome was opaque and
  depth-writing, hiding everything past its 165 m radius, so an arrival popped
  into existence through the sky wall; it is now a background-queue backdrop and
  the star sphere sits beyond the flight envelope. Recorded as
  `docs/decisions/0030-aircraft-motion-and-focus.md`, which also scopes the one
  thing this pass did **not** do: the parallel taxiway, extra runway exits and
  larger map that Bailey asked for are a simulation topology change, not a
  presentation tweak. Evidence: 73 assertions in `work/flightcheck`, 13 new
  EditMode tests, `scripts/test-domain.sh` **181 passed**. Nothing here has been
  through a Unity editor — there is none on the machine it was written on.

- **An arrival keeps the runway until it is past the holding position.** The
  runway was released the instant the landing rollout ended, while the aircraft
  was still on the centreline, so a waiting departure could be cleared and start
  its takeoff roll straight through it — the "multiple aircraft on the runway"
  Bailey reported. `AirportTaxiNetwork` now owns the holding-position distance
  (`RunwayHoldingPositionZ`, 6.5 m from the centreline) and derives the route
  progress that crosses it, and a flight taxiing in holds `RUNWAY-09-27` until
  it does. No deadlock: a departure holding short still releases the corridor, so
  the arrival always has somewhere to vacate to. Three new EditMode tests pin the
  hold, the holding line landing on A1 for all three stands, and a 40-cycle
  two-flight soak in which the runway never has more than one aircraft on it.
  Evidence: `scripts/test-domain.sh` **181 passed**.

- **Flight realism: continuous speed, no mid-air freeze, smoother turns.** New
  `AirsideFlightPath` builds every air phase from a speed profile instead of a
  smoothstep on position, so nothing starts or ends at zero velocity. Takeoff is
  one ramp along the runway axis — the old build lost **88%** of its speed at
  rotation, it now gains 6%. `Departed` flew to a fixed point and froze on
  screen; it now climbs out continuously to 240 m with the chase camera easing
  back. Approach/landing speed spread drops from 200×/575× to 1.2×/8.2× (the
  8.2× is the intended braked rollout). Aircraft and ground-traffic turns use
  frame-rate independent exponential damping, and bank angle is damped per
  airframe with a slower roll-in than roll-out. Control surfaces, flaps and
  spoilers moved onto the presentation clock (they animated while paused) and
  onto schedules keyed to rotation and touchdown. Tires spin up and wind down
  with the aircraft and stop once the wheels leave the ground. Propeller RPM
  spools between phases instead of jumping, up faster than down. The chase
  camera tracks harder on the fast phases so a departure cannot outrun it out of
  frame. Engine pitch and volume follow the spooled RPM, so a takeoff no longer
  sounds identical to a pushback. Six new EditMode tests pin no-stall, seam continuity, no slowdown at
  rotation, a departure that keeps flying, wheels that stop when airborne and
  frame-rate independent damping. Evidence: all 21 assertions mirrored and
  passing in `work/flightcheck`; `scripts/test-domain.sh` **178 passed**.

- **Runtime kit combine + per-frame cache.** Forecourt benches/planters/signs/
  bollards, airside planter strip, luggage trolleys, landside benches, fence
  corners, pedestrian gates, vehicle-gate furniture, chocks, belt loader, tug
  towbar fallback and windsock fabric stamp one cached combined mesh (with
  per-part local offsets where poses differ). Dome/sun/moon/stars/foam/spray/
  puddles/smoke/engine audio cache renderers instead of `GetComponent` every
  frame. Ops `antenna_dish` stays off the static batch so it still rotates.
  High stays 4× MSAA + SMAA, four cascades, 12 additional lights, two probes.
  Decision 0029. Evidence: brace depth 0; `scripts/test-domain.sh` **178 passed**.

- **Runtime kit combine + static-batch skip.** ALS stations, REIL, cones,
  barriers, signs, FOD bins, dollies, windsock poles, stairs and GPU carts
  stamp one cached combined mesh per instance. Greybox shrubs/trees/clouds
  share combined primitive meshes. Cloud umbras keep drifting when tint is
  unchanged; bird wings are cached. Static combine skips GSE, clouds,
  birds, boats and foam so those transforms still move. High stays 4× MSAA
  + SMAA, four cascades, 12 additional lights, two probes. Decision 0029.
  Evidence: brace depth 0; `AirsidePrototype.cs` 255 `CreateBlock` sites;
  `scripts/test-domain.sh` **178 passed**.

- **Runtime kit combine + deferred audio.** Fence bays, edge/taxi/flood lamps,
  taxi arrows, VEG-002 scrub and VEG-001 eucalyptus stamp one cached combined
  mesh per instance instead of 3–9 kit GameObjects. Ambient wind/rain/coast and
  UI click `Resources.Load` after first frame. Sun lookup no longer scans every
  Light. High stays 4× MSAA + SMAA, four cascades, 12 additional lights, two
  probes. Decision 0029. Evidence: brace depth 0; `AirsidePrototype.cs` 257
  `CreateBlock` sites; `scripts/test-domain.sh` **178 passed**.

- **Runtime airfield paint + probe pass.** Taxi Alpha / A1 / A2 / edge paint is
  one strip per run instead of a 1 m cube dump; aiming points, TDZ, chevrons and
  taxi arrows are thinned to the readable set. Stars are one inward-quad mesh.
  Reflection probes `RenderProbe` after static combine, not mid-Awake. Medium
  thins fillet PointLights, fence rails, window/pane lights, rain drops, edge
  fixtures and scrub; High stays 4× MSAA + SMAA, four cascades, 12 additional
  lights, two probes. Decision 0029. Evidence: brace depth 0;
  `AirsidePrototype.cs` 257 `CreateBlock` sites; `scripts/test-domain.sh` **178 passed**.

- **Runtime airfield GPU-state.** Per-frame `Renderer.material` clones (heat,
  spray, puddles, smoke, skids, foam, clouds, shadows, sun/moon, ARFF bar)
  now read and write through `MaterialPropertyBlock`. One `AirsideSceneIndex`
  scan replaces Awake/Update `GameObject.Find` / `FindObjectsByType`. Stars
  share one UnlitSky material; birds, trees, clouds, binder and greybox props
  use `sharedMaterial`. Greybox fuel pad is one slab; landside bay/access paint
  is combined; kit ALS laterals and extra runway mid-dashes are skipped.
  High remains 4× MSAA + SMAA. Decision 0029. Evidence: brace depth 0;
  `AirsidePrototype.cs` 258 `CreateBlock` sites; `scripts/test-domain.sh` **178 passed**.

- **Runtime airfield performance (P0–P2).** Retired the tile-built operational
  airfield and terrain-kit cube dump in favour of combined runway/taxi/apron
  pads plus the WLD-004 kit. Streamed textures and Lit materials are cached and
  shared; tints use `MaterialPropertyBlock`. Scenery is marked static and combined.
  Prefabs load on demand through Addressables keys (no `Resources.LoadAll`).
  High keeps 4× MSAA / four cascades / two probes; Medium is 2× MSAA, two
  cascades, one 64px apron probe. Probes refresh only on weather/time bands.
  Decision 0029. Evidence: `AirsidePrototype.cs` ~11k lines / 266 `CreateBlock`
  call sites (was ~18k / 3,500+); brace depth 0; `scripts/test-domain.sh` **178 passed**.

- **Airfield startup safeguard.** Replaced the oversized generated `BuildAirfield`
  body (which threw `InvalidProgramException` in the packaged player) and its
  unreachable tens-of-thousands of outer grass primitives with a textured terrain
  base. The operational runway, taxi, apron, building and prop construction stays
  intact. Unity EditMode is 192/192; packaged visual-first-frame QA remains open.

- **Free CC0 runway asphalt.** Poly Haven's Asphalt 01 diffuse, OpenGL normal and
  roughness maps are resized to the existing 1024px budget and wired as the
  preferred `tx_asphalt_runway_*_v03` source. The roughness map becomes the
  smoothness alpha; generated v02 and v01 maps remain fallbacks.

- **EditMode compile repair.** Replaced an incompatible StableId containment
  constraint in `BugfixPassTests` with an explicit typed predicate.

- **Free CC0 coastal ambience.** Jasinski's field-recorded beach wave is losslessly packaged as a Unity WAV and replaces the synthetic coast bed when available; the procedural clip remains as fallback. Source and shipped checksums are recorded in the asset register.

- **Free CC0 ambient rain.** Ylmir's 45-second loopable OpenGameArt rain bed is packaged through Resources and replaces generated rain crackle when available; the procedural clip remains as a fallback. Source checksum and licence are recorded in the asset register.

- **Free CC0 ambient wind.** SketchMan3's loopable OpenGameArt wind bed is packaged through Resources and replaces generated wind noise when available; the procedural clip remains as a fallback. Source checksum and licence are recorded in the asset register.

- **Free CC0 UI audio.** Kenney `ui_select_005.ogg` is packaged through Resources and plays on all UI Toolkit and Canvas HUD button actions, respecting mute. Licence, source hash and fallback are recorded in the asset register. Unity imported the asset; local EditMode execution is still blocked while the Unity package resolver initialises its dependencies.

- **Presentation compile repair.** Restored the presentation-clock backing field and converted malformed generated decimal literals to C# float literals, removing the prior source-level conversion errors from `AirsidePrototype.cs`.

- **Art sourcing checklist.** `docs/art/FIRST_PLAYABLE_ART_SOURCING_CHECKLIST.md`
  lists every first-playable item to source (wheels, props, engines, trees, GSE,
  audio, …) with in-git / quality / target-path columns; linked from the art
  index and `GAME.md`.

- **Layering / collision / routes (100-fix).** Stand centres 14/24/34; dogleg lead-ins +
  `APRON-THROAT`; GT off-field Away hold; run-up bay off Alpha; selective Yield; length-
  weighted taxi; soft commercial motion; dual approach lanes; ATC vacated/respawn/GT-hold
  fixes. Evidence: `scripts/test-domain.sh` 177 passed; `CollisionPass100Tests`.

- Document 100-item layering / collision / taxi-route bug audit (`docs/testing/BUG_AUDIT_2026-09-09_LAYERING_COLLISION_ROUTES.md`).
- **Ground authenticity + aerodrome ATC (cycle 136).** Apron→Alpha mid fillets; vacated cue now
  hands off to Kingscote Ground / taxi-to-stand; taxi-to-stand phrases add surface wind + remain
  this frequency. Evidence: Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 135).** Hold bars K/L + Alpha edge east near A2;
  soft taxi-lead throat/mouth fillets; departure hold traffic names real arrivals / lined-up /
  FIFO leaders (no fake "on approach"). Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 134).** Outer far rim to ±152 for overview 155
  (240 tiles, then subdivided); runway blast pads refined; mid-downwind / base / final / cleared-to-
  land deepened. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 133).** Overview camera 155 + larger horizon dome
  (330×150); established-final phrase deepened. Evidence: brace depth 0; Unity Editor open — Press
  Play.

- **Ground authenticity + aerodrome ATC (cycle 132).** Jetty/service/car-park pads ≥3.7 subdivided
  (Jetty deck Find target kept); denser service-lane paint; short-final clearance deepened.
  Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 131).** Car-park/grass/coast/outer pads ≥4.8
  subdivided (~2169); apron corner chord fillets; denser runway edge fallback; arrivals name
  lined-up / hold-short departures; on-stand + continue-approach deepened. Evidence: brace depth 0;
  Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 130).** Apron/fringe/fuel/stand pads ≥4 subdivided
  (~33); takeoff clearance acknowledges prior line-up-and-wait. Evidence: brace depth 0; Unity
  Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 129).** Denser access centre/edge paint; extra runway
  mid dashes; denser stand lead-ins; traffic advisory names number-two departure. Evidence: brace
  depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 128).** Taxiway/access/fuel/apron pads ≥4.5
  subdivided (~77); denser Alpha centreline + edge paint; LUAW window 6s + conditional landing 5s;
  mid-roll radar contact cue. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 127).** Remaining hangar-apron tiles ≥5 subdivided;
  hold-short Alpha deepened (surface wind + vehicle caution). Evidence: brace depth 0; Unity Editor
  open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 126).** Join-downwind adds squawk VFR; go-around
  highlights in ATC hot HUD with left-circuit label. Evidence: brace depth 0; Unity Editor open —
  Press Play.

- **Ground authenticity + aerodrome ATC (cycle 125).** Hangar/runway/stand pads ≥5 subdivided (~54);
  denser car-park bay/stall paint even with kerbs; give-way taxi phrase deepened. Evidence: brace
  depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 124).** West-mid Alpha hold bars I/J; denser aiming
  points (−6/6); conditional landing deepened (surface wind + acknowledge). Evidence: brace depth 0;
  Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 123).** Hangar apron lead/edge/stop paint; ground-hold
  and expect-landing clearances deepened (vehicle caution / vacate via Alpha). Evidence: brace
  depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 122).** Pads ≥5.8 then grass/paddock ≥5.5 subdivided
  (~2600 parents; CreateBlock ~19496); ARFF apron tiles + bay paint; fuel-pad paint retained;
  go-around adds airborne report / no turns below circuit height; frequency change + apron hold
  deepened. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 121).** Taxiway/runway-mid/access/grass pads ≥5.5–6.2
  subdivided (~1054); service-lane paint always drawn (was skipped with kerbs); fuel-pad bay paint;
  engine-start / pushback deepened; short-roll airborne cue. Evidence: brace depth 0; Unity Editor
  open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 120).** Pads ≥6.5 + jetty approach subdivided
  (~963); denser access-road centre paint; continue-taxi inbound/outbound phrases deepened.
  Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 119).** Pads/shoulders/coast ≥7 subdivided (~913);
  denser Alpha centreline + Stand 2/3 chevrons; vacated phrase no longer duplicates Contact Ground
  (expects Ground taxi next). Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 118).** Denser Alpha taxi edge paint; radar contact
  includes squawk VFR. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 117).** Pads ≥7.5 subdivided (~708); jetty mid
  tiles + apron bay joints; duplicate Contact Ground on taxi-in removed; number-two traffic still
  gets established/short-final cues; ready-for-departure expects clearance when number one.
  Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 116).** Far rim tiles subdivided; taxi-in now issues
  Contact Ground before taxi-to-stand; takeoff clearance includes circuit height 1000 ft. Evidence:
  brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 115).** Overview camera 145 + far grass rim (~168
  tiles) + larger horizon dome; jetty deck subdivided; pads ≥8 subdivided (~634); go-around /
  orbit / radar / airborne use circuit height 1000 ft. Evidence: brace depth 0; Unity Editor open —
  Press Play.

- **Ground authenticity + aerodrome ATC (cycle 114).** Pads ≥8.5 subdivided (~409); join-downwind
  phrases circuit height 1000 ft. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 113).** Denser runway centre/edge paint; Ground
  contact phrase adds apron vehicle caution. Evidence: brace depth 0; Unity Editor open — Press Play.

- **Ground authenticity + aerodrome ATC (cycle 112).** Pads ≥9 further subdivided (~819); HUD
  mid-downwind / hold-short-runway labels (brace fix). Evidence: brace depth 0; Unity Editor open —
  Press Play to audit.

- **Ground authenticity + aerodrome ATC (cycle 111).** Remaining pads ≥10 subdivided (~386);
  HUD adds mid-downwind + hold-short-runway clearance labels. Evidence: brace/string checks;
  Unity Editor open — Press Play to audit.

- **Ground authenticity + aerodrome ATC (cycle 110).** Outer paddock tiles further subdivided
  (~657); apron-hold phrase adds vehicle caution. Evidence: brace/string checks; Unity Editor
  open — Press Play to audit.

- **Ground authenticity + aerodrome ATC (cycle 109).** Large grass/coast/runway pads subdivided
  (~572); taxi-to-hold includes surface wind. Evidence: brace/string checks; Unity Editor open —
  Press Play to audit.

- **Ground authenticity + aerodrome ATC (cycle 108).** Apron/access/car-park/service/grass pads
  further subdivided (~537); established / airborne / traffic-advisory / hold-short / LUAW
  include QNH. Evidence: brace/string checks; capture blocked on macOS 15.

- **Ground authenticity + aerodrome ATC (cycle 107).** Further subdivided runway shoulders /
  infield / coast / far grass (~354 pads); mid-Alpha taxi paint + denser TDZ; circuit /
  frequency-change / number-two / conditional-land phraseology includes QNH. Evidence:
  brace/string checks; capture blocked on macOS 15.

- **Ground authenticity + aerodrome ATC (cycle 106).** E/W outer horizon + shoulders/infield/far grass
  further subdivided; mid-Alpha hold bars G/H; short-final / taxi-to-stand / vacate / on-stand
  phraseology deepened. Evidence: brace/string checks; capture blocked on macOS 15.

- **Ground authenticity + aerodrome ATC (cycle 105).** Outer E/W horizon rim at ±132; continue-approach
  includes QNH. Evidence: brace/string checks; capture blocked on macOS 15.

- **Ground authenticity + aerodrome ATC (cycle 104).** Subdivided blocky outer horizon rim (18×6 →
  ~13.5 max); further runway/apron/fuel/coast tiling; takeoff / go-around / number-two departure /
  expect-landing phraseology deepened. Evidence: brace/string checks; capture blocked on macOS 15.

- **Ground authenticity + aerodrome ATC (cycle 103).** Outer N/S horizon rim beyond overview 132;
  min-approach / conditional-land / radar-contact / apron-hold phraseology deepened with QNH and
  vacate reports. Evidence: brace/string checks; window capture blocked on macOS 15 ScreenCaptureKit.

- **Ground authenticity + aerodrome ATC (cycle 102).** Runway/stand/grass/coast tiles further subdivided;
  denser runway edge paint; larger horizon dome for overview 132; vacated/join/orbit/Alpha-hold
  phraseology deepened; Alpha holds against ground traffic now name the opposing callsign.
  Evidence: brace/string checks; screen capture still black (TCC); batchmode blocked by Editor.

- **Ground authenticity + aerodrome ATC (cycle 101).** Overview camera 132; denser runway centreline;
  inbound continue-taxi HUD label; give-way includes QNH; number-two landing expects Alpha vacate.
  Evidence: brace/string checks; screen capture still solid black (TCC); batchmode blocked by Editor.

- **Ground authenticity + aerodrome ATC (cycle 100).** Further grass/coast/apron tiling; engine-start /
  pushback / traffic-advisory / hold-short include surface wind / frequency / apron vehicle cautions.
  Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 99).** Apron/access/grass/coast pads further subdivided;
  denser Alpha edge paint, apron chevrons, taxi arrows; ready/mid-downwind/base/ground-hold/taxi-out
  phraseology deepened. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 98).** Continue-taxi after inbound holds directs to the
  apron/stand (not runway 09 hold-short). Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 97).** Taxiway Alpha main slabs ≥10 subdivided;
  frequency change signs off from Kingscote Tower; turning-final reports runway is clear. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 96).** Coast sand/berms ≥20 subdivided; denser Alpha/
  A1/A2 taxi centre dashes; LUAW holds position on the runway and asks for acknowledge. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 95).** Grass/paddock tiles ≥22 subdivided; cleared-to-land
  says runway is clear; established final expects landing clearance. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 94).** Apron/car-park/access pads ≥14 subdivided;
  denser threshold bars/side stripes; apron contention uses HoldApron with named traffic (no longer
  mis-phrased as Alpha give-way); airborne reports no turns below circuit height; continue-taxi
  includes QNH. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 93).** Denser perimeter fence posts/rails; ARFF apron
  split; Ground contact includes QNH. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 92).** Denser runway centreline / aiming / TDZ paint;
  min approach speed reports base then short final. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 91).** Blast pads + remaining apron/access pads
  subdivided; overview 128; apron hold is a real clearance (HoldApron) with HUD/pulse; Adelaide
  Centre 125.3 on frequency change. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 90).** More apron/car-park/access pads subdivided;
  join-downwind names Kingscote Tower; weather remarks say surface wind calm. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 89).** Runway edge paint segmented (no 88 m slabs);
  service lane pads + paint dashed; opposing taxi traffic says “taxiing opposite”; hold-short Alpha
  continues via Alpha. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 88).** Stand 3 / apron bay pads subdivided; cleared
  to land asks report runway vacated; continue approach includes number one expected. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 87).** Taxiway A shoulders + S2 berms subdivided;
  fuel pads split; wet matching uses Fuel pad prefix; give-way / ground-hold / expect-landing
  phraseology deepened. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 86).** Stand bay box paint segmented; arrival traffic
  advisories can name mid-downwind callsigns. Evidence: brace/string checks; batchmode blocked by
  Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 85).** Car-park kerbs split W/E; pushback advises
  taxi via Alpha; radar contact asks report frequency change when clear. Evidence: brace/string
  checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 84).** Terminal canopy slab/edge/glow/glass
  subdivided (night glow collect includes W/E); conditional landing says “when the runway is
  vacated”. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 83).** Overview camera 125; access turn/road
  shoulders subdivided; mid-downwind number-one expected; engine-start taxi via Alpha. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 82).** Hangar apron pads subdivided; taxi-to-stand /
  hold-short / on-stand phraseology names Alpha and chocks. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 81).** Car-park bay/stall paint segmented; terminal
  planter split; wet-paint prefixes for access/exit centre paint; number-two landing + short-final
  phraseology tightened. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 80).** Landside access paint dashed (centreline/
  edges/turn); hold-short Alpha / orbit / go-around phraseology aligned with mid-downwind
  reports. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 79).** Split long runway shoulders; jetty deck/rails
  segmented; A2 exit centreline dashes; base/final/vacate/Ground contact phraseology deepened.
  Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 78).** A2 exit night lights; apron fringe W/E
  tiles; fuel bund as four walls (not a solid slab); number-two / ready / traffic advisory name
  holding point Alpha. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 77).** Eastern Taxiway A2 exit chord + hold bars;
  infield grass between runway and Alpha subdivided; hold-short pulse collects by prefix (fixes
  A1/A2 bars); LUAW / hold-short / continue-taxi / takeoff name holding point Alpha. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 76).** Denser Taxiway Alpha dashed centreline and
  segmented edge paint; eastern hold-short bars; mid-downwind clearance accepted through the
  short-final chain; minimum approach speed includes wind. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 75).** Fixed coast foam/water bobbing — collect by
  prefix instead of stale exact names; further split foam pads (~5029 CreateBlocks); arrival/
  leading traffic advisories name the other aircraft’s callsign. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 74).** Split pads ≥24 including dunes/coast
  (~5020 CreateBlocks); coast bob rebuilt; mid-downwind report clearance + sim wiring;
  go-around climbs to circuit height; number-two departure includes wind. Evidence:
  brace/bob sync checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 73).** Split pads ≥28 and remaining Taxiway A
  (~4203 CreateBlocks); Access road / car-park aisle wet prefixes; landing/takeoff weather
  remarks include QNH; frequency change says radar service terminated; apron hold names
  apron traffic. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 72).** Split coast water/shallows ≥30 (~3658
  CreateBlocks); coast bob list rebuilt; pushback asks report clear of stand; established
  final asks report short final. Evidence: brace/bob sync checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 71).** Added horizon rim (72 tiles); map
  ~268×244; split grass/outer/sand ≥32 (~3577 CreateBlocks); overview camera 120; climb-out
  and radar contact to circuit height; engine start asks contact Ground for taxi; ground
  hold includes wind. Evidence: brace/bob sync checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 70).** Split paved/infield/taxi pads ≥24
  (~3346 CreateBlocks); overview camera 110; join downwind asks mid-downwind then base;
  line-up-and-wait can name arrival traffic. Evidence: brace checks; batchmode blocked by
  Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 69).** Cleared ≥35 ground pads (~3305
  CreateBlocks); coast bob list rebuilt; taxi-to-stand cautions vehicles on apron; report
  turning final; short final says runway is yours shortly. Evidence: brace/bob sync checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 68).** Cleared ≥40 ground pads (~3026
  CreateBlocks); wet helpers use Stand 3 / car-park bay prefixes; hold-short runway can
  name arrival traffic; contact Ground taxis via Alpha; ready-for-departure includes QNH;
  report turning base. Evidence: brace/string/bob sync checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 67).** Cleared ≥45 ground pads (~2831
  CreateBlocks); continue-taxi after hold says traffic clear; hold-short Alpha can name
  traffic + wind; vacated warns wake for following; taxi-out asks report ready for
  departure. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 66).** Cleared ≥50 ground pads and finer
  apron quarters; overview camera 100; on-stand welcomes to Kingscote; taxi give-way
  names opposing callsign; reasoned go-around asks acknowledge. Evidence: brace/string
  checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 65).** Cleared ≥60 ground pads; subdivided
  blast pads, apron bays, grass ribbons; outer NW/NE/SW/SE rim corners; Runway blast
  wet/paved prefix; continue-approach can caution wake; engine start says park brake set;
  landing-block reason names landing traffic. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 64).** Cleared ≥65 ground pads; removed stale
  apron-north exact wet names (covered by Apron prefix); pushback cautions jet blast
  behind. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 63).** Cleared ≥70 ground pads; further
  subdivided runway slabs; hangar apron N/S split with Hangar apron wet/paved prefix;
  departure radar contact is climb-out (not report downwind); frequency change contacts
  Adelaide Centre; report-airborne includes wind. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 62).** Cleared remaining ≥80 ground pads
  including relief berms; conditional landing confirms number one. Evidence: brace/string
  checks; desktop capture black (TCC); batchmode blocked by Editor.

- **Ground authenticity + aerodrome ATC (cycle 61).** Cleared remaining ≥90 ground pads;
  expect-landing says number one expected; ground-hold asks report when clear.
  Evidence: brace/string checks; desktop capture black (TCC); batchmode blocked by Editor.

- **Ground authenticity + aerodrome ATC (cycle 60).** Cleared ≥100 ground pads and
  subdivided apron quarters/north extensions; outer N/S/E/W rim shelves; overview
  camera 96; report-established / ready-for-departure / taxi-to-stand / contact-ground
  phrase polish. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 59).** Cleared remaining ≥110 ground pads;
  join-left-downwind says make left circuit; coast bob list kept synced (72 water/
  shallows). Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 58).** Cleared remaining ≥120 ground pads
  (hangar left intact); coast water/shallows bob list kept in sync; report-base asks
  expect further clearance. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 57).** Cleared remaining ≥140 pads; rebuilt
  coast water/shallows bob list after renames; further subdivided runway W/E/mid and apron
  bays; short-final confirms number one. Evidence: brace/string checks; batchmode blocked
  by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 56).** Cleared remaining ≥160 pads; fixed
  coast shallows bobbing names after mid-WW split; continue-approach can name lead
  traffic. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 55).** Cleared remaining ≥180 pads
  (paddocks, fringes, hills, coast sand, far shelves); left-orbit phrase can name
  lead traffic. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 54).** Cleared remaining ≥200 grass /
  paddock / fringe / hill pads; number-two landing and min-approach-speed phrases name
  leading traffic progress. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 53).** Taxiway A main + shoulders subdivided;
  remaining ≥220 grass/paddock/hill pads cleared; wet/paved matching uses Taxiway A prefix;
  number-two departure traffic phrase follows arrival progress. Evidence: brace/string
  checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 52).** Runway W/mid/E and apron quarters
  further subdivided; runway shoulder mids + coast water E2 split; wet/paved name matching
  uses Runway/Apron prefixes; LUAW “behind landing” only on wake caution; taxi-to-hold and
  apron-hold phrases ask report ready/clear. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 51).** Outer paddocks + mid/corner *b grass
  cleared below area 240; apron bay N/S halved; departure traffic advisories name arrival
  progress (circuit/base/final/short final/landing); approach report cues resume after
  orbit/go-around. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 50).** Cleared remaining ≥250 far mid/corner
  pads + fringe NW1W; SW2a/SE1a/NW2a/NE1a grass halved; far E/W map shelves (then N/S
  halved); overview 92; join/orbit wind; go-around report downwind; traffic-advisory
  report ready. Evidence: brace/string checks; Unity AX windows=0; batchmode blocked by
  Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 49).** Corner grass pads SW1a/SE2a/NW1a/NE2a
  halved W/E; fallback S1a/N2a/N1b paddocks + coast water W2 + hills NW1E/SW W split; LUAW
  report ready for departure; number-two wind; min-approach report base; radar report
  downwind. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 48).** Coast water E1 + shallows W2 + kit
  E/W paddock N + far-N grass W2/E1 halved; far-S map shelf; overview camera 88; conditional
  land / on-stand / hold-short runway phrase polish. Evidence: brace/string checks; Unity
  AX windows=0 (no Game capture); batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 47).** Kit N1a + far-N grass W1/E2 + hills
  WS/NW1/EN + coast shallows E2 + far-S E paddocks halved; expedite taxi report-on-stand;
  give-way / continue-taxi report polish. Evidence: brace/string checks; batchmode blocked
  by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 46).** Last ≥300 fringe WS2/ES2 + coast
  shallows W1/E1 + coast water mid-E/W1 halved; water bobbing name list resynced; ready-
  for-departure and hold-short Alpha phrase polish. Evidence: brace/string checks;
  Unity window not AX-accessible for Game capture; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 45).** Fallback W/E paddocks + far-S grass
  W2/E1 + hills WS/EN + coast water mid-W halved; expect-landing asks report short final.
  Evidence: brace/string checks; desktop capture black (Editor not Game-view); batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 44).** Fallback N1a + far-S grass/paddocks +
  kit S1b halved; continue-approach asks report short final; report-final number one
  expected; number-two departure names traffic on final. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 43).** Far-N paddocks/shelf + kit/fallback
  S1a/N2b/E1a/W1a and S2 paddocks halved; coast sand E + shallows mid + E/W fringes split;
  hills NE2/WN refined; land/takeoff/vacate phraseology adds vacate-via-Alpha / report-
  airborne when able. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 42).** Fallback south S1b/S2a paddocks halved;
  SW/SE grass fringes + coast sand mid split; departure LUAW/number-two/ready/hold now
  log on clearance change; LUAW asks report ready. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 41).** Mid-belt and mid-column *b grass
  halved; kit N1b/N2a paddocks + EN/WN fringes split; approach sequencing and taxi holds
  now log ATC on clearance change (not every stall tick); expect-landing wind; short-
  final “shortly”. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 40).** Remaining corner *b grass pads halved;
  fallback N2b paddocks + Hill ES + coast water E2; far SW/SE map corners; taxi/pushback
  QNH; contact-ground after vacate; squawk VFR on frequency change; established wind.
  Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 39).** Fallback N/S outer paddocks halved;
  mid-column and mid-belt grass pads refined; engine-start QNH; min-approach traffic
  ahead; orbit at circuit height; number-two departure expect further. Evidence:
  brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 38).** Corner grass pads halved N/S; far
  NW/NE map corner extensions; fringe NE2/SW1 split; left-hand circuit height join;
  report-base wind; radar identified; give-way on Alpha. Evidence: brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 37).** North paddock N1a split; coast water
  ribbons halved N/S; far E/W mid grass + NW fringes refined; NW/NE hills halved; report-
  airborne/final and number-two phrase polish. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 36).** Outer S/E/W paddocks further split
  (kit + fallback); Hill far SW/SE halved; hold-short Alpha names opposing traffic; LUAW
  includes surface wind. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 35).** Mid-column grass pads halved; far
  N/S outer paddocks split (kit + fallback); foam inner/outer ribbons; taxi QNH 1013;
  go-around calls left circuit. Evidence: brace/string checks; batchmode blocked by
  Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 34).** Remaining wide grass pads halved;
  coast foam into six ribbons; Hill far WS/EN split; taxi-to-hold fog caution; continue-
  taxi and ready-for-departure phrase polish. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 33).** N/S paddocks further split; runway
  wear into three decals; overview camera 80; short-final ATC cue on approach.
  Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 32).** Far E/W mid-band grass for bigger
  map; Taxiway Alpha into six segments; join-downwind monitors frequency; pushback
  warns jet blast. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 31).** Far-north grass fill; fallback E/W
  paddocks split; commercials blocked by other commercials get give-way-taxiing ATC.
  Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 30).** Far-south grass fill; hangar apron
  split W/E; continue-taxi clearance after a commercial hold clears. Evidence:
  coverage/brace checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 29).** Fixed mid-band grass gap under the
  runway/taxi belt; apron split into NW/SW/NE/SE; far-south paddock extension; taxi-to-
  hold remarks wet surface in rain. Evidence: geometry check; brace/string checks;
  batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 28).** North paddocks refined + far-north
  map extension; car park bays split; number-two escalates to min approach speed then
  orbit; Alpha holds say hold short Alpha. Evidence: brace/string checks; batchmode
  blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 27).** Runway shoulders further segmented;
  overview camera pulled back to 72; line-up-and-wait says "behind the landing" when
  separation is still draining. Evidence: brace/string checks; screen capture still
  black (macOS -10827); batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 26).** Grass pads to 16 tiles; apron split
  W/E; number-two waits escalate to left orbit; departure gets radar contact before
  frequency change. Evidence: brace/string checks; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 25).** Grass pads refined to 8 tiles; runway
  segmented W/mid/E; coast shallows/water further split; approach asks report final;
  conditional land-when-vacated HUD + tests. Evidence: brace/string checks; batchmode
  blocked by Editor lock; screen capture blocked (macOS -10827).

- **Ground authenticity + aerodrome ATC (cycle 24).** Outer N/S paddocks split (kit + fallback);
  landing clearances in the wake window say traffic vacating via Alpha. Evidence: string/brace
  checks; display capture blocked (macOS -10827); batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 23).** Main grass pad tiled into NW/NE/SW/SE;
  coast sand and outer paddocks further segmented; inbound ATC joins left downwind and
  reports base (also after go-around). Fixed duplicate ReportAirborne EditMode test.
  Evidence: brace/string checks; display capture blocked (macOS -10827); batchmode blocked
  by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 22).** GT holds say "give way to
  commercial" when blocked by airline traffic; remaining S/E/W grass fringes
  segmented. Evidence: Editor Play audit; batchmode blocked by Editor lock.

- **Ground authenticity + aerodrome ATC (cycle 21).** Takeoff asks to report airborne;
  kit-path outer E/W paddocks and north grass fringes further segmented. Evidence:
  Editor Play audit; batchmode blocked by Editor lock.

- **Fix three visible aircraft bugs.** (1) Fleet aircraft (GT-201) taxiing out
  from a stand cut the corner straight to the A1/A2 junction and drove across the
  infield grass — the outbound route now reverses the arrival path exactly (down
  the lead-in to the A2 join, then along the taxiway). (2) Commercial liveries
  were assigned by transient flight-list index, so a respawn-driven re-sort could
  repaint an aircraft mid-life or leave both commercials in the same paint; each
  aircraft now holds a stable livery slot keyed by its id. (3) Landing-gear doors
  keyed off "airborne" and shut on approach while the legs were still extended
  (struts clipping through closed doors); doors now track gear deployment.
  Evidence: Unity EditMode 131/131.
- **Session handoff after fidelity #167.** Point resume at `main`; Mac overview
  vs Approved modelling boards is the next sign-off. No behaviour change.

- **Tighten fidelity densify vs Approved boards.** Pale-grey open-bay ARFF shed,
  red/white truck accents, joint-free concrete + wet-concrete maps, denser scrub
  belts with pale limestone rocks, turquoise shallows, turnaround GSE zones
  (fuel port / bus starboard / GPU nose / tug at stand), dual marshaller wands,
  and clearer residual apron damp.

- **Fidelity densify against approved boards.** ARFF shed v02 is a pale-grey
  hollow open bay (bollards, fluorescents, dark void; no sign glyphs). ARFF
  truck v02 keeps deep red body with white roof/panel accents and Safety Yellow
  only on chevrons/steps/grabs (no number plate / oversized stripe). Surface
  v02 drops concrete joints, densifies grass/asphalt aggregate, and adds
  `tx_wet_concrete_*_v02` (cooler damp sheen, no puddles). Binder recognizes
  white/panel/cab-roof and bollard/chevron/grab. Additive StreamingAssets sync.

- **Integrate Bailey-approved fidelity boards.** Promote seven modelling boards
  into `docs/art/reference/`. Prefer denser runtime art against them: VEG-002
  scrub kit v02 (grass/rock/dune mixes), WLD-004 terrain kit v02, seamless
  surface maps `tx_*_v02`, ARFF truck/shed Resources prefabs v02, CHR apron
  silhouette coverage, and existing BLD v05 / turnaround fleet v06 as the
  approved building and service targets. v01 kits/maps remain fallbacks.

- **Regional ARFF facility fidelity reference.** Add a 2048×1152 scene with a
  fictional compact rescue truck, modest open-bay corrugated shed and larger
  background-hangar scale cue. A corrective pass removes recognisable branding
  and restores the faceted miniature language; runtime prefabs unchanged.

- **Airfield surface texture reference.** Add a 2048×1365 board with five
  labelled square top-down material targets for dry grass, apron concrete,
  runway asphalt, coastal sand and wet concrete. Visual tiling guidance only;
  procedural runtime maps remain unchanged and authoritative for seamlessness.

- **CHR-001/002 character silhouette reference.** Add a 2048×819 sheet with
  three distinct ramp roles and six passenger stand/walk/sit poses at a common
  overview-readable scale. Exact generation evidence included; runtime v02
  character kits unchanged.

- **Turnaround service-set fidelity reference.** Add a 2048×812 two-frame
  daylight/dusk board matched to REF-003, covering AIR-001, VEH-001…004 and
  PRP-001 placement, connections and overview readability. Exact generation
  evidence included; runtime fleet unchanged.

- **BLD-001…003 building fidelity reference.** Add a common-scale daylight
  board for the terminal, hangar and ops shed matched to REF-001, including a
  corrected gable-roof hangar and attached lean-to office. Exact generation and
  edit evidence included; runtime v05 meshes unchanged.

- **WLD-004 context terrain modelling reference.** Add a 2048×1152 review
  candidate for separated paddock berms, low hills, dunes, turquoise shallows
  and deep-water modules around a protected blank operational zone, with exact
  generation and corrective-edit evidence. Reference only; runtime kit and
  gameplay surfaces unchanged.

- **VEG-002 coastal scrub modelling reference.** Add a 2048×1152 review
  candidate covering five mallee/shrub silhouettes, three grass patches, three
  rock groups and two dune-edge mixes at 1.8 m human scale, plus exact generation
  evidence and AI-reference routing. Reference only; runtime kit unchanged.

- **Ground / vehicle / smoothness (20).** GT yield keeps mid-leg position; pause
  and 4× apply to GSE/props/tires/GT catch-up/walkers; takeoff gear down until
  rotate; AtStand visuals follow turnaround; wet/fog/coast/berm/apron seams;
  taxi spray and pushback tug logic; camera overview ease + orbit. Evidence:
  `scripts/test-domain.sh` 136/136; `docs/testing/SMOOTH_PASS_20_2026-09-08.md`.

- **Day/night readability.** Clearer coastal day sky, thinner overview fog, dimmer
  night sun key so apron floods define pools; softer post grade (less purple night
  crush / orange dusk wash). Presentation only (`ApplyDayCycle`,
  `AirsideDayVolume`). Evidence: `scripts/test-domain.sh`; Mac noon/midnight
  overview pending.

- **50-fix bugfix pass.** Simulation: hold-short runway wait survives
  reservation sync; concurrent commercials = `Capacity.StandCount`; live staffing
  during AtStand; refuse expired route Accept; research/seed/location/GT/yield/
  stand-reservation/event-log/StableId hardenings. Presentation: GSE/+X facing,
  chocks/GPU Y, stairs tip, marshaller L, fence/gate, camera follow, night
  ambient, dual-stand focus GSE, PreferArtKit null-safe. Evidence:
  `scripts/test-domain.sh` 135/135; `docs/testing/BUGFIX_PASS_50_2026-09-08.md`.

- **Eucalyptus VEG-001 v02.** Prefer `mdl_eucalyptus_kit_v02` (30 meshes):
  multi-lobe canopies, tapered trunk/bark/fork, denser lod1; place full tree
  belt; far trees add lod1 crown; v01 remains fallback. Evidence:
  `scripts/test-domain.sh` 118/118; Mac overview vs REF pending.

- **Forecourt PRP-003 v02.** Prefer `mdl_terminal_forecourt_kit_v02` (18
  meshes): slatted bench, nested trolley, rimmed planter, richer bollards/sign/
  kerbs; placement densifies kerb_corner, mid bollard, second sign, west
  planter; v01 remains fallback. Evidence: `scripts/test-domain.sh` 118/118;
  Mac overview vs REF pending.

- **Fence/gate PRP-002 v02.** Prefer `mdl_airfield_fence_gate_kit_v02` (24
  meshes): chain-link lattice bay, tubular rails/posts, frame+mesh gate leaves;
  PlaceBay densifies mid/bot rails, caps, brace; `gate_vehicle_rail` at vehicle
  gate; v01 remains fallback. Evidence: `scripts/test-domain.sh` 118/118; Mac
  overview vs REF pending.

- **Character kits CHR-001/002 v02.** Prefer `mdl_ramp_crew_kit_v02` (32 meshes)
  and `mdl_passenger_kit_v02` (42 meshes): tapered limbs, hi-vis vest/hat brim,
  marshaller wand; UpdateApronLife extract names retained; v01 fallbacks kept.
  Evidence: `scripts/test-domain.sh` 118/118; Mac overview/follow vs REF-003
  pending.

- **Ops shed BLD-003 v05.** Prefer `mdl_operations_shed_v05` (160 meshes):
  dual-pitch roof, gable ends, denser porch/corrugation, antenna/AC silhouette;
  night-glow / glass / door names retained; authored_v01+ older kits remain
  fallbacks. Evidence: `scripts/test-domain.sh` 118/118; Mac overview vs REF-001
  pending.

- **Stand stairs / GSE fidelity (PRP-001 v03).** Prefer
  `mdl_service_equipment_kit_v03` (106 meshes: tubular rails, denser GPU/belt/
  chocks) over authored/v02/v01; stairs Resources fallback
  `mdl_passenger_stairs_v02`. Safety Yellow GPU + stairs rails/nosings, Coastal
  Blue side panels. Evidence: `scripts/test-domain.sh` 118/118; Mac overview/
  follow vs REF-003 pending.

- **Hangar BLD-002 v05.** Prefer `mdl_hangar_small_v05` (193 meshes): dual-pitch
  roof, gable ends, denser corrugation, office lean; sliding-door motion names
  retained; authored_v01+ older kits remain fallbacks. Evidence:
  `scripts/test-domain.sh` 118/118; Mac overview vs REF-001 pending.

- **Vehicle fleet fidelity (REF-003/005).** Landside `mdl_parked_car_v02` (lofted
  regional car kit) prefers over cube v01; turnaround VEH-001…003 ship `*_v06`
  and pushback `v03` ahead of v05/v02 with fallbacks retained. Safety Yellow /
  Coastal Blue accents, glass/rubber/metal via `AirsideMaterialLibrary`; metre
  ASCII FBX + StreamingAssets + Resources prefabs. Evidence:
  `scripts/test-domain.sh` 118/118; Mac overview/follow vs REF-003 pending.

- **Flight/taxi polish (pass 2).** Takeoff lines up from A1 instead of yaw-snapping;
  taxi-out leaves the pushback pad without reversing onto the stand; landing
  rolls out ~22 m with touchdown FX on ground contact; stand lead pads follow the
  real taxi chords (incl. Stand 3 + apron extension); A1 fillet gets night taxi
  lights and a hold-short across the exit; prop blur discs size from blade bounds;
  taxi edge paint covers the full Taxiway A. Evidence: `scripts/test-domain.sh`
  118/118; needs Mac Play follow-camera verify.

- **Prop / landing / taxiway presentation.** Rebake glTF propeller pivots to the
  nacelle hub so blades spin in place (not around the airframe); drive prop
  rotation from true RPM (×6 deg/s). Continuize takeoff with taxi-out at (-24),
  add ground-roll then climb, shallow the approach, and flare/roll out landing to
  the A1 entry. Fix taxi centreline/edge kit yaw (identity — meshes are X-authored)
  and pave the A1 runway exit plus stand lead-ins so aircraft are not on grass.
  Evidence: `scripts/test-domain.sh` 118/118; needs Mac Play follow-camera verify.

- **Bug audit (2026-09-08).** Dual commercials no longer occupy the same taxi
  segment (corridor lock + hold-short release + phase stall while blocked);
  priority crew / delay HUD focus any commercial at stand; departed flights
  release the runway immediately; wait monitor updates the blocked resource;
  commercial visuals remap by aircraft id across respawn reorder; approach gear
  deploys; cabin window frames no longer emit at night; UV-less meshes cannot
  keep textured materials; Addressables prefab loads are session-cached;
  `AircraftAssetTests` excluded from `test-domain.sh`. Evidence:
  `scripts/test-domain.sh` 118/118; probe same-taxi seconds 981→0.

- **AIR-001 v06 aircraft replacement.** Add a new deterministic high-wing regional
  turboprop with a continuous 40-segment fuselage, three-station NACA wing,
  lofted nacelles, swept/tapered empennage, six twisted propeller blades per
  engine, connected gear and cleaner glazing/livery details. Prefer v06 while
  retaining v05 and every older fallback. Correct the ASCII FBX metre declaration
  that made Unity import generated models at 1% scale, enforce outward winding,
  and keep UV-less meshes bright by omitting textures they cannot map. Evidence:
  deterministic FBX/glTF hashes; 15.09 m × 3.89 m × 10.70 m Unity bounds; packaged
  Mac overview/follow visual pass; `scripts/test-unity.sh` 126/126.

- **Fussy visual bug-fix (Bailey Play).** Remove opaque apron/stand stain decals
  (true transparent wear only); drop apron joint/slab and E/W fringe densify; gate
  stand-lead cubes when kit stops present; lift night exposure/ambient/fill/sun/
  floods; soften contact + aircraft ground shadows; denser runway edge Points;
  preserve FBX albedo maps in `ApplyPresentationMaterials`. Presentation only.
  Evidence: `scripts/test-domain.sh` 113/113.

- **Aircraft geometry (P1 audit items 6 + 8).** The wing, tailplane and fin are now
  lofted NACA sections instead of 12-triangle planks (`lofted_aerofoil`), and every
  wing-mounted part derives from one `WING` planform via `wing_station` /
  `wing_slab` instead of hard-coded constants. They had drifted badly: dihedral was
  added to the wing and the flaps, ailerons, spoilers, flap tracks, fairings and
  static wicks all stayed at their old flat-wing height, leaving 88–226 mm of clear
  air; the flap fairings and static wicks touched nothing at all. The main gear,
  441 mm below the wing and attached to nothing, moves to the nacelle station where
  the leg runs up inside the nacelle (ground contact unchanged). Also fixed two
  never-visible meshes: landing lights buried inside the engine intakes, and
  wheel/rim hubs fully enclosed by their tires. 4,356 → 4,788 triangles, mesh count
  unchanged at 163 so the Resources prefab is unaffected. The art generators are
  also runnable outside their original container again — the `/workspace` paths are
  now repo-relative and `export_fbx` falls back to the bundled ASCII FBX exporter
  when `assimp` is absent, instead of dying part-way and clobbering .meta GUIDs.
  Evidence: parts-touching-nothing 4 → 0, Unity resolves 163/163 mesh filters with
  0 missing, `scripts/test-unity.sh` 124/124. Look still needs a Mac Play verify.

- **Draw-call and material work (P1 audit items 4-5).** `AirsideMaterialLibrary`
  gains `CreateShared`, a memoised material used by both kit loaders — the art
  library's 2,662 meshes were each minting their own `new Material`, and one
  turboprop alone drops from 163 materials to 33 distinct (colour, SurfaceKind)
  pairs. Runtime tinting is untouched: it goes through `Renderer.material`, which
  clones per renderer. Enabled the GPU Resident Drawer (InstancedDrawing) with
  2% small-mesh culling on `PC_RPAsset`, set BatchRendererGroup Variants to
  `KeepAll` (required, and `m_BrgStripping: 1` is `StripAll`, not `KeepAll`), and
  turned off `m_RequireOpaqueTexture` — nothing under `Assets/` samples
  `_CameraOpaqueTexture`. Also committed the 225 `StreamingAssets` `.meta`
  sidecars that were never tracked, so clones stop starting dirty. Evidence:
  `scripts/test-unity.sh` 124/124, 0 compile errors, no GRD/BRG warnings. Still
  needs a Mac Play verify — batchmode does not exercise the resident drawer.

- **Visual + performance fixes (P1 audit).** Cockpit windscreens render as glass
  again (`InferFromMeshName` only matched the American spelling "windshield", so
  `windscreen_c/l/r` fell through to an opaque PaintedMetal default and discarded
  their 0.42 alpha); cabin window frames, windscreen pillars and the cockpit frame
  move to Metal (they matched the glass rule on the substring "window" and rendered
  translucent). Wet-surface materials are no longer re-applied every frame — the
  `paved` flag is cached at collect time and the apply loop is gated on `rainWetness`
  actually changing, so `ApplyWetness` stops toggling `_CLEARCOAT` /
  `_METALLICSPECGLOSSMAP` on every paved renderer every frame and the SRP Batcher
  is no longer invalidated continuously; keyword writes additionally no-op when the
  keyword already holds the requested state. Restored the six post-process effects
  `GAME.md` pins off (motion blur, panini, lens flare, depth of field, chromatic
  aberration, lens distortion) — they were left `active: 1` in the working tree.
  Fixed three malformed prefab GUIDs (29–31 hex chars instead of 32) that made Unity
  ignore the airfield fence/gate kit, terminal forecourt kit and Kingscote context
  terrain entirely, silently demoting all three to the greybox glTF fallback path;
  committed the four Batch F4 VFX prefab GUIDs Unity had regenerated. Evidence:
  `scripts/test-unity.sh` 124/124 EditMode tests pass, 0 compile errors, clean tree
  after a full Unity import.

- **Post-F visual polish.** Align Quality shadow distance with URP (PC 140 /
  Mobile 90); rain field stamps from VFX-003 kit; aircraft prefer VFX-002 heat
  kit; softer contact shadows; windsock fabric segment ripple; tighter overview
  camera; day/night sun/flood/fog soak; thinner bird silhouettes; ALS lateral
  bars from lighting-kit stems; layered cloud clusters; closer phase-aware
  follow framing; warmer hangar bay spill; BeaconHz for aircraft/aerodrome;
  forecourt parking sign + kerbs from PRP-003; WLD-004 coast/paddock accents;
  landside flood-kit streetlights; taxi centreline + REIL kit fixtures;
  threshold side-stripe z-fight gate; aerodrome beacon + threshold lamps prefer
  lighting kit; gate terminal canopy / window-glow / hangar-bay densify when
  kits already ship those parts; stand bay digits only via PlaceRunwayDigit at
  stand centres; wire ANM wheel/tire RPM constants; sync URP `_BaseColor` on
  contact shadows, night glow, airfield lights, clouds and ARFF; collect kit
  glass for dusk glow; GT props use PropRpmTaxi; shared service/heat/ALS/REIL
  pulse Hz; gate stand-box densify when stand_stop present; skip WLD ridge
  densify when terrain accents land; airside planter strip from PRP-003;
  hangar door gated on door_panel; chocks accept singular kit mesh; gate coast
  dune densify when WLD-004 terrain kit present; PC quality 4 cascades + High
  shadows + MSAA 2 + probes; overview framing toward terminal (46 m / 50° FOV);
  stronger soft sun shadows; fix orphaned UpdateCoastalMotion foam scale
  (Unity compile); hold-short wait pulse collects kit hold_short meshes;
  skip BuildStandMarking when stand_stop present; wet VFX + foam sync
  `_BaseColor`; WLD-004 accents own near horizon (skip far greybox hills);
  centralize windsock/flag/apron/bird life Hz; collect hangar glass_pane_* for
  night glow; thin apron joints/slabs; restrain jetty densify; scale up terrain
  accents; landside benches/trolleys prefer PRP-003; dropoff_bollard preferred;
  UI offer pulse + window flicker via AirsideReusableMotion; restore
  BirdOrbitHz/BirdFlapHz (tip-8 compile break); Flood/Star/Coast/ApronStride Hz
  centralized; WLD-004 thins outer paddock + skips relief mounds/ribbons;
  VEG-002 coast rocks; scaled landside kit streetlights; thin bay/overflow paint
  when forecourt kerbs present; dusk flood/landside/window flicker share rates;
  VEG kits thin far tree/scrub densify; coast scrub via PlaceShrub; UI-PNL-001
  light chrome on economy/speed; Toolkit hides Canvas when active; Cloudy soft
  fog + cloud umbra; horizon/sun/moon SetRendererColor; Cloudy/Overcast soft sun
  gloom; contact shadows SetRendererColor; light toast/save chrome; parked GA
  capped at 3 when prefab present; fuel farm kit skips cone/barrier densify;
  ARFF kit softens bay Point; coast boats capped at 3 with prefab; cloud bands
  thinned to 9×1–2 blobs; props kit thins cones/barriers/dollies/signs; night
  glow PointLights capped to hero + ≤6 panes; parked cars / CHR people / rain
  stamps thinned when kits present; decal SetRendererColor; speed chip ink on
  light chrome; overview framing ~44 m / 48° toward terminal; safety props
  thinned when kits present; softer night bloom + warmer dusk fill/midtones;
  lighting kit thins edge Points + taxi densify; wet kit thins puddles; PRP-003
  thins access/service paint; Toolkit status/ops/offer light chrome (UI-PNL-001)
  with Runway Ink body text; one silhouette belt loader when service kit present;
  flood/edge/taxi/obst silhouette fixtures; ALS 5 stations when lighting kit;
  overview FOV init 48°; dollies×2 with props kit; coast yaw/pitch/roll Hz
  centralized. Presentation only — simulation unchanged.
  Evidence: `scripts/test-domain.sh` 113/113.

- **Batch F4 motion / VFX / UI system icons.** Eight UI-ICO-005 system-control
  icons (play/pause/speed/follow/overview/audio/save) wired into Toolkit chrome;
  VFX-001…004 Resources + Art/VFX prefabs; touchdown smoke prefers kit; wet kit
  accent; `AirsideReusableMotion` extracts ANM phase rates. Presentation only —
  simulation unchanged.
  Evidence: `scripts/test-domain.sh`; art sync; `docs/art/prompts/batch-f4-motion-vfx-ui-2026-09-07.md`.

- **Batch F3 setting modules.** Authored eucalyptus + Kingscote scrub kits, modular
  fence/gate, terminal forecourt furniture, and context terrain accents; PlaceTree /
  PlaceShrub / BuildPerimeterFence / landside canopy / distant hills prefer kits with
  procedural fallbacks. Presentation only — operational geometry unchanged.
  Evidence: `scripts/test-domain.sh`; art sync; `docs/art/prompts/batch-f3-setting-modules-2026-09-07.md`.

- **Batch F2 turnaround vehicles + people.** Authored VEH-001/002/003 v05 (fuel truck
  oval tank, baggage tug+carts, apron bus with rounded nose), VEH-004 pushback tug v02,
  CHR-001 ramp-crew and CHR-002 passenger kits; PreferArtKit + PlacePerson wired;
  Resources pipeline-proof prefabs; StreamingAssets sync; bake menu updated. Soft-skip
  Addressables catalog init when `aa/settings.json` is missing. Presentation only —
  simulation unchanged.
  Evidence: `scripts/test-domain.sh`; art sync; `docs/art/prompts/batch-f2-vehicles-characters-2026-09-07.md`.

- **Batch F1 soak evidence.** Pre-rebuild soak ~16 min stable; post-rebuild packaged app (shallower roof) stayed up 35+ min continuous with no crash (RSS ~250 MB).

- **BLD-001 v05 roof silhouette.** Shallower ~4° dual-pitch roof / lower plant to better match REF-001; Mac FBX rebake. Packaged rebuild pending soak tip.

- **Batch F1 follow-up: stop mat_glass on shadows/clouds/VFX.** Ground/contact shadows, cloud volumes and umbras use Default Lit instead of mat_glass (was causing bright shaft artefacts). Evidence: test-unity 116/116; Mac rebuild.

- **Batch F1 follow-up: day light + glass blend.** Daytime sun/ambient raised for REF-readable overview;
  `mat_glass_v01` transparent blend/ZWrite fixed via MAT-001 menu. Evidence: test-unity 116/116; Mac rebuild.

- **Batch F1 follow-up: rain no longer uses mat_glass.** Translucent VFX (rain/smoke mist)
  stays on Default Lit instead of MAT-001 glass panes — fixes bright vertical shafts in storm.
  Evidence: `scripts/test-unity.sh` 116/116; `scripts/build-mac.sh` rebuilt packaged app.

- **Batch F1 BLD-001 v05 + MAT-001.** Authored regional terminal `mdl_terminal_regional_small_v05`
  (FBX + glTF + Resources bake; PreferArtKit v05 first) and eight URP Lit materials
  `mat_{asphalt,concrete,grass,corrugated_metal,glass,painted_line,aircraft,wet}_v01`.
  Prefab instantiate re-applies presentation materials. Fallbacks retained; no simulation change.
  Evidence: Unity bake 12 prefabs; MAT-001 CreateMaterials ×8×2; `scripts/test-unity.sh` 116/116;
  packaged Mac build launched; soak notes in GAME handoff.
  F2–F4 not started.

- **Save/replay and insolvency bug audit.** Preserve player order for same-second commands
  (including legacy IDs), assign unique IDs to new commands, retain the valid backup after
  recovery writes, reject unsupported schemas before migration, and stop traffic immediately
  when a midnight close declares insolvency. No schema fields changed; schema-1 migration stays
  supported. Unity 6000.3.23f1 EditMode: 124/124; the pre-fix run reproduced six failing cases
  across five defects. Universal arm64/x86_64 Mac build passed. See `docs/testing/BUG_AUDIT_2026-09-07.md`.

- **Batch F1 AIR-001 v05 Mac bake.** Unity ModelImporter meshes now in Resources prefabs for
  `mdl_regional_turboprop_01_v05` plus the authored FBX kits from the same bake menu; Bailey
  accepted the AIR-001 v05 result. No standalone `.mat` files (materials stay FBX sub-assets).
  BLD-001 v05 / MAT-001 / F2–F4 not started.
  Evidence: Unity bake 11/11 prefabs, 32 Addressables keys; `scripts/test-unity.sh` 116/116
  on Unity 6000.3.23f1. `scripts/test-domain.sh` not available on this Mac (no .NET SDK).

- **Batch F1 AIR-001 v05.** Authored `mdl_regional_turboprop_01_v05` FBX + companion glTF (163 meshes:
  oval lathe, tilted windscreen, six-blade props with yellow tips, separated gear/doors/surfaces/lights)
  and Resources prefab; PreferArtKit prefers v05 ahead of authored/lofted/v04; pipeline-proof Resources
  yield to StreamingAssets glTF until Mac FBX bake. BLD-001/MAT-001/F2–F4 not started.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 173; `docs/art/prompts/air-001-v05-turboprop-2026-09-07.md`.

- **Decision 0027 / Batch F visual asset gap closure.** Audited the complete repository,
  runtime presentation builders, approved references and current asset/register state.
  Added an implementation-ready ordered asset packet: authored turboprop/terminal/materials
  first, then turnaround people/vehicles, setting modules and reusable motion/VFX/UI
  finish. Exact paths, prefab keys, pivots, texture/LOD budgets, fallbacks and packaged
  Mac acceptance checks are defined; no runtime or simulation behaviour changed.

- **Terminal densify tip (0025 item 2).** Authored terminal kit 191 (extra curtain panes, landside
  ribs, canopy braces/lights, boarding/service glass, girth bands); color map uses StartsWith
  for glass/ribs. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 171.

- **Hangar/ops densify tip (0025 item 2).** Authored hangar kit 165 / ops shed 127 (extra ribs,
  corner trims, door peeks, awnings, flashings, porch/AC detail); color maps cover new metal/
  glass parts. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 171.

- **Wet/terrain densify tip (0025 items 3–4).** Residual damp covers markings-kit paint + relief
  mounds (0.22 clear-day); stronger wet darken/spec; more terrain mounds, grass ribbons, dune
  crests; scrub clumps to 5 spheres. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **HUD/day/fence/tree densify tip (0025 items 3–6).** Toolkit panels denser brand chrome +
  coastal top edge; ChannelMixer day profiles + golden-hour bloom; eucalyptus flare/fork/5
  canopies; fence top wire + post caps; densify stairs (~28) / pushback tug (~20) / cone /
  barrier Resources fallbacks. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Service GSE / belt-loader / ALS tip (0025 items 2–5).** Fix belt loaders to use service
  kit (were cube fallbacks via wrong props kit); prefer denser stairs/GPU/chocks kit over thin
  Resources prefabs; densify towbar/FOD bin kit parts; ALS stations + REIL reuse lighting kit;
  apron-safety hydrant/cabinet/FOD + chocks/GPU Resources densify; wet collect for markings-kit
  node names + relief mounds. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Lighting/props/jetty/flap tip (0025 items 2–5+7).** Wire unused authored lighting densify
  into PlaceFlood/Edge/Taxi/Obst; apron cone/barrier/sign/dolly prefer denser kit over thin
  prefabs; windsock kit fabric+guys; NestFlapParts; jetty planks/rails/bollards; wet for
  jetty/fuel/ARFF/canopy/shoulders; markings centre dashes/threshold bars/apron arrows;
  landside bench/trolley densify. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Hangar bay densify + marking wet collect (0025 items 3–4).** Hangar bay props ~49
  parts (vise, shelves, tires, extinguisher, pegboard); wet/residual damp covers taxi
  arrows, runway digits, stand leads, chevrons, hold-shorts. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Markings/ARFF/fuel densify (0025 items 1–3).** Markings kit 44 (digit bars, taxi
  arrows, extra hold/chevrons) wired into PlaceWorldMarkings/PlaceRunwayDigit; fuel
  farm ~47 / ARFF shed ~52 / truck ~53 Resources parts. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 171.

- **MAT-001 maps + wet/motion/env tip (0025 items 3–5+7).** Per-kind glass/rubber/
  painted-line/plastic PBR companions + procedural fallbacks/default tiling; wet
  `_BaseColor` sync; landside Access/Overflow/zebra wet collect; flap exact L/R;
  cargo bags nest under Cargo; cabin Door frame nests; fence/gate/ALS densify;
  coast boat ~16 parts; dusk midtone/WB polish. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 171.

- **PaintedLine + landside/motion fixes (0025 items 3–4+7).** Near-white primitives map
  to `PaintedLine` (not aircraft skin); access-turn markings/shoulders + overflow bay
  chevrons; hose mount/nozzle no longer stretch with hose; cabin/bus door parts nest
  for animation; wheel_arch excluded from wheel spin. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Service/props densify + hangar color maps (0025 item 2).** Service equipment 103
  (stairs/GPU/belt hubs); props 65; hangar color maps cover fascia/ribs 9–10.
  Presentation only. Evidence: `scripts/test-domain.sh` 105/105; art sync 159.

- **Hangar/GSE densify tip (0025 item 2).** Hangar 149 (fascia/office roof/crane rails/
  extra ribs); fuel truck 87; baggage tug 88 (hubs, bags, hitch pins). Presentation
  only. Evidence: `scripts/test-domain.sh` 105/105; art sync 159.

- **Terminal/ops/bus + day/wet/HUD tip (0025 items 2+4–6).** Terminal shell densify
  (157: fascia/soffit/ribs); ops shed 111; apron bus 90; noon midtone punch + dusk
  WB warmth; stronger non-ClearCoat wet sheen; Toolkit income/research/route icons.
  Presentation only. Evidence: `scripts/test-domain.sh` 105/105; art sync 159.

- **Turboprop/ARFF/fuel/HUD densify tip (0025 items 1–2+4+7).** Authored turboprop
  131 meshes (window frames, flap tracks, oil coolers, oleos/rims, spinner stripes);
  cabin glass alpha 0.42; lighting kit 55; markings kit 28 + PlaceWorldMarkings
  hold/aiming/TDZ/taxi-edge kit wiring; ARFF shed/truck ~36–37 parts; fuel farm ~31;
  Toolkit turnaround service icons. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Airfield props densify (0025 item 2).** Props kit dolly bags/posts, windsock fabric
  segments, barrier braces, sign glyphs (~55 meshes). Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Parked car densify (0025 items 1+3).** `mdl_parked_car_v01` ~32 parts (split glass,
  mirrors, grille, wheel arches, hood/boot); body tint covers door/hood/arch;
  headlight/taillight binder colors. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Parked GA densify + fuselage binder fix (0025 items 1–2).** `mdl_parked_ga_v01`
  ~40 parts (canopy glass, struts, spinner, gear scissors/wheels); runtime binder
  maps fuselage/nose to baseColor AircraftSkin; canopy→Glass. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Hangar glass, service densify, day/wet polish (0025 items 2+4+5).** Hangar side/office/
  skylight glass panes + mullions (132); stairs posts/GPU grille/belt rollers (80);
  stronger noon midtone separation + dusk WB warmth; residual damp on roads/markings;
  non-ClearCoat wet sheen bump. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **GSE/ops densify, landside panes, mullion tint fix (0025 items 2+4+7).** Ops shed
  cladding ribs + split glass panes (94); fuel truck tank ends/pump/cab panes (75);
  baggage tug cart beds/bags/canopies (68); apron bus side glass_pane* (72);
  terminal landside curtain panes (126); GSE/ops mullions metal not glass-blue;
  wet collect includes runway/taxi/stand markings. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Scrub carpet, dusk floods, glass/apron wet, terminal interior (0025 items 2–5+7).**
  Fence-line multi-sphere scrub carpet; URP additional lights/object 12 + warmer
  dusk flood punch with soft shadows on mast floods; glass_pane* get glass masks;
  apron slab overlays + clear-weather residual damp; terminal interior desks/
  chairs/figures (110 meshes); irregular multi-blob puddles; fringe/slab wet
  collect. Presentation only. Evidence: `scripts/test-domain.sh` 105/105; art sync
  159 files.

- **Terminal panes, hangar ribs, apron fringe, belt loaders, HUD chrome (0025 items 2+3+6).**
  Curtain-wall glass panes + proud mullions + interior glow; hangar corrugation
  ribs/girth bands (90 meshes); apron fringe/planters/stand stains; belt loaders
  on service kit (60); Toolkit METAR row, reputation bar, strip dividers, more
  translucent panels; concrete bump raised. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Materials, tapered props, day grade, trees, wet fixes (0025 items 2–5+7).**
  URP Lit AO strength uses profile occlusion; Glass/Water force alpha panes; wet
  disables metallic-gloss mask so sheen reads; turboprop 3× tapered blades (104
  meshes); REF-001 muted day sky + warmer dusk WB; eucalyptus 3-canopy clumps;
  Stand 3 pad and apron joints join wet collect. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Taxi blue, tall floods, apron joints, HUD bars, hi-vis (0025 items 3+5+6+7).**
  Taxi edge/point lights are blue (REF-002); airfield lighting kit floods ~9 m with
  multi-head lamps (41 meshes) and SpotLights synced to mast height; apron
  expansion-joint grid; Toolkit turnaround progress bars + slim economy strip;
  hi-vis ramp crew with marshaller wands. Presentation only (Progress01 on
  TurnaroundTaskView is HUD-facing). Evidence: `scripts/test-domain.sh` 105/105;
  art sync 159 files.

- **Stand lead dashes + calm coach + ALS lenses (0025 items 3+5+6).** Apron stand
  lead-in dashes for bays 1–3; ALS lenses emissive; Toolkit coach only when urgent.
  Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **URP volume + shadow/SSAO polish (0025 item 5).** Deactivate template
  DefaultVolumeProfile junk (DoF/motion blur/lens/test components); PC shadow
  distance 140; SSAO intensity/radius raised for apron contact. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Markings, gear nest, ALS spots, HUD time controls (0025 items 3+5+6+7).**
  Stand digits 1/3 paint correctly; markings kit edges/threshold/taxi/stops used;
  gear scissors/tires nest under struts; ALS/REIL SpotLights wash approach;
  Toolkit economy strip shows research; Pause/1×/4× buttons. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Landside + fence densify (0025 item 3).** Parking bay lines, access dashes,
  extra cars, inland scrub belt, E/W fence mid-posts and corner braces.
  Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **Env motion + day-volume polish (0025 items 3+5+7).** Apron walkers orbit
  spawn bases (no teleports); coast water UV scroll; weather thickens cloud alpha;
  UnlitSky uses URP Unlit; day volume owns WhiteBalance/SplitToning; puddle sheen
  without ClearCoat. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Night lighting + wet fallback + Toolkit HUD REF-004 layout (0025 items 4–7).**
  GSE SpotLights aim along kit +X; fuel truck gains bumper headlights (51 meshes);
  cabin glow skips frames; entrance doors no longer Glass; wet sheen without ClearCoat;
  Toolkit HUD: translucent panels, ops top-left, economy strip, bottom speed chip,
  Batch E phase/weather/cash icons. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Presentation wiring bug fixes (0025 items 4+5+7).** GSE rename map no longer
  collapses cart_* into Cargo; gear scissors no longer pitch with struts; prop
  hubs/spinners nest under propellers; duplicate aircraft lamps skipped when kit
  ships them; terminal glass panes vs metal frames; wet/ALS Collect covers densified
  names; day ambient uses Trilight. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Ops/props densify + headlight bugfix (0025 items 2+3+7).** Ops shed 53,
  airfield props 43, lighting 33 meshes; layered coast foam pulse; service-vehicle
  headlights no longer share beacon orange. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Vehicles + apron life + URP dry retune (0025 items 2–4+7).** Fuel truck 47,
  baggage tug 45, apron bus 46, service kit 48 meshes; denser apron figures,
  birds (28), vegetation belt; dry asphalt/concrete/skin/metal profiles retuned
  for stronger wet contrast. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Hangar densify (0025 items 2+7).** Authored hangar to 69 meshes (extra door
  bars/warnings, plinth, louvres, gutters, workbench/tool cabinet); door motion
  collects new bars/warnings. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Terminal densify + HUD chrome (0025 items 2+6+7).** Authored terminal to 73
  meshes (doors, boarding gate, canopy braces/lights, plinth, vents, flag);
  toolkit status/ops panels gain Open Sky top accent; save chip coastal bar;
  terminal flag flap motion. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Turboprop + coast/wet/day tip (0025 items 2–5).** Authored turboprop to 102
  meshes (spoilers, pylons, wicks, gear scissors, denser lathe); coast foam
  layers, six boats, rock outcrops, runway shoulders, 22 clouds; wet response
  deeper specular/AO + more puddles; stronger golden-hour day volume. Presentation
  only. Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **ALS / ARFF / GA densify (0025 items 3+5).** Approach light fan to 8 stations with
  crossbars + far REIL; ARFF shed gains roof ridge/door ribs/hose/hydrant; five
  denser parked GA with gear/struts and contact shadows. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **URP profile retune + apron life (0025 items 4+7).** Drier asphalt / richer
  aircraft-skin & glass dry profiles; denser apron figures; flood mast and dolly
  cluster contact shadows. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Fence densify + ops HUD chrome (0025 items 3+6).** West/east/south perimeter
  fences gain bottom rails and denser mesh posts; extra terrain mounds; OPERATIONS
  panel gets coastal accent + Open Sky title. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Service vehicles densify + puddles (0025 items 2+4).** Fuel truck 33, baggage
  tug 34, apron bus 35 authored meshes (fenders, rails, arches, lights); vehicle
  material map covers new parts; wet puddle count raised across apron/landside/
  fuel pad. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Terminal/hangar/turboprop densify (0025 items 2+3).** Authored terminal to 49
  meshes (extra mullions/sills/canopy/columns), hangar to 53 (door bars/handles,
  crane hook, skylights, downpipes), turboprop to 78 (mid fences, pitot, VOR).
  Denser TDZ marks, stand digits 1–3, taxi centreline dashes. Hangar door motion
  includes new bars/handles. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Ops/props densify + atmosphere (0025 items 2–5+7).** Authored ops shed 40,
  service equipment 42, lighting 28, props 34 meshes; presentation places denser
  stairs/GPU/cone/barrier/sign/dolly/flood parts. Night star field (72) + ops
  antenna sweep; wet AO deepen + stronger golden-hour day volume; landside overflow
  cars and denser bush belt. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; art sync 159 files.

- **Service vehicles densify (0025 item 2).** Authored baggage tug and apron bus gain
  rails/cart wheels, mullions, arches and stripe detail.
  Evidence: `scripts/test-domain.sh`.

- **Hangar + fuel truck densify (0025 item 2).** Authored hangar to 38 meshes
  (door bars, skylights, crane trolley, extra columns); fuel truck to 18 meshes
  (chassis, hose, denser tank). Door bars slide with hangar panels. Presentation
  only. Evidence: `scripts/test-domain.sh`; art sync 159 files.

- **Denser authored turboprop/terminal + sky discs (0025 items 2+5).** Hero
  turboprop regenerated to 74 meshes (more lathe stations, cabin windows,
  wing fences, prop hubs); terminal gains extra mullions/transom. Runtime sun
  and moon discs track the day cycle; cloud count 16; cylindrical fuel tanks +
  more contact shadows. Generator preserves Unity .meta GUIDs on regenerate.
  Evidence: `scripts/test-domain.sh`; art sync 159 files.

- **Toolkit HUD readability (0025 item 6).** Status panel gains a coastal accent
  bar and stronger type hierarchy (location/phase Open Sky, cash bold, coach
  urgent yellow vs calm Open Sky). Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Night glow flicker + hold-short polish (0025 items 5+7).** Building window
  PointLights and emissive quads flicker softly at dusk; hold-short bars C/D join
  the traffic-wait pulse with emission; touchdown-zone marks added beside aiming
  points. Presentation only. Evidence: `scripts/test-domain.sh`.

- **Runway/taxi markings + fence densify (0025 item 3).** Continuous runway edge
  stripes, taxi edge lines, apron lead-in chevrons, mid-span fence posts and gate
  chevrons. Coast boat bob no longer drifts yaw. Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Stand GSE deploy + hangar door dedupe (0025 item 7).** Stairs roll in from
  the apron edge before pitching up; chocks settle with a roll; when authored
  hangar door panels are present the greybox slab is hidden so doors do not
  double up. Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **Hangar kit doors + coastal motion (0025 items 3+7).** Authored hangar
  `door_panel_*` / `door_rib_*` slide with the greybox slab (opens for day and
  active stand traffic); coast boats bob, foam pulses, jetty breathes.
  Presentation only. Evidence: `scripts/test-domain.sh`.

- **Wet materials + day profiles + coast densify (0025 items 3–5).** URP Lit wet
  variants darken albedo, flatten bump, raise gloss and enable clear-coat sheen;
  day volume adds ShadowsMidtonesHighlights + rain/fog/storm gloom; coast foam
  ribbon, service-lane markings and southern headlands. Tire binder no longer
  paints wheels as metal before Rubber. Presentation only.
  Evidence: `scripts/test-domain.sh`.

- **Authored airfield props kit.** `mdl_airfield_props_kit_authored_v01` (cylindrical
  poles/cones/dolly wheels) preferred ahead of v02/v01 for PlaceWorldProps.
  Evidence: `scripts/test-domain.sh` 105/105.

- **Apron lighting + probes for authored kits (0025 item 5).** Apron floods are
  SpotLights aimed at stands/hangar/terminal; terminal gets its own realtime
  reflection probe; apron probe box expanded. Authored airfield lighting kit
  (`mdl_airfield_lighting_kit_authored_v01`, cylindrical poles/lenses) preferred
  ahead of v02/v01. Presentation only. Evidence: `scripts/test-domain.sh` 105/105.

- **Authored PRP-001 service equipment kit.** `mdl_service_equipment_kit_authored_v01`
  (stairs/chocks/GPU extract names + denser rails/treads/wheels) preferred ahead of
  v02/v01 for stand GSE mesh extraction. Evidence: `scripts/test-domain.sh` 105/105.

- **Material binder fix for authored kits.** `AirsideRuntimeMaterialBinder` no
  longer treats every name containing `cabin` as glass (CabinDoor was wrong);
  fuselage/wing/tire kinds route correctly; InferFromMeshName covers authored
  mullions, canopy posts, hangar door panels and vehicle tanks. Presentation only.

- **Authored service vehicles (VEH-001…003).** Fuel truck (cylindrical tank), baggage
  tug train and apron bus ship as `*_authored_v01` FBX + glTF + Resources prefabs
  with PreferArtKit ahead of v04. Completes the Batch C first-playable model set
  on the authored track. Evidence: `scripts/test-domain.sh` 105/105.

- **Authored ops shed + full building Resources set.** `mdl_operations_shed_authored_v01`
  joins turboprop/terminal/hangar with FBX + glTF + Resources prefab so
  `airside-prefab/*_authored_v01` covers AIR-001 and BLD-001…003. PreferArtKit
  prefers authored. Evidence: `scripts/test-domain.sh` 105/105; art sync updated.

- **Authored Resources prefabs + hangar kit.** Addressables keys
  `airside-prefab/mdl_regional_turboprop_01_authored_v01`,
  `…/mdl_terminal_regional_small_authored_v01`, and
  `…/mdl_hangar_small_authored_v01` resolve via Resources (round fuselage /
  canopy / hangar columns; gear/cargo/prop names for motion). FBX sources +
  companion glTF remain; ModelImporter bake can overwrite later. PreferArtKit
  prefers authored → prior kits → primitives. Evidence: `scripts/test-domain.sh`
  105/105; art sync 145 files.

- **Authored FBX turboprop + terminal (0025 item 2).** Distinct
  `mdl_regional_turboprop_01_authored_v01` and
  `mdl_terminal_regional_small_authored_v01` ship as Unity-importable `.fbx`
  (lathed fuselage / cylindrical engines; canopy posts) plus companion glTF for
  StreamingAssets. PreferArtKit prefers authored → lofted/v04 → … → primitives.
  Mac menu **Airside → Art → Bake Authored FBX Prefabs** can replace Resources
  prefabs with ModelImporter meshes. No simulation change.
  Evidence: `scripts/test-domain.sh`; art sync 143 files.
- **Motion, brand overlays, wet/coast polish (0025 items 4–5+7–8).** Gear doors
  animate separately from struts; cargo doors open at stand; soft coastal ambient
  audio joins wind/rain; wet response covers more ground surfaces and refreshes the
  apron reflection probe; night bloom/grain capped to avoid smear; Toolkit pause /
  away / insolvency overlays use the BRD-001 wordmark. Presentation only.
  Evidence: `scripts/test-domain.sh` 105/105; `scripts/test-unity.sh` 116/116;
  Play soak: gear/cargo doors in hierarchy, BRD-001 wordmark on HUD + 3 overlays.

- **Addressables Resources provider + lofted turboprop + env densify (0025 items 1–3+6).**
  Runtime `airside-prefab/<key>` keys now load via `AirsideResourcesProvider`
  (`Resources.Load`) instead of the missing LegacyResourcesProvider stub — StreamingAssets
  glTF and direct Resources fallbacks stay intact. Hero aircraft prefers distinct
  `mdl_regional_turboprop_01_lofted_v01` (79 stepped-fuselage meshes; does not race
  `*_v04`). South fence, denser vegetation belt, access-road shoulders; Toolkit HUD
  early-outs residual IMGUI when active. Evidence: `scripts/test-domain.sh` 105/105;
  `scripts/test-unity.sh` 116/116; Play soak loaded lofted kit, denser env, Batch C/WLD/PRP.

- **Unity 6.3 HUD startup: font + PanelSettings theme.** Editor Play threw
  `ArgumentException: Arial.ttf is no longer a valid built in font` while building
  the Canvas HUD; the packaged player logged `No Theme Style Sheet set to
  PanelSettings` for the runtime Toolkit UIDocument. Canvas text now loads
  `LegacyRuntime.ttf`, and Toolkit PanelSettings assigns
  `Resources/Airside/UI/AirsideRuntimeTheme.tss` (imports Unity's default theme).
  Play soak 2026-09-07: zero Arial / PanelSettings warnings. No simulation or art-path changes.

- **Verified decision 0025 packaged-art delivery on a real macOS build.** From clean
  `main` at `33a961a`, the art sync copied 137 files without repository drift,
  Unity 6000.3.23f1 passed 116/116 EditMode tests, and the Mac build succeeded.
  Every built StreamingAssets art file matched its source hash; the player visibly
  used Batch C model geometry, WLD/PRP lighting and markings, and Batch B aircraft
  surfaces instead of primitive fallbacks. Full Editor/player parity and Batch E
  remain unverified because Editor Play throws on removed built-in `Arial.ttf` at
  `AirsideCanvasHud.cs:1000`; no art-path missing-file error was logged.

- **Anti-aliasing and the template post-processing profile.** The game shipped with
  no anti-aliasing at all: `PC_RPAsset` had `m_MSAA: 1` and nothing set camera
  antialiasing — on a world made entirely of hard box edges and thin poles. MSAA is
  now 4x and the camera runs SMAA (high). Separately, Unity's template
  `DefaultVolumeProfile` is still wired as URP's global default and overrides
  DepthOfField, MotionBlur, LensDistortion, ChromaticAberration, ScreenSpaceLensFlare
  and PaniniProjection (alongside literal `CopyPasteTestComponent1/2/3` and
  `TestVolume`). Harmless while post-processing was off; rendering since
  `AirsideDayVolume` switched the post stack on. Each is now pinned to its no-op value
  in the day volume's own profile. Film grain is untouched — that one is the day
  volume's, and stays day-driven.

- **`runInBackground`.** A real-time, persistent simulation froze whenever the window
  lost focus — clock, flights and economy all stopped mid-session.

- **Four simulation bugs that contradicted the design brief.** Ground traffic taxied
  to and parked on Stand 3 before the player built it (`AlternateStand` walked
  Stand 1/2/3 with no knowledge of `Capacity.StandCount`, so with both baseline
  stands occupied GT-201 reserved `LEAD-IN-3` and parked on bare ground); it now
  holds off-field, leaving the corridor free, when every built stand is taken.
  Understaffing delays were reported with an empty cause, against the brief's "every
  delay should have an understandable cause" — `TurnaroundWorkflow.OverrunCause` now
  names cleaning disruption, understaffing or a generic extended turnaround. The
  daily finance brief halved the moment a second commercial started operating,
  because the projection assumed one aircraft. And a player command issued at
  simulation second 0 was dropped on reload — `ReplayTo` advances the clock then
  applies commands at the new second, so it never visited second 0, replaying the
  cash it cost but not its effect. Eight new EditMode tests, each verified failing
  against the previous code.

- **Fixed the main build.** `AirsidePrefabAddressables` implements `IResourceLocator`
  but did not import `UnityEngine.AddressableAssets.ResourceLocators`, so every
  EditMode run and player build had been failing with CS0246 since PR #97.

- **Aircraft skin PBR + wider livery coverage (0025 item 4).** Authored
  `tx_aircraft_skin_*` maps for AircraftSkin materials; livery decals cover
  segmented fuselage/nose parts on denser turboprop kits. Presentation only.

- **Denser WLD/PRP kits v02 (0025 item 2).** Prefer `*_v02` airfield lighting
  (multi-part flood masts, edge/taxi/obst), props and service equipment kits over
  thin v01 Batch B boxes. Presentation only.

- **ARFF shed prefab + textured distant hills (0025 items 1+3).** Resources
  `mdl_arff_shed_v01` replaces flat rescue-shed blocks; distant hills are segmented
  and grass/sand-mapped so the horizon is not four unlit slabs. Presentation only.

- **Apron safety props + stand boxes (0025 items 1+3).** Resources
  `mdl_fire_hydrant_v01`, `mdl_extinguisher_cabinet_v01`, `mdl_fod_bin_v01` on the
  apron edge; painted stand bay boxes for stands 1–3. Presentation only.

- **Coast sand + water PBR surfaces (0025 item 4).** Authored `tx_sand_coast_*` and
  `tx_water_coast_*` basecolour/normal/AO/mask maps on coast strip and dunes;
  material library wires Sand/Water stems. Presentation only.

- **Landside furniture + coast boat prefabs (0025 items 1+3).** Resources
  `mdl_luggage_trolley_v01`, `mdl_landside_bench_v01`, `mdl_coast_boat_v01`; denser
  car-park stall lines, kerbs, drop-off zebra and parking sign. Presentation only.

- **Landside parked-car Resources prefab (0025 items 1+3).** `mdl_parked_car_v01`
  fills car-park bays and kerbside drop-off (tinted body colours; Addressables key
  auto-registered). Presentation only.

- **Denser runway edge lights + REIL blink (0025 items 3+5).** Edge PointLights every
  8 m, more taxi centreline lamps, REIL flashers at both thresholds, and a second
  hold-short pair. Presentation only.

- **Batch C v04 denser hero kits (0025 item 2).** Prefer `*_v04` turboprop (68 meshes:
  winglets, gear doors, spoilers, window panes), terminal/hangar/ops and service
  vehicles; spoilers deploy on landing. Still procedural greybox. Presentation only.

- **Readable runway threshold digits (0025 item 3).** Block-style 09 / 27 markings plus
  side threshold stripes so runway ends read from overview/follow. Presentation only.

- **Terminal landside canopy (0025 item 3).** Steel posts, soffit slab, landside glass
  curtain, entrance doors, bench/planter and dusk canopy under-glow so the terminal
  entrance reads from landside and overview. Presentation only.

- **ALS chase flash + ARFF lightbar blink (0025 items 5+7).** Approach lamps run a
  far-to-threshold sequence at night; ARFF truck lightbar pulses amber/red at dusk.
  Presentation only.

- **ARFF truck Resources prefab (0025 items 1+3).** `mdl_arff_truck_v01` parks on the
  rescue apron in front of the ARFF shed (Addressables key auto-registered).
  Presentation only.

- **Perimeter fence, ALS bars + ARFF shed (0025 items 3+5).** Chain-link style multi-rail
  fence with open vehicle gate; five approach light bars west of threshold with night
  PointLights; red ARFF rescue shed + bay spill. Presentation only.

- **UI Toolkit overlays (0025 item 6).** Runtime `AirsideToolkitHud` owns briefing
  (dawn splash), pause, away summary and insolvency overlays with action buttons;
  Canvas HUD is now a fallback only when Toolkit fails to build. Presentation only.

- **UI Toolkit left status panel (0025 item 6).** Runtime `AirsideToolkitHud` now owns
  the full left status/decision panel (cash, turnaround, crew, stands, research,
  coach, wait meter) with action buttons; Canvas keeps overlays only and hides its
  left copy. Presentation only.

- **UI Toolkit OPERATIONS + route offer (0025 item 6).** Runtime `AirsideToolkitHud`
  now owns the OPERATIONS panel and Accept/Decline route card (pulse accent, daily
  report); Canvas keeps left status + overlays and hides its right-column copies.
  Presentation only.

- **Hangar bay props + runway aiming points (0025 items 1+3).** Resources prefab
  `mdl_hangar_bay_props_v01` (workbench/shelves/drum/cart) fills the open hangar;
  white aiming-point pairs on the runway. Presentation only.

- **UI Toolkit toasts + aircraft nav/beacon lights (0025 items 5+6+7).** Runtime
  `AirsideToolkitHud` (UIDocument) owns ops/research toasts and the Saved chip;
  Canvas keeps panels/overlays. Wingtip nav and anti-collision beacon cast real
  PointLights. Presentation only.

- **Addressables-first Resources locator (0025 item 1 / ADR 0026).** Runtime
  `AirsidePrefabAddressables` exposes every `Resources/Airside/Prefabs` asset under
  `airside-prefab/<key>` via LegacyResourcesProvider; loader prefers Addressables
  then Resources then glTF. Presentation only.

- **Landing skids + window PointLights + wet taxi spray (0025 items 5+7).** Rubber
  skid streaks fade after touchdown; denser smoke puffs; terminal/hangar/ops window
  PointLight spill at dusk; amber fuel-farm lamp; mist spray under gear when wet.
  Presentation only.

- **Parked GA prefab + denser apron life (0025 items 1+7).** Resources prefab
  `mdl_parked_ga_v01` (three parked GA on the west apron); more staff/passenger
  silhouettes with walker shuffle, marshaller arm wave and stride. Presentation only.

- **Phase-aware follow camera + fuel farm prefab + night windows (0025 items 1+5+7).**
  Follow framing/FOV lean into taxi, stand, approach, landing and takeoff; Resources
  prefab `mdl_fuel_farm_v01`; stronger terminal/hangar/ops window emission at dusk.
  Presentation only.

- **Sign/dolly/windsock prefabs + cabin glow + GSE headlights (0025 items 1+5+7).**
  Resources prefabs for airside sign, baggage dolly and windsock pole; denser apron
  placement; cabin/cockpit emissive at night/stand; fuel/baggage/bus/tug headlamp
  SpotLights. Presentation only.

- **Apron GSE prefabs + aircraft landing SpotLights (0025 items 1+5+7).** Resources
  prefabs for pushback tug, safety cone and work barrier (denser apron placement);
  approach/landing/takeoff landing lamps and night taxi lamps cast real SpotLights.
  Presentation only.

- **GSE prefabs + wet apron puddles (0025 items 1+4+7).** Resources prefabs for wheel
  chocks and GPU cart; soft reflective puddle discs appear on wet weather. Presentation only.

- **First Resources prefab + Addressables try (0025 item 1).** `mdl_passenger_stairs_v01`
  lands under `Resources/Airside/Prefabs/` with runtime Lit binder; loader probes
  `airside-prefab/<key>` Addressables before glTF. Presentation only.

- **Bird wing flaps (0025 item 7).** Coastal flock rebuilt as body + hinged wing quads
  (18 birds) with flap animation; denser orbit over the shore. Presentation only.

- **Runway edge PointLights (0025 item 5).** Sparse warm point lights every 12 m along
  both runway edges plus green taxi centreline hints; day-cycle intensity. Presentation only.

- **Cloud umbras + building contact shadows (0025 items 3+5).** Soft ground discs drift
  under cloud bands; terminal/hangar/ops/car-park contact blobs ground Lit surfaces;
  night film grain on the day Volume. Presentation only.

- **Denser landside vegetation (0025 item 3).** More eucalyptus belts, dual-canopy trees,
  shrub clusters and a tighter coastal scrub strip so overview reads as KI bush, not a
  sparse prop ring. Presentation only.

- **Canvas research toast + save indicator (0025 item 6).** Research-complete banner
  (top), ops toast (bottom) and Saved chip move onto runtime uGUI; IMGUI gated when
  Canvas is active. Presentation only.

- **Threshold approach lights + apron reflection probe (0025 item 5).** Point lights at
  both runway ends plus a compact PAPI ladder; realtime apron ReflectionProbe so wet
  Lit surfaces pick up floods at night. Presentation only.

- **Batch B surface PBR maps (0025 item 4).** Authored normal / AO / metallic-smoothness
  masks for asphalt, concrete, grass and corrugated metal; `AirsideMaterialLibrary`
  prefers them via StreamingAssets with procedural fallbacks. Presentation only.

- **Batch C v03 denser kits (0025 item 2).** Procedural turboprop/terminal/hangar/ops/
  GSE kits with more segmented parts; runtime prefers v03→v02→v01; flaps/ailerons/
  elevators animate. Still greybox — not authored meshes. StreamingAssets synced.

- **Canvas away + insolvency overlays (0025 item 6).** Welcome-back summary and
  insolvency cards move onto runtime uGUI; IMGUI duplicates gated when Canvas is
  active. Presentation only.

- **Prefab/Addressables art loader scaffold (0025 item 1–2 / ADR 0026).**
  `ArtPresentationLoader` prefers `Resources/Airside/Prefabs/<kit-basename>` then
  StreamingAssets glTF; Addressables package added to the Unity manifest; wet
  material variants centralized; Canvas owns opening briefing + pause overlays.
  Presentation only.

- **Terrain micro-relief (0025 item 3).** Grass berms, scattered mounds and coastal
  dunes break the flat ground slab so overview reads as a regional airfield site.
  Presentation only.

- **Atmospheric fog + apron life figures (0025 items 5+7).** Soft exponential fog on
  clear days (weather still thickens it); seven stylised staff/passenger silhouettes
  on apron and landside with idle lean and marshaller wave on approach. Presentation only.

- **URP day volume + richer GSE motion (0025 items 5+7).** Runtime global Volume with
  ACES tonemap, day-driven color/exposure, bloom and vignette; service vehicles park
  on the apron and drive into stand tasks; stairs/chocks deploy; GPU and beacons pulse.
  Presentation only — not a full probe bake.

- **Hangar bay interior light.** Warm point light inside the hangar brightens with
  the sliding door by day and keeps a soft night glow when closed. Presentation only.

- **Canvas OPERATIONS panel (0025 item 6).** Runtime uGUI hosts the OPERATIONS log
  (routes, ground traffic, event tail) and daily report under the route offer;
  IMGUI ops panel gated when Canvas is active. Presentation only.

- **Canvas left HUD migration (0025 item 6).** Runtime uGUI left panel now owns
  status, coach, wait meter, turnaround tasks, and hire / release / stand /
  research / priority-crew buttons; IMGUI left panel is gated when Canvas is
  active. Operations log remains IMGUI. Presentation only.

- **Sky bird flock + hangar field restore.** Twelve presentation birds orbit the
  southern coast; also restores `_hangarDoor` / `_touchdownSmokeRemaining` fields
  dropped during the Canvas HUD wiring. Presentation only.

- **Ambient wind + rain audio (0025 item 7).** Soft looping wind bed and rain/
  storm ambience that respect mute and pause; procedural clips, presentation only.

- **Canvas HUD foundation (0025 item 6).** Runtime uGUI Canvas hosts the route
  offer panel and ops toast with EventSystem + Input System UI module. Status /
  research remain IMGUI until the interactive left panel migrates. Presentation only.

- **Hangar door day/night slide.** Hangar door slab opens through the day and
  closes at night (presentation only; always placed even when the hangar kit
  provides an opening).

- **Coast jetty + fishing boats.** Timber jetty into the shallows and three boat
  silhouettes so the southern KI shoreline reads as a living coast. Presentation only.

- **HUD contrast polish (0025 item 6 interim).** Panel fill 94% opaque, Coastal Blue
  panel frames, larger body type, themed progress bar for the first-offer wait,
  framed OPERATIONS panel. Still IMGUI — Toolkit/uGUI rebuild remains next.

- **Ground shadows + drifting cloud bands.** Soft elliptical shadows under
  commercials/ground traffic that soften with altitude; ten translucent cloud
  blobs drift east and tint with day/dusk. Presentation only.

- **URP material library spike (0025 item 4).** Shared `AirsideMaterialLibrary`
  profiles (asphalt/concrete/grass/metal/aircraft/glass/rubber) with procedural
  normal + soft AO maps; glTF kits and CreateBlock route through it. Not a full
  authored PBR set — Addressables still the production path. Presentation only.

- **Landside streetlights.** Six poles along the access road and car park with
  warm point lights that come up at dusk/night. Presentation only.

- **More building night glow.** Ops shed + terminal landside window spill; night
  glow quads now emit so dusk/night interiors punch through. Presentation only.

- **Control surfaces + touchdown camera shake.** Rudder/elevator (and soft
  tailplane) deflect with attitude; follow camera pulses on commercial
  touchdown with the existing smoke/chirp. Presentation only.

- **Aircraft attitude pitch + turn bank.** Takeoff nose-up, approach pitch and
  landing flare; gentle bank into turns for commercials and ground traffic.
  Presentation only (Batch D motion life).

- **First-session UX polish.** Enter accepts a ready route offer; waiting countdown
  + progress bar before the first airline; taller Accept button with pulse stripe
  on the first-decision panel; clearer empty OPERATIONS copy. Presentation only.

- **Denser apron props + fuel farm + parked GA.** More cones/barriers/signs/dollies,
  four apron floodlights, a small fuel farm west of the hangar, and two static GA
  aircraft so the airfield reads busier from overview. Presentation only.

- **Prop disc blur + tire roll (0025 item 7).** High-RPM takeoff/approach hides
  blade meshes and shows a translucent prop disc; landing-gear tires roll on
  ground phases. Presentation only (Batch D motion life).

- **Landside parking life.** Parked cars in the car park, kerbside drop-off/taxi,
  luggage trolleys, bench, extra trees and GA tie-down markers so the terminal
  approach reads as a working regional airfield. Presentation only.

- **Lighting profiles + wet surface gloss.** Cool fill light opposite the sun,
  horizon dome follows sky colour, nav/edge lights emit at dusk/night, and wet
  weather darkens/glosses paved surfaces (VFX-004 greybox). Presentation only.

- **Follow-camera framing.** Look-ahead along aircraft heading, altitude-based
  distance/pitch, and gentle yaw ease so F-follow fills the frame for taxi and
  flight. Overview (O) restores the default pitch. Presentation only.

- **glTF kit UVs + building surface textures.** `ArtGltfLoader` generates planar
  UVs so Batch B basecolours tile on box kits; hangar/ops/terminal meshes get
  corrugated/concrete textures with soft URP Lit response. Presentation only.

- **Left HUD sequential layout + first-decision coach.** Status panel rows no longer
  overlap; height shrinks in the first session; the coach tip uses a Safety Yellow
  stripe when a route offer needs Accept. Presentation only.

- **Richer Batch C v02 kits (0025 item 2).** Procedural `*_v02.gltf` turboprop,
  terminal, hangar, ops shed and service vehicles with more readable parts.
  Runtime prefers v02 and falls back to Approved v01; StreamingAssets synced.
  Presentation only; `scripts/test-domain.sh` unchanged in behaviour.

- **Regional airfield environment greybox (0025 item 3).** Outer paddock, coast
  sand/shallows, access road + car park, perimeter fence, eucalyptus clumps,
  distant hills and a soft horizon dome. Hangar/ops/terminal glTF kits now get
  Batch B surface textures when present. Presentation only; simulation unchanged.

- **Warm key / cool ambient lighting pass.** Soft directional shadows, warmer sun at
  day/dawn, cooler ambient fill, Open Sky camera backdrop. Presentation only —
  not a full URP post stack. `scripts/test-domain.sh` 97/97.

- **Approve and integrate brand wordmark + dawn splash.** Bailey Approve for BRD-001
  and UI-ILL-001; promoted into `Art/Brand` and `Art/UI/Illustrations`, synced to
  StreamingAssets, drawn on the opening briefing (full-bleed splash) and left HUD
  title. Also lands `.cursor/environment.json` for the headless domain harness
  (supersedes draft PR #34). `scripts/test-domain.sh` 97/97.

- **Fix packaged-build art loading (StreamingAssets).** Runtime glTF/PNG loaders
  resolve via `ArtRuntimePaths` (StreamingAssets first, Editor Assets fallback).
  Synced 63 art files into `Assets/StreamingAssets/Airside/Art` with
  `scripts/sync-art-streaming-assets.sh`. Records Bailey's ~20% visual assessment
  and presentation backlog as decision **0025**. Does not replace placeholder
  models — only stops art from silently vanishing in player builds.
  `scripts/test-domain.sh` 97/97.

- **First route-income consequence toast.** When the first accepted-route payout hits
  cash, a HUD toast confirms the decision→money loop. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **First-session HUD declutter.** Until the first route is accepted, hide hire/release
  crew, stand-3 build and research start controls; show one unlock line instead so the
  route offer stays the only early decision. Presentation only. `scripts/test-domain.sh` 97/97.

- **First-session countdown tip and auto-follow on Begin.** Coach line counts down to
  the first route offer; dismissing the opening briefing starts camera follow on the
  lead commercial so the aircraft cycle is visible immediately. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **First-session offer pacing and HUD priority.** First route offer arrives at 12s
  (was 25s); pending offers pin above the OPERATIONS panel so detail never buries
  the decision; first-offer coach tip uses caution colour. Domain constant +
  Presentation. `scripts/test-domain.sh` 97/97.

- **First-session opening briefing and clean new-game path.** New games (no away
  report, no accepted routes) open paused on a role + first-decision briefing;
  first route offer is labelled FIRST DECISION with consequence toasts on offer and
  accept; Pause / Insolvent offer Start new airport (wipes save and reloads).
  Presentation only. `scripts/test-domain.sh` 97/97.

- **Add review-ready brand and splash candidates.** Added the transparent
  `airside_wordmark_light_v01.png`, the 3840×2160
  `ui_splash_airport_dawn_v01.png`, exact generation evidence, and a compact AI
  image reference index. Both remain candidates until Bailey approves them;
  neither is wired into Unity.

- **Focus product plan on the Mac first playable.** Replaces the broad blueprint with
  a delivery plan that makes the first playable the only active target; defers companion,
  CloudKit, cargo/GA and extra art batches until external playtest confirms the loop.

- **Pulse hold-short markings during traffic waits.** When the traffic wait monitor
  warns, hold-short bars flash Safety Yellow → orange so the delay cause is visible
  in-world, not only on the HUD. Presentation only. `scripts/test-domain.sh` 97/97.

- **Touchdown chirp and day-est income icon.** Soft procedural squeal on landing
  transition plus income icon on the day-estimate HUD line. Presentation only.
  `scripts/test-domain.sh` 97/97.

- **Batch D runtime alive-airport + first-session coach tips.** Phase-based prop RPM,
  soft gear retract, split landing/taxi lights, dual touchdown smoke, denser storm rain,
  stronger engine heat on takeoff/approach, ground-traffic props/lights/heat, wet Stand 3
  apron, route-offer icon, and contextual left-panel tips. Presentation only — Unity
  .anim/.prefab files remain Planned; runtime behaviour Integrated. `scripts/test-domain.sh` 97/97.

- **Restore Unity macOS compilation.** Qualify Unity Object calls and include the
  built-in image conversion module required by PNG loading. Retain Unity-generated
  metadata for the new UI assets. Unity 6000.3.23f1 macOS build succeeded and all
  107 EditMode tests passed on 2026-09-07; live visual verification remains pending.

- **Integrate Batch C models, WLD kits, and Batch E cleanup.** Runtime
  `ArtGltfLoader` loads Approved Batch C / WLD / PRP glTF kits from disk with
  primitive fallbacks (aircraft, buildings, vehicles, service gear, markings,
  lights, apron props). Regenerated UI-ICO-003 service icon sheet and UI-PNL-002
  dark panel (~89% mean alpha); HUD draws operation/economy/service icons and
  prefers the dark panel. Presentation only; `scripts/test-domain.sh` 97/97.
  Unity Play Verified still Bailey-owned.

- **Merge wave: Approve Batch C/E and land overnight polish + Batch E HUD.** Bailey
  Approve recorded for Batch C models and Batch E UI. Merged draft PRs #19–#30
  (and #31 Approve) onto `main`: WLD greybox, Stand 3 traffic fix, insolvency HUD,
  follow cycle, research bar/mute, toasts, status colours, autosave chip, beacon,
  pause overlay, and Batch E icon/panel runtime wiring. Domain harness should be
  re-run on `main` after the wave.

- **Integrate Batch E UI candidates into the runtime HUD.** Verified all 7 candidates
  against their recorded SHA-256 hashes: `ui_service_icon_sheet_v01.png` is corrupted
  as committed (invalid PNG signature, hash mismatch — needs regeneration) and
  `ui_panel_9slice_dark_v01.png` measures ~9% average alpha (max 56%), too faint for
  the "WCAG-aware contrast" it was specified for, so the HUD keeps its existing
  procedural Runway Ink panel instead of regressing to it. The other 4 verified
  correct: sliced the weather/operation/economy icon sheets (7 icons each, single row,
  real alpha) into 21 individual files under `Art/UI/Icons/`, and copied the alert
  stripe and light panel into `Art/UI/Panels/`. Added `AirsideTheme.Icon`/`WeatherIcon`/
  `AlertStripeBackground`/`CautionStyle` (all fallback-safe if a file is missing); the
  HUD now draws the weather icon live and uses the alert stripe behind caution text.
  Done on Bailey's direct instruction, ahead of the usual formal-approval gate for new
  runtime art — still needs a Unity Play check. See the integration review in
  `docs/art/prompts/batch-e-ui-generation-2026-09-06.md`. Presentation only; no
  simulation code changed; `scripts/test-domain.sh` 96/96 pass (unaffected).
- **Pause overlay and speed caution colour.** While paused, a translucent dimmer
  and centred PAUSED chip appear (hidden under the away summary). 4× speed and
  pause tint the clock line Safety Yellow. Presentation only.
  `scripts/test-domain.sh` 96/96.
- **Ops-event toast.** When the operational event log gains an entry, a Clear Green
  chip flashes the latest flight · title for ~4.5s (skips history already present
  on load). Presentation only. `scripts/test-domain.sh` 96/96.
- **Aerodrome beacon and dual-flight phase HUD.** Night white/green pulsing
  aerodrome beacon mast (presentation greybox). Dual commercials show per-aircraft
  phase + countdown on the HUD; FormatPhase covers the full operation cycle.
  `scripts/test-domain.sh` 96/96 (Presentation not covered).
- **Autosave indicator.** After each autosave (and pause/quit saves), a short
  Clear Green "Saved" chip appears bottom-right for ~1.6s. Presentation only.
- **HUD status colours for cash, reputation and day estimate.** Negative cash and
  negative day-est. use Signal Red; Trusted reputation and healthy day-est. use
  Clear Green; Provisional reputation and ≤3-day cash runway use Safety Yellow.
  Presentation only. `scripts/test-domain.sh` 96/96 (Presentation not covered).
- **Research-complete HUD toast.** When `AirportResearch` finishes a project, the HUD
  shows a centred Clear Green banner for eight unscaled seconds naming the unlock
  and its permanent bonus. Presentation only — driven by `LastCompletedProjectId`
  after each sim tick. `scripts/test-domain.sh` unchanged (Presentation not covered).
- **Research progress bar, engine mute, Stand 3 presentation Z.** While a research
  project is active the HUD draws a Coastal Blue progress bar under the research
  line (`AirportResearch.Progress01`). Press **M** to mute engine loops; engines
  also drop to a quiet idle volume when paused or when props are off. Commercial
  aircraft and service vehicles at Stand 3 now use `AirportTaxiNetwork.StandZ`
  instead of the old Stand-1/2 ternary (presentation only). Help line lists mute.
  `scripts/test-domain.sh` unchanged (Presentation not covered); needs Play check.
- **Cycle the follow camera across dual commercials.** With two aircraft on the field,
  pressing F while already following advances to the next commercial (and wraps). First
  F still enters follow; O returns to overview. Help strip updated. Presentation only;
- **Show insolvency on the HUD.** Simulation already froze after three consecutive negative
  day closes, but the player only saw frozen cash with no explanation. Presentation now
  turns cash Signal Red when negative, warns on consecutive negative closes, auto-pauses
  visuals when insolvent, blocks ops hotkeys, and shows a centred AIRSIDE insolvency
  overlay. Presentation only — `scripts/test-domain.sh` 96/96.
- **Fix ground-traffic Stand 3 circuit.** Arrive/depart fleet aircraft mapped any non-Stand-1
  target onto Stand 2's lead-in and apron Z, so a Stand 3 assignment reserved the wrong
  taxi segment and parked at Stand 2's position. `BuildCircuit` now uses
  `AirportTaxiNetwork.LeadInFor` / `StandZ` for all three stands. Regression:
  `GroundTraffic_WhenStandsOneAndTwoAreBusy_UsesStandThreeLeadInAndPosition`.
  `scripts/test-domain.sh` 97/97.
- **Overnight WLD greybox + miniature look polish (presentation only).** Places Approved
  Batch B WLD intent with primitive stand-ins: animated windsock, threshold / hold-short
  markings, taxi edge lights, obstruction lights, cones, barriers, airside sign and
  dollies; stand equipment (stairs, chocks, GPU, pushback tug) tracks turnaround /
  pushback; Stand 3 apron pad appears when built; aircraft use `AirportTaxiNetwork.StandZ`
  (fixes Stand 3 drawing on Stand 2); tighter camera FOV / overview framing; dusk sky and
  ambient; away-summary branded to AIRSIDE with cash colour; night terminal/hangar window; night terminal/hangar window glow; painted stand digits; stylised runway end designators.
  glow; painted stand digits. `scripts/test-domain.sh` 96/96. Needs Unity Play soak.
  Does not integrate Batch C/E assets.
- **Approve Batch C models and Batch E UI candidates.** Bailey confirmed the
  Generated/Modelled Batch C set (AIR/BLD/VEH/PRP) and Batch E icon/panel
  candidates. Status moved to Approved in the art register. Integration
  (runtime wiring) follows; primitives remain fallback until Integration is
  Verified. Decision 0022 lifecycle unchanged.
- Generated Batch E UI candidates under `docs/art/candidates/`: four transparent
  seven-icon sheets plus light/dark nine-slice and caution-stripe textures. Every
  request repeats the decision-0022 anchor; exact prompts and processing evidence
  are recorded. Candidates are not approved, imported or used by runtime code.
- **Apply the approved Airside palette to the runtime HUD.** The REF-004 operations
  HUD reference (ChatGPT-generated, Approved) specified translucent Runway Ink
  panels, Cloud text, Coastal Blue for buttons, Safety Yellow for caution, Clear
  Green for on-time and Signal Red reserved for delay — none of which had reached
  `AirsidePrototype.OnGUI()`, which still rendered on Unity's plain default grey
  IMGUI skin. Added `AirsideTheme` (the palette from
  `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`, a themed panel background texture,
  and themed label/button styles) and wired it through every panel, label and
  button in the HUD, the route-offer card and the away-summary popup. Delay text
  is Signal Red, on-schedule/understaffed/caution states use Clear Green/Safety
  Yellow, buttons use Coastal Blue. Presentation only — no simulation or save
  behaviour changed; `scripts/test-domain.sh` 96/96 pass (this file has no
  EditMode coverage, since IMGUI rendering isn't unit-testable without Unity —
  needs a Play-mode check on the next Unity session).
- **Fix a zero-seed crash-on-save landmine.** `AirsideSaveData.Validate()` treats
  `randomSeed == 0` as corruption (rejecting the save and falling back to the
  previous snapshot), but `PersistentAirportSession.LoadOrCreate` would happily
  persist a literal 0 if ever called with a zero `newGameSeed` — the very next
  autosave would then throw `InvalidOperationException` and never recover.
  Unreachable today (the only call site is a hardcoded non-zero literal), but a
  real landmine for any future random seed source. Remapped 0 to a fixed
  non-zero fallback at creation, the same way `SeededRandomSource` already
  tolerates a zero seed internally. Added
  `NewGameWithZeroSeed_SavesWithoutThrowingAndPersistsANonZeroSeed`, confirmed
  it reproduces the crash without the fix. 96/96 tests pass.
- **Fix dead/confused branch in `AirportResearch.Progress01`.** The `!IsResearching`
  path had an unreachable condition (always evaluated false given the guard above
  it) that only ever mattered if a future caller queried progress outside of
  `IsResearching` — no current call site does. Simplified to what it actually
  computed (1.0 only when both projects are complete, 0.0 otherwise) and added
  `Progress01_WhenIdle_ReflectsOnlyWhetherBothProjectsAreComplete`, the first
  test coverage for that branch. No behaviour change for any current caller;
  95/95 tests pass.
- **Headless Domain/Simulation/Persistence test harness.** `scripts/test-domain.sh`
  runs the 94 EditMode NUnit tests via `dotnet test` against a hand-authored
  `scripts/dotnet-harness/Harness.csproj` that compiles Domain/Simulation/
  Persistence straight from the Unity project (a `HarnessSaveRepository.cs`
  stands in for the one file that needs `UnityEngine.JsonUtility`, using
  `System.Text.Json` with `IncludeFields = true` instead). Supplementary to
  `scripts/test-unity.sh`, not a replacement — Presentation and the real Unity
  compile still need a Mac editor. No simulation code changed; 94/94 pass.
- **Playtest HUD and taxi visuals.** HUD scaling uses resolution-aware `HudLayout`
  (Retina-safe). Taxi drawing follows reservation segment windows; yielded ground
  traffic snaps to its hold point instead of lerping through released space.
  EditMode layout/taxi visual regressions added. Simulation rules unchanged.

- **Passenger Services research.** After Operations Efficiency, unlock a second
  project (3500, one sim day) that permanently adds +$75 route income per
  departed commercial. Persisted via `start-research-passenger-services` (no
  save-schema field). Daily finance brief includes the route bonus and the Ops
  Efficiency operating-cost discount. See `docs/decisions/0023-passenger-services-research.md`.
- Batch D greybox hooks: gear retracts when airborne; nav/beacon/landing lights
  follow phase and night (beacon strobes); cabin door opens at stand; service-vehicle
  wheels spin on task; fuel hose / bag bob / bus door service loops; rain streaks + fog;
  paved surfaces darken when wet; engine heat shimmer while engines run; brief touchdown
  smoke puff; runway edge + taxi centreline markers.
- Added Batch D animation/VFX task packet and night apron flood lights that
  brighten as daylight falls (presentation only).
- Generated Batch C first-playable 3D kits (turboprop, terminal/hangar/ops shed,
  fuel truck, baggage train, apron bus, service equipment) plus livery atlases.
  Presentation uses richer primitive stand-ins with spinning props and dual-flight
  service-vehicle targeting; HUD lists each flight’s phase. Status
  Generated/Modelled — not yet Approved.
  Evidence: `docs/art/prompts/batch-c-models-generation-2026-09-06.md`.
- Bailey approved Batch B. Greybox now loads Batch B surface/decal textures in
  `AirsidePrototype` (solid-colour fallback if a PNG is missing). Added Batch C
  model production packet. WLD kits Approved but not yet placed as prefabs.
- Generated Batch B world-surface candidates: 2048 tileable asphalt, concrete,
  grass and corrugated metal; glass mask; runway/apron decals; metre-scale glTF
  markings, lighting and props kits under `game/Airside/Assets/Airside/Art/`.
  Later Approved and surface-integrated; see bullet above.
  Evidence: `docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md`.
- Prepared Batch B production task packet (world surfaces, decals, markings,
  lighting, props) and cleaned Batch A doc inconsistencies: art pipeline cites
  decision 0022, REF-004 delivery size, and the Batch A gate sentence.
- Added and approved Batch A visual references: daytime and dusk airport masters, turnaround service-zone composition, operations HUD direction, and the shared scale/palette sheet. Recorded prompt/source evidence; these five images are now the production visual authority.
- **Daily finance brief.** HUD shows a deterministic day estimate: expected
  operating cost (base + current weather + payroll) versus expected flight
  income at today's cadence, plus cash runway days when the net is negative.
  No save-schema change. See `docs/decisions/0021-daily-finance-brief.md`.
- **Accept-route schedule capacity.** Accepting a route now refuses when
  `ScheduledFlightsPerDay + pending` would exceed `StandCount × 6` (12/day on
  two stands). Offer stays pending; HUD disables Accept with a "Schedule full"
  reason. Operations panel lists accepted routes (airline, frequency,
  destination, payout) so the player can see what fills the cap. No save-schema
  change. See `docs/decisions/0020-accept-route-capacity.md`.
- **Concurrent commercial flights (slice 1).** Primary loop promoted to
  `CommercialFlight` list. When `ScheduledFlightsPerDay >= 4`, a second commercial
  spawns on a half-cycle stagger onto the free stand; fleet yields to any
  commercial. Single-flight path stays seed-identical below the threshold. No
  save-schema bump. HUD/world show both aircraft. 57/57 tests. See decision 0019
  and `docs/product/concurrent-flights-slice1-packet.md`.
- Added the approved Airside art direction and production asset contract: exact paths, staged first-playable manifest, image-generation rules, 3D/animation/VFX requirements, licensing workflow and cross-tool integration rules (decision 0022).
- **Concurrent flights design.** Decision 0019 locks promotion-to-list model,
  commercial FIFO priority, stand-based concurrency cap, schedule cadence
  (`ScheduledFlightsPerDay >= 4` → second flight), and per-flight settlement.
  First-slice task packet ready. Brief marked designed. No gameplay code in this
  change.
- **Daily operations report.** At each simulated midnight the sim publishes a
  `DailyReport` (flights, income, delays, running cost, net cash, reputation,
  weather, crew). Keeps the latest seven; rebuilt by replay. HUD shows the
  latest card. See `docs/decisions/0018-daily-operations-report.md`.
- **Research progression.** `AirportResearch` — first project Operations Efficiency
  (2500, one simulated day) permanently cuts base daily running cost by 100.
  Start is a persisted `start-research` command (replayed on load). Does not
  change flight timing. HUD shows progress / complete. See
  `docs/decisions/0017-research-operations-efficiency.md`.
- **Buildable third stand.** `AirportCapacity` — first capacity upgrade. Spend
  8000 (`build-stand` command, replayed on load) to unlock Stand 3; taxi network
  gains lead-in geometry; primary flights and ground traffic use the new stand.
  Two-stand behaviour stays seed-identical. HUD shows stand count and a build
  button. See `docs/decisions/0016-third-stand-capacity.md`.
- **Insolvency / game-over.** Cash negative at three consecutive simulated day
  closes declares the airport insolvent: simulation freezes, player commands
  refuse, and an `"Insolvent"` event is logged. Tracked on `AirportEconomy`
  (`ConsecutiveNegativeDays`, `IsInsolvent`); rebuilt by replay, no save-schema
  change. Presentation untouched. See `docs/decisions/0015-insolvency-game-over.md`.
- Test line.
- **Staffing by role.** `AirportStaffing` — ground crew, baseline 4. The baseline
  runs turnarounds unchanged (`TurnaroundWorkflow` gains an optional
  `staffingFactor` that short-circuits at 1.0, so every seed/timing test is
  byte-identical); extra crew shorten turnarounds, understaffing lengthens them
  into delays. Hire (120, persisted `hire-crew`/`release-crew` commands) and a
  daily wage settled with the running costs. `AirportEconomy.TrySpend` generalises
  the old priority-crew purchase. HUD shows headcount/payroll with hire/release
  and an understaffed warning. 60/60 tests. See `docs/decisions/0014-staffing-by-role.md`.
- **Simulated weather + daily running costs.** `Weather` (deterministic from the
  timeline, biased mild) changes every 5 simulated minutes. Each simulated
  midnight the airport pays `BaseDailyOperatingCost` (400) plus a weather
  surcharge (0–160) via `AirportEconomy.PayOperatingCosts` — the economy now has
  a drain, so cash can fall and route income / on-time performance matter for
  solvency. Weather does not affect flight timing (keeps every seed/timing test
  green). HUD shows current weather; away summary reports operating cost. 56/56
  tests; macOS build runs. See `docs/decisions/0013-weather-and-daily-running-costs.md`.
- Route proposals can now be **declined** (a persisted `decline-route` command).
  The HUD offer panel gains a Decline button; `AirportRoutes` exposes
  `OffersDeclined` and `ScheduledFlightsPerDay` (sum of accepted routes'
  flights/day), shown on the HUD. 51/51 tests. Groundwork for
  `docs/product/concurrent-flights-brief.md`.
- The "welcome back" away summary now also reports **route income earned** and the
  **reputation change** while the player was away (design pillar: a short visit
  should reveal what changed). `AirportEconomy` tracks `TotalRouteIncome`.
  48/48 tests.
- **Reputation** (0–100, starts 50). On-time departures raise it, delays lower it
  in proportion to the delay. `AirportRoutes.Accept` now takes the score and
  refuses proposals above the airport's reputation; an accepted route locks in a
  per-flight bonus of `3 × (score − 50)`. Reputation is rebuilt by replay — no
  persisted field. HUD shows score + band and disables offers the airport can't
  meet. 47/47 tests; macOS build runs. See `docs/decisions/0012-reputation.md`.
- **Airline route proposals.** `AirportRoutes` (simulation) offers one scheduled
  service at a time on a timer; it lapses if unaccepted. Accepting is a persisted
  `accept-route` command (replayed on load / offline catch-up); every completed
  flight then pays `Routes.IncomePerFlight` on top of turnaround revenue.
  Proposal content comes from the timeline, not the random source, so existing
  seed-based outcomes are unchanged. HUD shows the offer with an Accept button.
  42/42 tests; macOS build runs. See `docs/decisions/0011-airline-route-proposals.md`.
- Airport **location** + **day/night cycle**. `AirportLocation` (domain) carries
  id/name/region/UTC offset/latitude; ships with Kingscote (default), Port Lincoln
  and Coober Pedy. `DayCycle` derives local time from the sim clock — one
  simulated day per 20 real minutes from an 08:00 start — and drives the sun and
  ambient light and a HUD line (location, day, clock, phase). **Save schema → v2**
  (adds `locationId`); schema-1 saves migrate on load. 37/37 tests; macOS build
  runs. See `docs/decisions/0010-location-and-day-cycle.md`.
- Fair corridor hand-off: when the shared A1/A2 corridor is free and more than one
  fleet aircraft is queued, it goes to the one that has waited longest (fleet
  order breaks ties), instead of fleet order alone. Reuses the traffic monitor's
  wait timestamps — no new state. A forty-cycle soak asserts neither fleet
  aircraft is starved. 30/30 edit-mode tests; macOS build runs.
  See `docs/decisions/0009-fair-corridor-handoff.md`.
- Ground-traffic **fleet**: `AirportSimulation.GroundTraffic` is now a list.
  `GT-201` runs an arrival/stand/departure schedule (parking on whichever stand
  the primary flight is not using); `GT-202` repositions in and out through a
  run-up bay without a stand, starting 25s later. Every fleet aircraft reserves a
  single-file `TAXI-CORRIDOR` lock while on A1/A2, so at most one is on the shared
  taxiway at a time — they queue instead of meeting head-on. The primary flight
  keeps priority and is never blocked (`ReservationConflicts` stays zero).
  Deadlock-free by construction. Presentation renders one model per fleet aircraft
  and lists them in the HUD. 28/28 edit-mode tests; macOS build runs.
  See `docs/decisions/0008-ground-traffic-fleet-and-corridor-lock.md`.
- Earlier the same day: single second aircraft — shared segment reservations
  (`0006`), then an arrival/stand/departure schedule (`0007`).
- Second aircraft first introduced: shared taxi-segment reservations with the
  primary flight, priority-and-yield rule, per-tick reservation time so segments
  are correct during offline catch-up.
  See `docs/decisions/0006-second-aircraft-priority-and-yield.md`.
- Repo set up for shared work: added `.gitattributes` (Unity merge/binary rules),
  expanded `AGENTS.md` into the shared contract, added `CLAUDE.md` and Cursor
  rules, this changelog, and `LICENSES.md`. Removed a stale duplicate
  `AirsidePrototype 2.cs`. Pushed to a private GitHub `origin`.
- Segment clearance and traffic wait diagnostics: taxiing aircraft reserve only
  the segment they occupy and release it before moving on; a monitor explains any
  aircraft blocked on one resource for ten seconds or more
  (`docs/decisions/0005-segment-clearance-and-wait-diagnostics.md`).

## 2026-09-06

- Add named taxi routes and operations history.
- Add persistent saves and offline catch-up.
- Add operational turnaround and economy loop.
- Complete deterministic movement milestone.
- Open the Airside prototype by default.
- Add first playable Airside prototype.
- Target current Unity 6.3 LTS patch.
- Create Airside project foundation.
