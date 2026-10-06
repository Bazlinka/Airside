# Aircraft interiors and flight-view immersion audit — 6 October 2026

Requested by Bailey: analyse every aircraft with one dedicated agent per type,
then find improvements that make cockpit, passenger and exterior views visibly
more realistic and immersive. Functional avionics and instrument systems are not
required. This report proposes work; it does not authorise or claim implementation.

Source snapshot: main `64ca4a23` (PR #537). Thirteen fixed-wing passenger types plus
Bell 412EP. Four current camera modes: cockpit, left/right window and exterior.
Bell's cockpit and passenger modes are currently unsupported; freighter refits
exclude passenger seats. Fourteen aircraft agents worked in batches of up to
three; the integrator reviewed shared infrastructure and consolidated priorities.
Source line pointers refer to that baseline, including older `GAME.md` blocks;
this documentation branch adds a new handoff without changing runtime source.

No Unity tests, builds, editor/player launches or new runtime captures. Sources
are current code and shipped models, primary aircraft references, 1 October
native cockpit/passenger fixtures and 6 October native exterior-builder fixtures.
Old captures establish their depicted appearance only; they are not proof of the
current packaged game. Missing ATR/Q400 cockpit captures remain missing. Reference
images inform original future modelling; no third-party pixels/assets imported.

## Direction

Improve the large, near-camera forms first: fitted windows and sidewall curvature,
seated-eye position, cockpit silhouettes, seat construction and material response.
Then add bounded lighting, decorative fittings and believable cabin depth. Keep
the existing coherent miniature exterior; close-view realism means aircraft-specific
proportions and construction, not an unrelated photorealistic asset pack. Existing
flight state, stable watched registration, weather and motion remain the owners
of animation; do not create another flight simulation for the cockpit.

## Shared findings

1. **One aperture for thirteen aircraft.** `PassengerCabinInterior.cs:92–115`
   makes every opening a 0.32 × 0.44 m ellipse. `:38–48` uses seat-row pitch for
   window spacing. Historical window fixtures show conspicuous faceted white
   rings and dark rectangular bounds. Give aperture/station data its own per-type
   profile; do not move wings/engines to fake a better view.
2. **A short boxed cabin.** `PassengerCabinInterior.cs:38–42` places full end walls
   around five rows, with flat ceiling and sidewalls. Passenger yaw is unrestricted
   (`AirsideCameraController.Cockpit.cs:201–205`), so depth/curvature and inward
   views need deliberate treatment. Historical window sheets do not show every
   inward angle; the enclosure limitation is source-derived.
3. **Seat count is not enough identity.** `PassengerCabinInterior.cs:49–72` uses
   shared rectangular seats, full-length box bins and a universal .44 m aisle.
   Preserve known 1+2, 2+2, 2+3, 3+3 and twin-aisle groupings, but calibrate widths,
   bin shapes, overhead units, wall shoulders and eye clearances per aircraft.
   Manufacturer example layouts are configurable; they are not operator replicas.
4. **Materials have too little distinction.** `CockpitInterior.cs:60–68` assigns
   smoothness .2 to every lit material; the cabin uses flat colours. Separate
   rubber, fabric, moulded lining, painted metal, brushed fittings and clear glass.
   Use restrained texture/roughness and construction seams before more polygons.
5. **Jet shells are mostly one shape.** `JetCockpitShellGeometry.cs:32–39` takes
   only half-width; its sill, crown and forward angles are common. Family-specific
   control/display cues already exist. Preserve those while profiling window posts,
   panel slopes, glareshields, pedestal and roof shapes by family/type.
6. **Cabin light inherits panel light.** Passenger interior derives from
   `CockpitInterior`; its nonvirtual environment path creates a nearby warm panel
   point light (`CockpitInterior.cs:106–123`). A real cabin lighting pass needs its
   own ceiling/under-bin/reading-light treatment, bounded fill and glazing response.
   Existing unlit light strips are not proof that they illuminate geometry.
7. **Exterior mode removes the close interior.** `AirsidePrototype.Cockpit.cs:94–121`
   destroys/rebuilds the current interior and leaves it absent in exterior mode.
   Better window-depth/interior hints outside need a separate low-detail solution;
   blindly keeping the entire close cabin/crew active would waste geometry and
   complicate renderer restoration.

## Aircraft recommendations

Each report covers cockpit, left passenger, right passenger and exterior support,
inspected evidence, aircraft-specific mismatches, priorities and future acceptance.
Effort is relative (S/M/L), not a delivery estimate. All recommendations remain
proposals. Cabin furnishings refer to representative fictional operator layouts.

| Aircraft | Highest-value visible work | Dedicated report |
|---|---|---|
| Saab 340B | Compact asymmetric 1+2 cabin; Saab window trim; layered EFIS/gauge/overhead fittings | [SF34](aircraft/SF34.md) |
| ATR 42-600 | Calibrated compact 2+2 section and independent window stations; five-screen ATR panel shapes | [ATR42](aircraft/ATR42.md) |
| Dash 8 Q400 | Deep window reveals retaining high-wing/nacelle sightlines; shaped bins and Q400-specific deck | [DH8D](aircraft/DH8D.md) |
| Embraer E190 | Curved 2+2 section, fitted windows, rounded ram-horn controls and original E-Jet panel | [E190](aircraft/E190.md) |
| Airbus A220-300 | A220 deck architecture; offset 2+3 cabin furnishings; smooth window trim and canted winglets | [A223](aircraft/A223.md) |
| Airbus A320ceo | Airbus windshield/post geometry; recessed passenger windows; sculpted FCU and sidesticks | [A320](aircraft/A320.md) |
| Airbus A321neo | Calibrated shared family width, longer cabin depth and window construction; shared Airbus deck | [A21N](aircraft/A21N.md) |
| Boeing 737-800 | Shaped NG six-display deck and windshield; fitted 3+3 cabin; variant-correct close nacelle detail | [B738](aircraft/B738.md) |
| Boeing 737 MAX 8 | Four-display MAX panel, fitted windshield/eye and sculpted Sky Interior windows/bins | [B38M](aircraft/B38M.md) |
| Airbus A330-900neo | Curved 2+4+2 Airspace cabin/bins; fitted windows; A330 deck with coherent inside/outside pane map | [A339](aircraft/A339.md) |
| Airbus A350-900 | Sculpted 3+3+3 twin-aisle cabin; independent window stations; six equal-size screens with angled lateral units | [A359](aircraft/A359.md) |
| Boeing 787-9 | Large dimmable window assembly; curved 3+3+3 cabin/bins; shaped shared 787 deck and variant fit | [B789](aircraft/B789.md) |
| Boeing 787-10 | Shared 787 dimmable windows/deck; longer cabin continuation and distinct stations without stretching fittings | [B78X](aircraft/B78X.md) |
| Bell 412EP | Fitted doors/glazing, two-blade tail-rotor geometry and rounded skids; future EP flight deck/rescue cabin are separate new scope | [B412](aircraft/B412.md) |

## Recommended implementation sequence

1. **Shared window and cabin structure (highest impact, M).** Give windows their
   own contour, dimensions and longitudinal stations; fit the inner lining to the
   real hull section. Add curved walls/ceiling, fitted seat eyes and bounded cabin
   continuation. Prove the mechanism on ATR42, A320 and B789, then fit every other
   passenger profile. Three different sections exercise compact, narrowbody and
   twin-aisle cases; passing one generic example is insufficient.
2. **Family-specific cockpit structure (high impact, M–L).** Shape windshield
   posts, roof, glareshield, panel rake, pedestal and overhead groups. Share genuine
   A320/A321, 737 NG/MAX and 787 family construction; retain separate equipment
   arrangements. ATR and Q400 need separate visual profiles within their shared
   tools. Existing display state suffices; working switches and avionics are not
   part of this work.
3. **Cabin furniture and finishes (high impact, M).** Build reusable shaped seats,
   bin doors, passenger-service units, trim and shade components with type-specific
   dimensions. Retain Saab/A220 asymmetry and widebody aisle structure. Explicitly
   choose cabin generation before applying optional Airspace/Sky/retrofit fittings.
4. **Materials, light and human context (medium–high impact, M).** Distinguish
   cloth, lining, rubber, metal and glass; give cabins bounded indirect light
   appropriate to day/night. Then consider seated passengers, visible hands or a
   co-pilot using existing approved character assets after seated-pose suitability
   is verified. Occupancy is a design proposal, not evidence that suitable poses
   already exist. Keep exterior window-depth hints cheap and separately owned.
5. **Framing and small exterior corrections (medium impact, S–L).** Tune seated
   gaze and glances after fitting geometry. Preserve real wing/nacelle visibility,
   banking, weather, existing flight information and stable watched identity. Fix
   aircraft-specific exterior discrepancies only where reference/source evidence
   warrants them; do not move wings for an attractive passenger composition.

Fourteen agents are useful for independent reference work. Implementation should
use one owner for each shared builder/material/camera file, with aircraft workers
owning disjoint profile or model data. Fourteen simultaneous edits to the same
interior builder would create conflicting assumptions and unnecessary merges.

Bell's supported exterior can be improved independently. A pilot-eye deck and
medical/utility cabin require new profiles, view availability and visibility rules;
do not silently give it an EPX glass cockpit or a passenger-airliner interior.
The exact rescue cabin fit needs its own verified reference before modelling.

## Acceptance and evidence boundary

Before visual implementation is called realistic, document hull/window/eye fits,
independent seat/window spacing, cabin grouping, family panel identity, bounded
geometry/material ownership and renderer restoration. These source checks help
reject incorrect construction; they cannot prove the result looks good.

Later visual acceptance should compare actual player cockpit forward/side/down/
overhead, both outward and inward passenger glances, exterior orbit and repeated
view switching. Include day, dusk, night, rain, banking and takeoff/climb, with
HUD readability and frame cost. Those checks require a later user-authorised
runtime review; none was performed or made a prerequisite for this analysis.
Historical fixtures and manufacturer imagery are reference evidence only.
