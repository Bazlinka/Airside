# Free CC0 source audit — 2026-09-09

## Purpose

Evaluate genuinely free, commercial-use-compatible 3D sources for the active
first-playable refine pass. This is an intake record, not runtime integration.
No reviewed source may replace an existing preferred kit unless it visibly improves
the Approved reference target in a Mac Play camera pass.

## Accepted licence policy

Only CC0 sources are eligible for this free-first pass. Keep the source URL,
creator, download date, licence evidence, checksum and original source outside the
runtime tree until an individual candidate is approved. A shipped replacement still
needs its own `ASSET_AND_DATA_REGISTER.md` row, Unity metadata, StreamingAssets
sync, and fallback verification.

## Reviewed sources

| Candidate | Source and licence | Technical finding | Decision |
|---|---|---|---|
| Rigged twin turboprop | OpenGameArt, `mfonasd`, [CC0](https://opengameart.org/content/twin-turboprop-rigged-airplane-low-poly) | Native `.blend`; about 29 m long. It has propeller, flap, gear, rudder and elevator bones, but its simple three-blade aircraft silhouette is materially below the existing `AIR-001 v06`, which already has the required six-blade prop meshes and named presentation parts. | **Reject as runtime replacement.** Keep `v06` preferred. Useful only as a future CC0 rig/reference donor. |
| Low-poly vehicle pack | OpenGameArt, `rgsdev`, [CC0](https://opengameart.org/content/free-low-poly-vehicles-pack) | FBX pack with separated wheels and colour materials; bus, fire truck, truck, van and car variants are available. The inspected bus is clean enough for a low-detail background but is not an improvement over Airside's preferred apron-bus kit. | **Do not integrate now.** Consider individual wheels/vehicle shells only if a specific existing kit fails visual QA. |
| Nature Kit | [Kenney Nature Kit](https://kenney.nl/assets/nature-kit), CC0 | 330 FBX/OBJ/GLTF-style low-poly assets. The tree-heavy teal/orange toy palette and pine/temperate silhouettes do not match the approved Kangaroo Island coastal scrub setting; the existing scrub v02 has a stronger local silhouette. | **Reject as runtime vegetation replacement.** Do not import isolated neutral pieces without an overview/follow comparison. |
| Interface Sounds | [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), CC0 | `select_005.ogg` is a restrained 0.383-second UI acknowledgement with no branding or visual-style conflict. Its SHA-256 is recorded in the asset register. | **Integrate.** Ships as `Resources/Airside/Audio/ui_select_005.ogg` and is played by both primary UI Toolkit controls and the Canvas fallback. |
| Wind whoosh loop | SketchMan3, [OpenGameArt](https://opengameart.org/content/wind-whoosh-loop), CC0 | 5.959-second stereo 48 kHz OGG described by its author as a loop. It is a suitable low-level exterior air-bed and materially more natural than the procedural noise fallback. | **Integrate.** Ships as `Resources/Airside/Audio/wind_whoosh_loop.ogg`; procedural `CreateWindClip()` remains the missing-file fallback. |

## Free sources still worth sampling, one at a time

| Need | Candidate | Why it remains worth a controlled comparison |
|---|---|---|
| Trees, scrub, grass and rocks | [Kay Lousberg Forest Nature Pack](https://kaylousberg.com/game-assets/forest-nature-pack) (CC0) | FBX, glTF and OBJ; the free tier has more than 100 stylised vegetation models. It may improve distant belts, but must be palette-tuned and compared to the approved Kingscote scrub board. |
| Landside parked cars | [Kenney Car Kit](https://kenney.nl/assets/car-kit) (CC0) | 45 transport models suitable for background/parking variation; assess one model before importing the pack. |
| Surface source maps | [Poly Haven](https://polyhaven.com/) (CC0) | Useful PBR source material, but each map needs Airside's stylised colour/roughness treatment and must not introduce photo-real mismatch. |

## Rule for the next intake

Download and stage **one candidate only**. Convert or import it, rescale to metres,
check root/pivots/materials and compare it against the existing preferred kit in
the default overview and follow cameras. Commit it only when it wins that visual
comparison; a free asset is not automatically an improvement.
