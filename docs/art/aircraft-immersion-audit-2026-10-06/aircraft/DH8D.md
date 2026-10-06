# DH8D — Dash 8-400 / Q400 immersion audit

Audit date: 6 October 2026. Source baseline: main `64ca4a23`, supplied audit checkout. Analysis only: no Unity launch, tests, renders, model changes or functional avionics work. Preserve the approved believable miniature exterior; closer interiors merit better shape, material and spatial detail, not a separate photographic art style.

## Coverage and evidence

| View | Current implementation | Evidence and limit |
|---|---|---|
| Cockpit | Supported; dedicated Dash fit within the shared glass-turboprop builder | `GlassTurbopropCockpitInterior.cs:21–35, 50–90`. No retained DH8D cockpit image found: `docs/testing/turboprop-cockpits-2026-10-01/` contains only its README, which explicitly records missing native capture. Source assessment only. |
| Left window | Supported; DH8D station and representative 2+2 cabin | `PassengerCabinProfile.cs:19`, `PassengerCabinInterior.cs:15–28`. Visually inspected DH8D tile in historical `docs/testing/passenger-flight-views-2026-10-01/left-sheet.jpg`. |
| Right window | Same cabin reused, mirrored eye and yaw | Same source; visually inspected DH8D tile in historical `right-sheet.jpg`. |
| Exterior | Supported shared orbit; exterior kit restored | `AirsideCameraController.Cockpit.cs:85–94, 208–234`; visually inspected historical DH8D tile in `outside-sheet.jpg`. |

These are 1 October native fixture stills, not current packaged-player evidence. `GAME.md:295–310` records current combined-source integration review still outstanding. Contact sheets permit broad shape assessment, not close seam or dynamic propeller judgement.

## Largest gaps

**The interior window system does not follow the Q400 exterior window rhythm.** DH8D supplies 0.76 m seat pitch; the shared builder also uses it for every window station (`PassengerCabinInterior.cs:38–48, 92–115`). The exterior generator instead creates windows at 0.508 m intervals (`scripts/generate-air-006-dash8-q400.py:726–735`). Exterior nominal panes are 0.24 × 0.34 m; the interior opening is a 0.32 × 0.44 m ellipse. Those are authored source dimensions, not certified aircraft measurements. Hidden fuselage geometry makes a selected aperture workable but conceals this disagreement. Seat pitch and structural window spacing should be independent.

**The actual historical view reads as a generic oval cut into a flat wall.** Both DH8D tiles show a thick, visibly faceted pale rim and dark rectangular corners around it. The left tile shows a silver-grey nacelle across the upper aperture; the right shows the same general nacelle mass against blue sky. This obstruction is not automatically a defect: Q400 high-wing/nacelle geometry legitimately dominates some seats. What is absent from the composition is a convincing recessed liner, glazing depth and coherent curved sidewall. Do not move the wing or invent a clear view to fix it.

**Cabin identity is mostly numbers.** DH8D has its own 1.295 m half-width, 2.566 m window height and 3.650 m longitudinal station, but shares flat ceiling, full-length box bins, five-row end walls and rectangular seats with all types (`PassengerCabinInterior.cs:29–72`). Full yaw and generous pitch are permitted (`AirsideCameraController.Cockpit.cs:201–205`), exposing this short theatrical section when looking inside. Seat shapes and end-wall appearance are source-derived concerns, not observed in these window contact sheets.

**Cockpit identity is a good start but shallow.** The builder already gives DH8D a distinct eye, narrower deck, taller five displays, narrower overhead and roof hatch (`GlassTurbopropCockpitInterior.cs:31–34, 154–173, 251–259`). Yet its pedestal, yokes and uniform switch arrays mostly share ATR geometry. The baked 192-pixel display patterns and “DH8D • LOCAL VIEW” panel label are stylised presentation choices (`:87–89, 176–227`), not faithful equipment detail. Exact Q400 equipment arrangement and shell proportions remain unverified against a primary cockpit photograph; do not present a five-display layout accuracy claim as externally confirmed.

## Prioritised proposals

