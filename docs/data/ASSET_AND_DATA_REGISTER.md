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
| Batch B world surfaces and kits | Airside procedural generation | Tileable asphalt/concrete/grass/metal, glass mask, wear decals, markings/lighting/props glTF kits | Project-owned procedural; no third-party pack | None | `docs/art/prompts/batch-b-surfaces-generation-2026-09-06.md` | Approved (surfaces + WLD kits Integrated; Play unverified) |
| Batch E UI icons and panels | OpenAI + project-owned fix | Weather/operation/economy icons; regenerated service icons + dark panel; alert stripe | Mixed: OpenAI terms for original sheets; service/dark panel project-owned via `scripts/generate-batch-e-ui-fix.py` | None known | `docs/art/prompts/batch-e-ui-generation-2026-09-06.md` | Approved · Integrated (unverified in Unity Play) |
| Batch E UI-ICO-003 service icon sheet | Airside procedural (fix) | Valid PNG sheet + 7 sliced runtime icons | Project-owned | None | `scripts/generate-batch-e-ui-fix.py` | Integrated |
| Batch E UI-PNL-002 dark operations panel | Airside procedural (fix) | 128×128 Runway Ink ~89% mean alpha with edge | Project-owned | None | `scripts/generate-batch-e-ui-fix.py` | Integrated |
| Batch E UI-PNL-001 light panel texture | OpenAI built-in image generation for Airside | Verified correct (opaque, matches Cloud `#EEF1EC` closely); not currently used by any HUD element | OpenAI service terms applicable at generation; release review required | None known | `docs/art/prompts/batch-e-ui-generation-2026-09-06.md` | Generated — available, unused |
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
