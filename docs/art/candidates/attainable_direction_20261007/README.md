# Airside: an attainable visual direction and a fresh interface

7 October 2026. Proposed direction for Bailey's review, based on actual packaged-game captures and native aircraft renders. This supersedes the earlier photographic finished-product vision as the planning recommendation. No runtime implementation or approved product-plan change is included.

## The target

A coherent, readable airline simulation with the airport and simple aircraft Airside already has. Keep the geographically sourced Adelaide world. Improve surface contrast, material consistency and readability before investing in new geometry. Explore a fresh horizontal airline-dispatch interface rather than reskinning the current vertical rail and floating cards.

| Concept | What it demonstrates |
|---|---|
| [Airport overview](concept_airside_attainable_overview_v01.png) | Existing airport/camera as the foundation, charcoal pavement, calmer roofs/ground, horizontal navigation and one bottom flight tray |
| [Dispatch workspace](concept_airside_dispatch_workspace_v01.png) | Light, opaque schedule board and adjacent rotation planner using existing flight/booking data |
| [Aircraft polish](concept_airside_aircraft_modest_polish_v01.png) | Modest material/shading improvements while aiming to retain current asset complexity |

These are image edits, not screenshots of implemented changes. The aircraft board's left column is also regenerated, not an untouched native capture; its 'Current native render' heading must be read as an illustrative reconstruction. The unmodified source below is the evidence. Generated edits cannot guarantee unchanged pixels, mesh silhouette or geometry. They are a visual hypothesis.

## Evidence inspected

- `docs/testing/full-map-refresh-2026-10-06/player-airport.png`: actual packaged-game airport overview, build `00e43f21`; used as generation reference.
- `docs/testing/full-map-refresh-2026-10-06/player-planning.png`: actual packaged route/booking surface from the same build; used as generation reference.
- `docs/testing/post-audit-p0-2026-09-30/terminal-airside-day.png` and `follow-jet-close.png`: earlier terminal and close-follow captures; establish asset/material appearance at closer range, not today's exact build.
- `docs/testing/aircraft-identities-2026-10-06/native/SF34.jpg`: native runtime-builder aircraft review, not a packaged flight capture; used as generation reference.
- Current `GAME.md`, approved art/product plans, `HudShell.cs`, `AirsideDayVolume.cs`, `AirsidePrototype.Sky.cs`, `AirsideMaterialLibrary.cs`, and project colour-space setting.

Latest packaged airport evidence available in this cloud checkout is 6 October, not a live run of the 7 October game. Later refinements are described in current source/docs but no current-build Mac capture was obtained. A fresh baseline capture is the first implementation step.

The available captures show very dark pavement against pale grass/roof/apron, busy near-ground textures, simple aircraft/buildings and overlapping traffic labels. Their cause is not established by looking at a screenshot. The project already has ACES/post-processing, surface materials and ambient/day profiles; it needs diagnosis and tuning rather than adding a second rendering stack. The project uses Gamma colour space. A switch to Linear is a separate, potentially broad migration, not assumed by this plan.

## Fresh interface proposal: airline dispatch desk

**Airport view:** one slim top bar for airline identity and navigation; one bottom tray for the selected or next player flight. Most of the airport stays unobstructed. Clicking an aircraft selects its flight in that tray. Clicking View rotation opens its details. Minimap/radar is optional. Selecting another operator's aircraft must retain its inspection/follow capabilities.

**Navigation:** Airport, Schedule, Network, Fleet, Career. Airport replaces the operations-view entry; Schedule opens rotations/flight board; Network opens the existing route map; Fleet retains bases/acquisition/maintenance; Career retains objectives and Contracts as an internal tab. No existing capability is silently removed.

**Progressive detail:** default tray shows one useful flight and next action. All flights expands into a scrollable list; do not squeeze the full long-career fleet into permanent cards. Expanded details expose existing service, blockers, cancel, follow, cockpit/cabin and maintenance actions with one primary action for the current state. Save/refusal/command feedback stays visible next to the action. Career goals appear contextually or in Career rather than in another permanent corner panel. Onboarding remains accessible during the first flight.

