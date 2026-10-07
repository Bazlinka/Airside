# Changelog

One line per merged change, newest first: what the player or contributor sees, plus the PR or ADR number.
Keep each entry to **one line (about 160 characters)**; the evidence belongs in the PR, an ADR or
`docs/testing/<topic>/README.md`. Mark behaviour not yet run in Unity as "(unverified)".
Older entries (about 1,000, through 2026-10-07) are in
[`docs/history/CHANGELOG-through-2026-10-07.md`](docs/history/CHANGELOG-through-2026-10-07.md).

## Unreleased

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
