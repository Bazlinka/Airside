# Airside finished-product vision

Generated 7 October 2026 for Bailey's visual planning request. Five current concepts based on the Unity project, approved art direction, airline career plan and refined interface. These are proposed visual targets for review, not gameplay captures or a change to the approved product plan.

| Image | Planning purpose |
|---|---|
| [Finished gameplay](concept_airside_finished_gameplay_v01.png) | Adelaide character, airport activity, camera composition, materials and lighting |
| [HUD detail](concept_airside_hud_detail_v01.png) | Navigation, selected-aircraft inspector, service states and next action |
| [Aircraft fidelity](concept_airside_aircraft_fidelity_v01.png) | Regional-to-international visual quality, doors, engines, landing gear and painted surfaces |
| [Fleet and routes](concept_airside_fleet_route_interface_v02.png) | Spacious comparison rows, selected-aircraft actions and contextual network map |
| [Turnaround close-up](concept_airside_turnaround_closeup_v01.png) | Aircraft follow view, ground equipment, passenger scale and service choreography |

Fleet interface v01 is retained as generation evidence; use v02 for planning. It removes invented navigation categories and improves the off-map Singapore connection.

## What to build toward

The ambition is a believable, calm Australian airport that rewards close inspection. Keep the approved premium stylised-realism direction; treat the photographic finish of these generations as an aspiration, not a verified Unity rendering or performance promise. Realism should come first from correct silhouettes, scale, service locations and motion, then materials and lighting.

| Area | Concrete implementation work | Acceptance target |
|---|---|---|
| HUD | Thin status bar; five navigation destinations; inspector only on selection; fixed primary actions; optional contextual flight list | Readable at 1280x720 and 1440x900; no overlap or hidden actions; at least roughly two-thirds of the overview remains visible with inspector open |
| Aircraft | Author type-specific meshes with separate doors, gear, rotors/fans and lights; consistent livery UVs; staged LODs | Correct dimensions and engine/propeller counts against sourced aircraft references; flush doors; tyre contact; correct port-red/starboard-green lights |
| Surfaces | Shared physically based painted-metal, glass, concrete and asphalt materials; valid UVs and tangents; modest wear decals | No plastic appearance, stretched maps or excessive seams; shape reads at overview and follow distance |
| Airport | Refine existing geographically sourced Adelaide layout, terminal facade, apron markings and near-camera landscape | Retain real routing and stand geometry; plausible terminal/aircraft scale; no invented geography copied from the concepts |
| Ground activity | Place each service at its actual type-specific door or wing zone; animate stairs, dollies, hoses, crew and passengers from simulation state | Safe propeller/engine clearances; no intersecting people or vehicles; visual activity agrees with the HUD |
| Lighting | Establish a repeatable daylight scene first; then dusk, night and wet-weather variants | Readable service zones at all times; restrained reflections; acceptable measured frame time on the target Mac |

Suggested sequence: agree HUD hierarchy and one daylight camera; finish one Saab 340 and its turnaround; match airport materials and lighting; expand the established quality to domestic/widebody aircraft; validate dusk/night/weather and performance. Finish one coherent scene before multiplying asset production.

## Realism corrections to apply during production

Generated images are visual references, not aircraft drawings, geographic data or operational instructions. In particular:

- The generated Saab profiles can overstate fuselage length/window counts, and the close-up rotor can show an incorrect blade count. Use sourced Saab 340B dimensions and four-bladed propellers when modelling.
- Service placements, gear arrangements and door shapes need type-specific reference checks. Verify boarding/refuelling concurrency against the chosen simulation policy; do not infer procedure from an illustration.
- The HUD background may show baggage near the passenger entry; production loading must use the appropriate cargo door.
- The turnaround card says boarding is next while passengers are already on the stairs. Production presentation and status must agree in the same frame.
- Aircraft seats, times, funds and reliability are illustrative mock data. The 787-9 sheet's engine/gear shapes require technical reference checks.
- The route map is schematic and still has approximate coastline/location placement. Build the actual map from the project's geographic data.

Render all runtime text and controls in Unity. These PNGs remain under documentation and must not become clickable screenshot interfaces or substitutes for 3D assets. Current approved references and runtime fallbacks remain authoritative until a candidate is approved and integrated.

## Source and generation evidence

Generator: OpenAI `image_gen.imagegen`, six calls (five initial images and one interface revision). Exact prompts and available settings: [`../../prompts/airside_finished_vision_20261007.json`](../../prompts/airside_finished_vision_20261007.json).

Source context: `GAME.md`, `docs/product/PROJECT_PLAN.md`, `docs/product/AIRLINE_PROGRESSION_AND_HUD_PLAN.md`, `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`, and existing reference/preview images inspected in the chat. Initial generations used written prompts without image references. Fleet v02 used fleet v01 as its editing reference.

Rights: generated output under applicable OpenAI service terms; no third-party source images supplied to generation. No attribution requested by the tool. Actual generation cost and model/version were not exposed; do not assume zero cost or a CC0 licence. No runtime imports. Fallback: existing approved art and current interface.