**Visual treatment:** warm off-white opaque surfaces, ink text, fine rules, restrained blue selection and simple rectangular controls. State uses both text and colour. No oversized pills, patterned translucent glass or decorative graphs. At night use a matching dark surface variant to avoid a glaring white frame; keep the same hierarchy and geometry.

**Responsive layout:** at 1440x900 and 1280x720 retain readable type and meaningful airport space. At smaller supported windows collapse secondary top navigation into Menu, stack the planner below the flight list and scroll workspace content. Keep primary actions reachable; no ellipsis hiding the only explanation of a dispatch blocker.

This is a structural layout change, not a coat of paint. Existing draw/layout/input layers and simulation commands can be reused, but the shared shell, hit regions, workspace reservations and layout checks will need coordinated changes. Validate with a one-aircraft career and a mature multi-base fleet.

## Practical implementation sequence

| Stage | Scope | Work | Evidence required before expanding |
|---|---|---|---|
| 1. Establish the baseline | Small, read-only | Capture current build from fixed overview, terminal and Saab-follow cameras in clear daylight. Record graphics settings, weather, frame time and resolution. | Actual current-build captures, not offline UI exports |
| 2. Test the fresh interface | Medium, shared presentation work | Prototype header + bottom tray + Schedule using existing commands. First test layout and interaction over the unchanged world. | At supported sizes: navigation, first-flight guidance, selection, booking, refusal messages, follow/cockpit/cabin, cancellation and scrolling work; no overlapping input regions |
| 3. Tune one daylight scene | Small to medium, bounded rendering work | Diagnose white/black clipping from exposure, source albedo, ambient and wetness separately. Tune shared material families and one day profile; reduce texture contrast where necessary. | Side-by-side captures from identical cameras/settings; markings remain readable; aircraft remain identifiable; target-Mac frame time does not materially regress |
| 4. Improve one Saab turnaround | Medium, one asset family | First fix UV/tangent/material issues and ground contact. Then reference-check silhouette, doors/gear and service locations; only add detail visible at follow distance. | Native views demonstrate fitted doors, grounded wheels and service clearance; geometry/memory cost recorded |
| 5. Extend the proven look | Incremental | Apply shared material improvements to terminal/GSE and other types; test dusk/night/wet weather, then selectively improve close-camera assets. | No glaring UI at night, legible night operations, correct wet-surface transitions and measured Mac performance |

Do not start by replacing the terminal, every aircraft or the statewide terrain. Do not commit to photorealistic people, engine interiors or airport architecture as the quality bar. Larger model replacements can be considered after the simpler scene is proven; they require sourced dimensions, rights, authoring/import work and LOD/performance acceptance.

## Boundaries and corrections

- Overview image removes aircraft labels for clarity; runtime should show the selected label, hover information and optional filtered traffic labels. Removing information everywhere would hurt operation visibility.
- Airport geometry, map outlines and aircraft shapes in generated edits may drift. Implement from existing project geometry/data, not the edited pixels.
- The Schedule concept combines the current Broken Hill flight with a next Kingscote plan. Booking must respect aircraft availability and existing reservation rules; no overlapping rotation is implied.
- The illustrative economics copied from the screenshot do not form a simple subtraction: +$188 is not $396 minus $337. Production must display authoritative settlement/offer data with a clear cost breakdown rather than reproduce those numbers.
- The small generated route map is approximate; use the current geographic map implementation.
- Frame-time improvement, exact effort and final quality are unverified. The scope bands above describe relative work, not delivery estimates.
- All runtime text/buttons must be rendered as live UI. None of these PNGs belongs in the Unity runtime tree.

## Generation/source record

Three OpenAI `image_gen.imagegen` edits using project-owned game-capture/native-render references. Exact prompts and source paths: [`../../prompts/airside_attainable_direction_20261007.json`](../../prompts/airside_attainable_direction_20261007.json). Transparent background false; no model/version, quality or resolution settings exposed. Actual cost not exposed. Generated output follows applicable OpenAI service terms; no CC0 claim. Reference captures contain the existing project's credited geographic data; retain its established source credits. No new external data was fetched. Existing approved assets/interface are the runtime fallback until a proposal is approved and implemented.