| Priority | Player-visible improvement and bounded change | Ownership | Impact / effort |
|---|---|---|---|
| 1 | Replace square corner gaps/faceted rim with a continuous rounded aperture, inset reveal, layered seals and subtly curved wall. Give DH8D its own aperture profile; reconcile to actual kit panes before choosing dimensions. | Shared cabin topology; DH8D profile | High / M |
| 2 | Decouple seat-row pitch from window stations. Anchor the chosen DH8D eye to a real generated pane, then preserve nacelle/wing relationships in both views. Retain 2+2 as representative seating. | Shared profile schema; DH8D station data | High / M |
| 3 | Shape Q400 bins with door divisions, lower lip and latches; add row-aligned PSU faces and ceiling coves. DHC documents modified bin lips/doors and LED ceiling/under-bin washes as real enhancements. Choose one plausible cabin era, rather than mixing every optional retrofit. | DH8D cabin fittings using shared primitives | High / M |
| 4 | Replace the five-row end walls with a short visually continuous cabin extension or convincing distant occlusion; round seat shoulders and add shared bench structure, belts and restrained cloth relief. Keep the narrow single aisle legible. | Shared cabin shell; DH8D width/layout | Medium / M |
| 5 | Give cockpit panel, pedestal and overhead aircraft-specific silhouettes and equipment grouping once a primary Q400 interior reference is secured. Improve chamfers, switch bases and seals before adding more tiny labels. Retain spectator controls. | Shared builder infrastructure; DH8D deck data | High / L |
| 6 | Balance cockpit/cabin roughness, glass edge response and gentle local fill. A bounded Q400 retrofit-inspired lighting option can show ceiling and under-bin washes without washing out outside scenery. Replace in-world technical identity text with a restrained physical placard; preserve shared flight HUD telemetry. | Shared material/light support; DH8D palette | Medium / M |
| 7 | Refine what passengers actually see of nacelles: intake lips, panel joins and propeller/spinner material separation, keeping the long pods, six-blade geometry and T-tail. Preserve current high-wing exterior character and full restoration when switching views. | DH8D exterior kit; shared visibility lifetime | Medium / M |

## First pass and acceptance

Start with proposals 1–3: they directly improve the two supported passenger views and fix a demonstrable geometry disagreement. Cockpit detail should follow a verified reference, not an ATR-derived guess.

Implementation review should trace DH8D pane centres into cabin stations, inspect aperture topology for uncovered rectangle corners, and keep seat pitch independent of windows. Check that nacelles/props remain retained by `CockpitExteriorVisibility.cs:12–24` and that interior meshes/materials remain batched rather than creating per-frame parts. This audit performed none of those implementation checks.

Future native and packaged visual acceptance must review left/right at default and wider gaze, then cockpit forward/panel/overhead/sides and exterior after repeated switches. Use day, dusk and night; verify no shell leaks, near-clip intersections, visible section ends, crushed seats, disappearing nacelles or unreasonably bright windows. Capture registration, aircraft type, build identity and phase. Stills cannot establish propeller motion, exposure transitions or runtime performance.

## Primary references

- [De Havilland Dash 8-400](https://dehavilland.com/dash-8-400/) — exact variant and manufacturer exterior/configuration context; accessed 6 October 2026.
- [DHC Cabin Enhancements brochure](https://dehavilland.com/wp-content/uploads/2024/10/DHC_Dash-8_Cabin_Enhancements_v12.pdf), pp. 2, 5–7 — window/bin changes, PSU realignment, extended bin details and LED wash locations. These are configurable improvements/retrofits, not universal equipment. Genuine differentiator: Q400 extended bins reshape the lower lip and replace doors/latches; a rectangular generic bin cannot express that cabin option.
- [Qantas Dash 8-400 seat map, 74 economy seats](https://www.qantas.com/content/dam/qantas/pdfs/qantas-experience/onboard/seatmaps/dash-8-400.pdf) — exact operator/variant layout reference, not a universal Q400 configuration or permission to copy airline branding. No certified window, eye-height or seat-pitch measurement is claimed from this map.
