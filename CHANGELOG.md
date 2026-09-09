# Changelog

One line per merged change, newest first. Update this in the same commit as the
change it describes.

## Unreleased

- **Adelaide overnight visual rebuild.** New games start at Adelaide Airport
  (`ADL`), not Kingscote. Ground is one level grass deck plus shared-height
  pavement (no tiled gaps); the field is much larger with a visual 12/30 cross
  runway and Gulf St Vincent to the west. ATC uses Adelaide Tower / runway 23.
  Runway paint is 05/23 along the full strip. Aircraft kit sits on the pavement
  with a slightly larger silhouette. Awake no longer builds ~10k offset cubes
  and refuses a second prototype instance. PC shadow distance is 260 m so the
  bigger overview still gets sun shadows. Outer fence ribbons enclose the long
  23/05 strip (inner fence no longer sits on the asphalt). Unity EditMode 193/193.

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
