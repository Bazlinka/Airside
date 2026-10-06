# World, buildings and asset loading — static audit

Read-only review of `/workspace/Airside`, main `000ab56b`, 6 October 2026. Read `AGENTS.md`, top `GAME.md`, art specification and the previous visual backlog. No source edits, Unity launch, app tests or headless suite runs. The only output is this scratch report. Small Python probes read shipped binary/glTF records to check arithmetic; they did not modify assets.

## Strong actionable findings (three)

### W1 — Suburb roof satellite coordinates omit the floating origin (P3)

**Evidence:** `game/Airside/Assets/Airside/Art/Shaders/SuburbBuildings.shader:90` directly computes satellite UV from `input.positionWS.xz`. Compare `AdelaideGround.shader:184,194,234` and `Surroundings.shader:136,144,167`, which declare the global `_AirsideFlightOrigin` and add it to world X/Z before lookup. This omission affects roofs, whose vertex alpha mixes satellite colour into the authored roof colour; tree and wall alpha is zero and therefore does not exhibit this satellite lookup defect.

**Complete coordinate chain:**

1. Suburb generator/data uses absolute runway-frame metres (`AdelaideSuburbData.cs` format/HouseCorners; `AirsideAdelaideSuburbs.cs:233-248,277-307` uses those values directly for vertices).
2. `AirsidePrototype.FieldBuild.cs:270` calls `BuildGradually(_airfieldRoot, ...)`. `AirsideAdelaideSuburbs.cs:173-174` parents the suburb holder to this root with identity local transform, and `:180-181` does the same for each tile.
3. `AirsidePrototype.FlightWorld.cs:55-70` updates the flight origin while watching a flight: once either absolute coordinate exceeds 80,000 m, the origin snaps to an 8,000 m multiple (`FlightWorldGrid.cs:11,13`). It moves `_airfieldRoot.position = -FlightOrigin` and publishes the positive origin to `_AirsideFlightOrigin`.
4. Thus a fixed runway-frame roof point P becomes shader world position P−O. Ground/surroundings sample `(P−O)+O = P`, but suburbs sample P−O and clamp it to the image edge. For P.x=1,000 m, O.x=80,000 m and satellite extent=12,000 m, correct U is 0.5417 while suburb U clamps to 0.
5. `ResetFlightWorld` (`FlightWorld.cs:81-85`) restores zero origin and correct roof sampling, so the defect is state-dependent. It is a coordinate error even though the suburb may be distant/fogged in many flight views; code alone cannot prove its visible severity.

**Fix:** declare the same global in SuburbBuildings and use `input.positionWS.xz + _AirsideFlightOrigin.xz` only for geographic texture lookup. Keep actual camera distance/fog in shifted world coordinates.

**Acceptance without app execution:** use a shared pure UV-coordinate rule or a shader-source contract plus mathematical cases for zero, positive and negative origins, verifying a fixed geographic roof point has invariant unclamped/clamped UVs. Native visual acceptance would still be separate.

### W2 — Non-aircraft glTF unwrap collapses most box faces (P2)

**Evidence:** `ArtGltfLoader.cs:506-525` claims to choose the two largest axes, but actually discards the longest axis. Every vertex is then mapped through that one global projection (`:528-536`). On a closed box any single global planar projection collapses two face families; discarding the longest span additionally removes texture variation along the long wall. `AirsideMeshUtil.HasUsableUvs` checks only presence of TexCoord0, so these collapsed mappings are treated as usable and textured materials are attached (`ArtGltfLoader.cs:177-183`).

**Concrete shipped asset:** the first mesh `shed_body` of `Models/Buildings/mdl_operations_shed_v05.gltf` spans X=6 m, Y=2.8 m, Z=4 m. The runtime function selects Z/Y. Reading its shipped POSITION and index accessors and evaluating the same projection gives **8 of 12 nondegenerate geometric triangles with zero UV area**. Long X-facing-direction wall edges share UVs along X, so authored colour/normal detail samples as a line along that span. The Resources `mdl_operations_shed_v05.prefab` uses builtin Cube/Cylinder meshes (`m_Mesh` built-in GUID at lines 114,197,280,...); `ArtPresentationLoader.cs:84-86,222-238` deliberately yields pipeline-proof prefabs to the available glTF, so this is a current path, not merely hypothetical unimported content.

**Fix:** unwrap box-kit faces per face normal, splitting vertices at UV seams as needed, or ship/use authored UVs in the procedural glTF contract. Merely swapping largest/smallest axis is insufficient for all six faces. Keep the aircraft-specific metre/cylindrical mapping separate.

**Acceptance:** extract a pure per-face unwrap seam; test the shipped shed body's triangles and representative roof/wall/box faces. Every nondegenerate face intended to take a 2D texture must have nonzero UV area and finite UVs, and the texture must vary along both face axes. A test for TexCoord0 presence alone will not catch this.

### W3 — Runtime glTF normal-mapped meshes have no tangent attribute (P2)

