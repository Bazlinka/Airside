# ATR42 — ATR 42-600 interior realism audit

Audit of source snapshot `64ca4a23`, 6 October 2026. Analysis only; no game changes, tests, Unity execution, build or new capture. Preserve the coherent stylised miniature exterior required by `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md:14–33`; increase close-view credibility through fitted geometry and material response.

## Coverage and evidence

Repository-relative `P/` means `game/Airside/Assets/Airside/Presentation/`.

| View | Current support and evidence | Limit |
|---|---|---|
| Cockpit | Dedicated ATR branch of `P/GlassTurbopropCockpitInterior.cs:21–35`; left pilot eye, five displays, yokes, pedestal and overhead | No ATR cockpit image found in the supplied 1 October fixtures. Source findings are not observed cockpit rendering defects. Saab/jet captures cannot validate this deck. |
| Left passenger | ATR tile visually inspected in `docs/testing/passenger-flight-views-2026-10-01/left-sheet.jpg` | Historical native fixture, not current packaged-player evidence; reveals near window framing and high-wing/nacelle obstruction, not whole cabin. |
| Right passenger | ATR tile inspected in corresponding `right-sheet.jpg` | Same limitation; stronger shade and nacelle/wing across upper opening. |
| Exterior | ATR tile inspected in corresponding `outside-sheet.jpg`; supplementary `docs/testing/aircraft-identities-2026-10-06/native/ATR42.jpg` inspected | First is historical fixture; second is recorded native runtime-builder review, not flight-camera playtesting. Neither verifies prop motion, weather or view transitions. |

`P/AirsidePrototype.Cockpit.cs:115–123` constructs these interiors; exterior uses the aircraft without an interior. Current camera defaults to 65° inside, with 35–85° zoom; passenger yaw permits a full turn (`P/AirsideCameraController.Cockpit.cs:64,142,201–205`). Keep the shared HUD usable; no instrument-system expansion.

## Biggest mismatches

**Observed historical fixture:** both ATR passenger windows look like bright faceted oval rings mounted over rectangular recesses. Angular corner patches and dark rectangular bounds are plainly visible. A smooth sculpted wall/reveal is the most immediate improvement. Retain the distinctive high-wing/nacelle occlusion.

**Source-derived:** ATR gets the same fixed 0.32 × 0.44 m oval aperture as every passenger type (`P/PassengerCabinInterior.cs:92–115`). Its exterior generator instead specifies 0.24 × 0.34 m panes at 0.508 m spacing (`scripts/generate-air-001-atr42-v03.py:283–301`). Interior windows are one per 0.76 m seat row. Smoothing the oval alone leaves this interior/exterior mismatch unresolved. The exterior fuselage is hidden while wing, nacelle and prop parts remain (`P/CockpitInterior.cs:26–46`, `P/CockpitExteriorVisibility.cs:12–17`), masking that discrepancy during interior viewing.

**Source-derived cabin proportions:** `P/PassengerCabinProfile.cs:18` uses half-width 1.340 m, window station Y=2.168/Z=2.048, 0.76 m pitch and 2+2 seating. The manufacturer factsheet cross-section distinguishes 2.263 m interior width from 2.570 m outer width and shows a 1.91 m height; the constructed section is 2.68 m wide with floor/ceiling at −1.02/+0.99 m. Its seat-width formula gives roughly 0.51 m before gaps, versus the reference's 0.460 m seat width. Calibrate these; current clipping is unverified. Pitch and seating already match ATR's 48-seat/30-inch example.

**Source-derived cockpit:** five landscape LCDs are correctly retained, a real -600 differentiator confirmed by ATR. However ATR and Q400 share generic guidance controls, yoke construction, FMS blocks and pedestal arrangement; the overhead is a regular 7×6 switch grid (`P/GlassTurbopropCockpitInterior.cs:55–89,152–173,230–259`). More pixels alone cannot fix their identity. All lit surfaces receive smoothness 0.2 (`P/CockpitInterior.cs:60–68`). Runtime material appearance remains unknown.

