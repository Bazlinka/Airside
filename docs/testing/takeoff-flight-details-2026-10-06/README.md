# Smooth takeoff and flight details — 6 October 2026

Bailey reported a takeoff altitude step and requested immersive status/location
information in every aircraft view. ADR 0238 defines the scope and acceptance.
Explicit constraint: no Unity tests, builds, editor/player execution. Native
compilation, motion, camera switching and rendered text remain unverified.

## Source diagnosis and implementation

TakingOff ends at TakeoffEndHeight. WatchingJourney becomes active at Outbound0,
but the old enroute path starts at DepartedEndHeight and the horizontal end of
the departure turn. It bypassed the entire local Departed path in aircraft views.
Checked-in performance formulas give these altitude jumps (metres above datum):

| Type | Takeoff end | Old journey start | Jump |
|---|---:|---:|---:|
| ATR42 | 101.57 | 275.72 | 174.16 |
| B38M | 134.30 | 351.83 | 217.53 |
| A359 | 117.98 | 321.00 | 203.02 |
| B78X | 112.23 | 304.25 | 192.02 |

JourneyWorld now reuses the local mapped departure for DepartedSeconds. A
presentation-only Hermite offset then matches the field exit to the original
geographic route while keeping the leg timer intact. Horizontal endpoint
velocities use .1-second finite differences and are approximate; the pure curve
matches its supplied derivatives exactly. Height curves are monotonic and match
vertical rates through rotation, takeoff/climb-out and the timed route join.
Authored body attitude is retained at the status boundary and fades into the
route angle. Regional departure/terminal branches are preserved. Normal fixed-wing
legs are at least 600 seconds; the join finishes before the terminal blend begins.

Cockpit, passenger left/right and exterior share one watched-registration data
snapshot. Display route/status, nearest catalogue airport and latitude/longitude,
field-relative height, true heading, ground speed, vertical speed, direct distance
to the current target, model leg progress and time to the arrival area when
supported. Do not call field height AGL/MSL or the area estimate touchdown ETA.
Ground/holding/cancelled/turnaround states use explicit labels without invented
arrival timing. Origin shifts preserve location; narrow views use two metric
columns, keep controls inside panels and place toasts below the identity panel.

## Code-only evidence

- 42 new departure tests cover all 13 fixed-wing types: takeoff/Outbound position
  and vertical-rate continuity, local exit/route continuity, monotonic lift-off /
  climb, exact return to the timed profile and generic world-axis correction.
- 19 new information/painter tests cover cardinal true headings, floating origin,
  outbound/return route identity, scheduled/cancelled/turnaround states, ground /
  runway status without fabricated ETA, and all four views at 320/800/1440/1920 widths.
- Focused **61 passed / 0 failed**, [log](focused.txt).
- C# 9 syntax parsing of seven changed production files: **0 errors**, [log](syntax.txt).
  This checks syntax of the native partials; it is not Unity compilation.
- Static metadata/mirror audit: **1,764 GUIDs**, **386 mirrors**, **70 materials**,
  [log](assets.txt). No runtime assets or external data added.
- Integrated headless **1,664 passed / 0 failed**, [log](integrated-headless.txt).
- Primary review plus independent aircraft/path and HUD source review completed;
  whitespace clean. Saves, simulation timings and weather are unchanged.

Headless tests compile the actual pure helpers and painter. Native Mesh/Transform/
camera APIs and shaders are excluded. They do not verify on-screen prominence,
exact font metrics, controller behavior, runtime continuity or performance.
