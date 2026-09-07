# Asset and data register

Every external asset or dataset must be added here before it enters a distributable build. Unknown rights block public release.

| Item | Owner or source | Use | Licence | Attribution | Evidence | Status |
|---|---|---|---|---|---|---|
| Prototype geometry | Airside project | Runway, buildings and aircraft made from Unity primitives | Project-owned configuration | None | Source in `AirsidePrototype.cs` | Approved |
| Prototype colours and interface copy | Airside project | Greybox visual target and status panel | Project-owned | None | Source in repository | Approved |
| Prototype engine tone | Airside project | Procedurally generated sine-wave audio | Project-owned generation | None | Source in `AirsidePrototype.cs` | Approved |
| Art direction and asset specification | Airside project | Canonical visual style, manifest and production rules | Project-owned documentation | None | `docs/art/ART_DIRECTION_AND_ASSET_SPEC.md`, decision 0022 | Approved |
| Batch A approved visual references | OpenAI image generation for Airside | Day/dusk masters, turnaround, HUD and scale/palette references | OpenAI service terms applicable at generation; release review required | None known | `docs/art/prompts/batch-a-reference-generation-2026-09-06.md` | Approved |
| Batch C first-playable 3D kits | Airside procedural generation | Turboprop, terminal/hangar/ops shed, service vehicles, equipment kit, livery atlases | Project-owned procedural; no third-party pack | None | `docs/art/prompts/batch-c-models-generation-2026-09-06.md`; runtime `ArtGltfLoader` | Approved · Integrated (unverified in Unity Play) |
| Batch C v03 denser kits | Airside procedural (`generate-batch-c-models-v03.py`) | Sibling `*_v03.gltf` kits with control surfaces / more building parts; prefer v03→v02→v01 | Project-owned procedural | None | `docs/art/prompts/batch-c-models-v03-generation-2026-09-07.md`; StreamingAssets sync | Integrated (prefer v03; Play unverified) |
| Batch C v04 denser kits | Airside procedural (`generate-batch-c-models-v04.py`) | Sibling `*_v04.gltf` kits (68-mesh turboprop, denser buildings/vehicles); prefer v04→v03→v02→v01 | Project-owned procedural | None | `docs/art/prompts/batch-c-models-v04-generation-2026-09-07.md`; StreamingAssets sync | Integrated (prefer v04; Play unverified) |
| Batch C v02 richer kits | Airside procedural (`generate-batch-c-models-v02.py`) | Sibling `*_v02.gltf` kits with more parts; v01 remains Approved fallback | Project-owned procedural | None | `docs/art/prompts/batch-c-models-v02-generation-2026-09-07.md`; StreamingAssets sync | Integrated (prefer v02; Play unverified) |
| Batch B world surfaces and kits | Airside procedural generation | Tileable asphalt/concrete/grass/metal, glass mask, wear decals, markings/lighting/props glTF kits | Project-owned procedural; no third-party pack | None | `docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md` | Approved (surfaces + WLD kits Integrated; Play unverified) |
| Batch B surface PBR companions | Airside procedural (`generate-batch-b-pbr-maps.py`) | `*_normal/_ao/_mask_v01` for asphalt/concrete/grass/metal; Lit via `AirsideMaterialLibrary` | Project-owned procedural | None | `docs/art/prompts/batch-b-pbr-maps-generation-2026-09-07.md`; StreamingAssets sync | Integrated (Play unverified) |
| First Resources stairs prefab | Airside procedural (`generate-passenger-stairs-prefab.py`) | `Resources/Airside/Prefabs/mdl_passenger_stairs_v01.prefab` + `AirsideRuntimeMaterialBinder`; Addressables key probe | Project-owned | None | ADR 0026; Prefabs README | Integrated (pipeline proof; Play unverified) |
| ARFF truck Resources prefab | Airside procedural (`generate-arff-truck-prefab.py`) | `Resources/Airside/Prefabs/mdl_arff_truck_v01.prefab` parked at ARFF shed | Project-owned | None | Prefabs README; Addressables locator auto-picks up | Integrated (pipeline proof; Play unverified) |
| Parked car Resources prefab | Airside procedural (`generate-parked-car-prefab.py`) | `Resources/Airside/Prefabs/mdl_parked_car_v01.prefab` landside parking + drop-off | Project-owned | None | Prefabs README; Addressables locator auto-picks up | Integrated (pipeline proof; Play unverified) |
| Landside furniture + coast boat prefabs | Airside procedural (`generate-landside-furniture-prefabs.py`) | `mdl_luggage_trolley_v01`, `mdl_landside_bench_v01`, `mdl_coast_boat_v01` | Project-owned | None | Prefabs README; Addressables locator auto-picks up | Integrated (pipeline proof; Play unverified) |
| Coast sand + water PBR surfaces | Airside procedural (`generate-sand-water-surfaces.py`) | `tx_sand_coast_*` / `tx_water_coast_*` basecolour+normal+AO+mask | Project-owned | None | `docs/art/prompts/sand-water-surfaces-generation-2026-09-07.md`; StreamingAssets sync | Integrated (pipeline proof; Play unverified) |
| Apron safety Resources prefabs | Airside procedural (`generate-apron-safety-prefabs.py`) | `mdl_fire_hydrant_v01`, `mdl_extinguisher_cabinet_v01`, `mdl_fod_bin_v01` | Project-owned | None | Prefabs README; Addressables locator auto-picks up | Integrated (pipeline proof; Play unverified) |
| ARFF shed Resources prefab | Airside procedural (`generate-arff-shed-prefab.py`) | `Resources/Airside/Prefabs/mdl_arff_shed_v01.prefab` rescue shed | Project-owned | None | Prefabs README; Addressables locator auto-picks up | Integrated (pipeline proof; Play unverified) |
| WLD/PRP denser kits v02 | Airside procedural (`generate-wld-prp-kits-v02.py`) | Sibling `*_v02.gltf` lighting/props/service kits; prefer v02→v01 | Project-owned procedural | None | `docs/art/prompts/wld-prp-kits-v02-generation-2026-09-07.md`; StreamingAssets sync | Integrated (prefer v02; Play unverified) |
| Aircraft skin PBR surfaces | Airside procedural (`generate-aircraft-skin-surfaces.py`) | `tx_aircraft_skin_*` basecolour+normal+AO+mask | Project-owned | None | `docs/art/prompts/aircraft-skin-surfaces-generation-2026-09-07.md`; StreamingAssets sync | Integrated (pipeline proof; Play unverified) |
| Batch E UI icons and panels | OpenAI + project-owned fix | Weather/operation/economy icons; regenerated service icons + dark panel; alert stripe | Mixed: OpenAI terms for original sheets; service/dark panel project-owned via `scripts/generate-batch-e-ui-fix.py` | None known | `docs/art/prompts/batch-e-ui-generation-2026-09-06.md` | Approved · Integrated (unverified in Unity Play) |
| Batch E UI-ICO-003 service icon sheet | Airside procedural (fix) | Valid PNG sheet + 7 sliced runtime icons | Project-owned | None | `scripts/generate-batch-e-ui-fix.py` | Integrated |
| Batch E UI-PNL-002 dark operations panel | Airside procedural (fix) | 128×128 Runway Ink ~89% mean alpha with edge | Project-owned | None | `scripts/generate-batch-e-ui-fix.py` | Integrated |
| Batch E UI-PNL-001 light panel texture | OpenAI built-in image generation for Airside | Verified correct (opaque, matches Cloud `#EEF1EC` closely); not currently used by any HUD element | OpenAI service terms applicable at generation; release review required | None known | `docs/art/prompts/batch-e-ui-generation-2026-09-06.md` | Generated — available, unused |
| BRD-001 Airside wordmark | Airside project; deterministic Pillow generation | Transparent light wordmark with project-owned runway/wayfinding mark | Project-owned | None | `scripts/generate-airside-wordmark.py`; `docs/art/prompts/brand-and-splash-candidates-generation-2026-09-07.md` | Approved · Integrated |
| UI-ILL-001 dawn splash | OpenAI built-in image generation for Airside | 3840×2160 splash derived from approved Batch A references; opening briefing backdrop | OpenAI service terms applicable at generation; release review required | None known | `docs/art/prompts/brand-and-splash-candidates-generation-2026-09-07.md` | Approved · Integrated |
| Unity engine and packages | Unity Technologies | Development and runtime | Unity terms applicable to the installed editor and packages | Review for distribution | Package manifest and Unity installation | Review at release |
| Real-world airport or map data | Not selected | Later location grounding | Unknown | Unknown | None | Excluded |
| Airline names, logos and liveries | Not selected | Possible later content | Unknown | Unknown | None | Excluded |

No third-party art, sound, map, weather or airline data is currently included.

## Generated asset evidence

An AI-generated file is not covered merely because the art direction is approved.
Add one row per delivered asset or coherent batch before it enters a distributable
build. The Evidence cell must link to the exact prompt record under
`docs/art/prompts/` and identify the generator/model, generation date, dimensions
and edit chain. Record any service terms relied on, monetary cost, required
attribution and the procedural or previous-asset fallback.

Planned items in the art manifest are not yet assets and are not approved for
runtime use. Real airline branding, watermarks, signatures and unverified
third-party reference material remain excluded.