## Prioritised proposals

| Priority | Player-visible improvement and bounded change | Ownership | Impact / effort |
|---|---|---|---|
| 1 | Replace rectangular wall cutouts with continuous curved wall/reveal geometry; independently profile ATR aperture and window stations against the kit and reference. Add shade track, handle and restrained clear inner pane without blocking the view. | Shared window builder; ATR dimensions | High / M |
| 2 | Make this a compact 2+2 regional cabin: calibrate inner width, seat width, eye clearance and crown against the reference cross-section. Decouple row pitch from window pitch. Keep the chosen station's real high-wing/nacelle relationship on both sides. | ATR profile; shared seat builder | High / M |
| 3 | Separate an ATR deck geometry profile from Q400: five-display bezel spacing, guidance panel silhouette, yoke grips and pedestal lever groups. Replace the uniform overhead grid with reference-led panel groupings; controls remain decorative. | Shared construction tools; ATR layout | High / M |
| 4 | Shape ATR-compatible slim seat backs, upholstery seams, tray hinges and armrests; curve overhead-bin fronts and add bin-door seams/PSU blocks. Use a fictional cabin finish, not a copied operator cabin. | Shared cabin tools; ATR family proportions | Medium / M |
| 5 | Replace the five-row segment's visible end caps with credible continuation or bounded depth proxies. Passenger full-turn look currently exposes a section only 3.8 m long (`PassengerCabinInterior.cs:38–42`).  | Shared cabin builder; ATR layout lengths | Medium / M |
| 6 | Distinguish matte fabric, softly satin liners, rubber seals and LCD/glazing response. Add restrained window daylight and overhead illumination appropriate to a compact ATR cabin; keep displays readable at dusk/night without universal emissive glow. | Shared materials/light; ATR tuning | High / M |
| 7 | Preserve exterior high wing, compact nacelles, T-tail and six 3.93 m propellers. Future orbit review should check close prop/wing finish and restoration after leaving interiors; do not redesign the already recognisable exterior to solve cabin flaws. | ATR kit; shared visibility/camera | Medium / S–M |

## First pass and acceptance

Start with priorities **1, 2 and 3**: fitted window construction, compact cabin proportions, then ATR-specific cockpit silhouettes.

Implementation source review should document one coordinate system, measured kit window centres, independent seat/window pitches, justified inner cross-section and seated-eye clearance. Confirm the five-display layout and retained wing/engine/prop renderers, plus exact visibility restoration. The source comment's sight angle is not certified data.

Future owner-approved runtime review must inspect all four views, passenger full-turn look, cockpit forward/down/overhead, zoom extremes, both window sides during banking, and day/dusk/night. Accept smooth wall transitions, plausible nacelle occlusion, no visible section caps or eye-through-seat/lining, legible shared HUD and stable visibility after repeated switches. **None of those runtime acceptance checks was performed here.**

## Primary references

- [ATR 42-600 current product page](https://www.atr-aircraft.com/regional-mobility/regional-aircraft/atr-42-600/): exact variant; configurable 30-seat, 48-seat and mixed cargo examples. Operator configurations vary.
- [ATR 42-600 manufacturer factsheet](https://www.atr-aircraft.com/wp-content/uploads/2020/07/Factsheets_-_ATR_42-600.pdf): dated reference drawing, cross-section and 48-seat plan; six-blade 3.93 m propellers. Older engine data does not define current deliveries.
- [ATR cockpit](https://www.atr-aircraft.com/innovation/cockpit/): confirms five LCDs for -600 family. Main photo is labelled ATR 72-600; use for family layout guidance, not exact ATR42 window/eye dimensions.
- [ATR Geven seat brochure](https://www.atr-aircraft.com/wp-content/uploads/2020/07/new_geven_seats_146.pdf): ATR -600 family seat reference; fittings remain configurable. PDF text accessible, screenshot request timed out, so no detailed visual claim relies on its photographs.