**Evidence:** `ArtGltfLoader.cs:276-294` constructs meshes, smooths normals, generates UVs and uploads, but never sets or recalculates tangents. `MeshNormalSmoothing` only writes normals; `AirsideMeshUtil.UploadKeepReadable` only uploads data. Combined meshes similarly have normals/UVs but no tangents (`ArtGltfLoader.cs:426-434`). Their URP Lit material path explicitly attaches `_BumpMap` and enables `_NORMALMAP` (`AirsideMaterialLibrary.cs:436-443`). Tangent-space normal mapping needs the tangent/bitangent basis, so attaching a normal texture cannot provide the intended surface detail on this path. Builtin/imported prefabs may have tangents; this finding is specifically the runtime-created glTF and combined mesh path, including the shed path above.

**Fix:** after valid UVs and normals exist, calculate tangents before upload for individual and combined glTF meshes. Repair W2 first for non-aircraft face UVs: recalculating tangents over zero-area UV triangles cannot produce a sound tangent frame. For combined meshes preserve/regenerate the appropriate basis after normals are computed.

**Acceptance:** native EditMode mesh assertions can check Tangent attribute exists, tangent count equals vertex count, all values are finite, tangent direction is nonzero and orthogonal to the normal, and handedness is ±1. A pure tangent calculator can be exercised headlessly if that route is chosen. Do not claim normal-map visual validation from an attribute-presence check or C# headless stubs.

## Previous B/C/D/E backlog verification and corrections

- **B1:** correctly retracted. Mow stripe constants and sine modulation are present (`AdelaideGround.shader:29-30,224-226`, `AirsideAdelaideGroundMesh.cs:22-23`).
- **B2:** camera-local grass tufts/bushes are optional content, not a proven missing-asset defect. Existing trees and specialist scrub do not establish the proposed near-ground tuft layer.
- **B3:** ELVIS terrain intake is genuinely deferred: ADR 0236 lines 23-25 says a manual data order is needed and shipped terrain is unchanged. It is not a reason for a code-only worker to replace terrain or invent a credit.
- **C1:** absence of tree wind is supported by the current SuburbBuildings shader, but remains optional motion polish. Weather-layer-off semantics proposed by the backlog are a design choice, not an existing invariant.
- **C2:** generic crown profiles are present, with baked Full/Medium/Billboard detail (`AdelaideTreeLod.cs` and `AirsideAdelaideSuburbs.cs:310-407`). Selecting real species from height/radius alone is speculative; keep it optional.
- **D1:** hipped houses and flat nonhouses are confirmed (`AirsideAdelaideSuburbs.cs:279-307`). No gable generator is present. This is content variation, not a regression. The count statement should be taken from current ASUB data rather than preserved from an earlier snapshot.
- **D2:** aggregate shipped suburb/tree mesh budget would be useful. Existing crown/LOD and specialist placement tests already cap individual tree shapes, so do not describe the project as having no geometry budgets. The claim “no native run” is stale: top GAME.md records native runs and packaged captures after integration, although physical input, full-flight weather and performance remain unverified. A code triangle budget cannot replace frame-time acceptance.
- **E1:** generated audit still records 78 airport buildings, seven sourced heights, 71 rule defaults and 60 unnamed. This is data accuracy debt, not a geometric programming error. The new LiDAR suburb/tree intake does not automatically source airport roof elevations.
- **E2:** the backlog's citation to BuildingDetail line 598 is **not Terminal 1 proof**: that region is generic hangar/freight pitched-roof treatment. Terminal detail is `BuildingDetail.ForTerminal:281-329`, with parapets, canopy columns, windows, facade fins/joints and rooftop plant. `AirsideAdelaidePavement.cs:193-213` defines raised skylight lanterns and three roof-plant blocks; `AirsidePrototype.YpadPavement.cs:508` instantiates those. Terminal swept-roof/atlas ambitions may remain optional, but “roof clutter absent” and “only dark clerestory band” must be retracted for Terminal 1.
- **E3:** overstated. Tiny procedural normal maps are fallbacks. `AirsideMaterialLibrary.cs:758-781,785-793` loads authored normal/AO/mask/basecolour v03→v02→v01; `ResolveNormal:616-625` prefers authored maps. The loader tangent omission W3 is a concrete reason surface maps fail to behave as intended on runtime glTF, rather than a fundamental art ceiling.

## Checked/rejected findings

- Ground and DEM row indexing is consistent: generated meshes use `[z*width+x]`, heightmaps use `[z,x]`, and AdelaideTerrainHeights.Sample uses `[zi*Count+xi]`. No X/Z transposition found.
- Adelaide terrain rectangle is centred on zero, so LandHeight's symmetric bounds match AdelaideGround. Do not borrow Kingscote's CentreZ=10 and report an Adelaide offset bug.
- `AirsideAdelaideSuburbs.MeshParts.ToMesh` selects UInt32 above 65,000 vertices, preventing the obvious large-tile 16-bit index overflow. Ground/far terrain meshes likewise select appropriate formats.
- Trees use bake-time distance-from-airport LOD rather than camera LOD, explicitly documented in ADR 0220. It can be improved for roaming cameras, but is not evidence of an indexing defect.
- Shader absence correctly skips suburbs; missing ground basecolour correctly selects a fallback. No claim that any shader lookup is unchecked.
- Static file inventory found no meaningful Art asset missing its .meta; only three `.gitkeep` placeholder files lack sidecars. This is not a missing-art defect, and does not stand in for full GUID/mirror audit.
- Additional-light and coast-foam audits are owned by other streams; omitted here to prevent duplicate findings.
