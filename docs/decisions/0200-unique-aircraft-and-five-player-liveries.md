# ADR 0200: Unique aircraft designs and five player livery presets

Date: 2026-09-30
Owner: Codex
Request: Bailey requested unique liveries for all aircraft and five player presets.

Each of the thirteen flying aircraft types now has a distinct fin silhouette and
an individually fitted fuselage ribbon. Coastline, Southern Cross, Outback, Gulf
and Redgum provide five coordinated primary/accent palettes. The player's airline
name stays their own; preset names do not replace operators in the simulation.
The parked rescue helicopter retains its separate rescue appearance.

| Type | Composition | Fin symbol |
| --- | --- | --- |
| ATR 42 | Saltwater | Paired feathers |
| Saab 340 | Ochre Country | Sunrise and horizon |
| Dash 8 Q400 | Coastal Current | Broken currents |
| E190 | Southern Star | Compass star |
| A220-300 | Morning Light | Three sun rays |
| A320-200 | Tidal Arc | Paired tidal wedges |
| 737-800 | Outback Horizon | Mountain horizon |
| 737-8 | Crosswind | Crossing ribbons |
| A321neo | Long Coast | Two ascending coastal strokes |
| A350-900 | Southern Aurora | Three ascending aurora strokes |
| A330-900neo | Desert Dawn | Open dawn peak and horizon |
| 787-9 | Ocean Reach | Two ocean crests |
| 787-10 | Southern Meridian | Meridian with directional pointers |

Paint polygons are clipped to the actual fuselage and fin triangles. Concave fin
symbols are triangulated before clipping to preserve their notches. Three ribbon
rise curves distinguish wave sweeps, late ascending ribbons and smooth sweeps.
No model dimensions, glazing, pilots, wings, engines or moving parts change.
All non-paint vertex/index arrays were compared byte-for-byte with the parent.

The setup wizard offers exactly five named two-tone choices. The Airline page
shares the same palette and applies the existing repaint command. Preset accents
are also used by the aircraft builder and in-place repainting. Old custom colours
remain valid and use the previous contrasting accent fallback; no save version or
migration is needed. Freighters keep their existing darkened operator paint.

Scope: fitted-paint generator and thirteen runtime/editable models, thumbnails,
packaged art, paint colour policy, setup and Airline painters, focused tests and
validation evidence. Economy, schedules, routes and fleet ownership stay unchanged.

Acceptance: thirteen individual bilateral fin marks; fitted paint 3–25 mm proud;
five choices in both player entry points; preset swatch accents match runtime
paint; old saves load; metadata and packaged copies match. Run headless and geometry
checks here, then Unity EditMode and packaged day/dusk/night overview/follow checks
on the Mac before merging. Offline proof sheets do not establish Unity appearance
or frame time. See `docs/testing/aircraft-liveries-2026-09-30/README.md`.

All designs are project-authored; no external assets, image generation, paid tools
or licence obligations are introduced. Parent git revision is the fallback.
