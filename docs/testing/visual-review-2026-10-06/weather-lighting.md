# Weather / lighting static review — main 000ab56b

Read-only source review; AGENTS.md, current GAME.md handoffs and VISUAL_BACKLOG_2026-10-06.md read. No source edits, game launches, captures, builds or tests. Findings below establish code-path facts, not observed appearance or measured performance. Existing owner visual acceptance remains intact. Paths below are relative to game/Airside/Assets/ unless stated otherwise.

## Five strongest concrete findings

### 1. P2 — weather moves toward the reported wind source
**Static error.** `Airside/Presentation/AirsidePrototype.Sky.cs:1241-1248`, `AirsidePrototype.WeatherEffects.cs:75-78` and `AirsidePrototype.Atmosphere.cs:77-79` turn `PresentationWind.DirectionDegrees` directly into a positive drift vector. But `Airside/Simulation/RunwayWeather.cs:163-164` makes wind aligned with a runway heading a positive headwind, establishing the meteorological *from* convention. The live sample constructs the same SurfaceWind (`LiveWeather.cs:95`); no caller reverses it.

**Trigger/consequence:** wind 050° at nonzero speed produces +X cloud/rain drift even though 050° wind blows toward -X in the runway-local frame. The ceiling and drifting fog receive the same reversed vector. This is independent of subjective weather styling. Windsock geometry has its own local axis; do not blindly add 180° there without verifying its fabric axis.

**Fix:** one pure wind-to-world *flow* helper using `DirectionDegrees + 180`, shared by cloud drift, rain drift and shader globals. Preserve tower headwind logic. Speed styling (minimum 1.5 m/s clouds, constant 7 m/s shader globals) is a separate design choice.

**Meaningful headless acceptance:** cardinal and 050°/230° table tests assert world flow is opposite the reported from-bearing, including wrap at 360°. Test cloud/rain/global data builders all consume the helper and calm magnitude is explicitly specified. Looks still require rendering.

### 2. P2 — H1 confirmed; custom terrain misses the fill light as well as point/spot lights
**Static integration gap.** `Airside/Art/Shaders/AdelaideGround.shader:246-250`, `Surroundings.shader:142,191`, `SuburbBuildings.shader:87-94` use only main light + SH, with no additional-light keyword/loop. `AirsidePrototype.Lights.cs:398-406` creates a second directional fill; `AirsidePrototype.Sky.cs:556-567` animates it each frame. Landing/taxi spots are enabled at `Lights.cs:264,300` subject to AircraftLights. Apron flood intensity is set from daylight at `Sky.cs:685` onward. These real additional lights can affect Lit pavement/aircraft but cannot affect the custom grass/land/building shader RGB.

**Trigger/consequence:** night apron/landing lamp overlapping grass, or any shadow-facing terrain relying on the opposite directional fill. The shader cannot receive that light; the visible severity is render-dependent. Suburb non-reception may be deliberately retained for cost and must be recorded as such.

**Fix correction to backlog H1:** `Settings/PC_Renderer.asset:56` sets `m_RenderingMode: 2` (Forward+), package manifest pins URP 17.3. A naive per-object additional-light loop is insufficient: use the pinned URP Forward+ compatible keyword and clustered light loop, including extra directional lights, with correct required InputData and distance/shadow attenuation. Verify names against that installed URP version; do not copy an older recipe. Introduce ground first and document whether suburbs remain simplified.

**Acceptance:** source-contract check can establish keyword/light-loop/attenuation presence, but is not shader compilation. Compile the shader in pinned Unity/URP; eventual small controlled capture with grass and Lit pavement under one extra directional, point and spot verifies reception. Headless arithmetic tests of the extracted light accumulation prove zero light contributes zero and distance/shadow attenuation are applied. No pixel test can honestly be replaced by a text search.

### 3. P3 — configured SSAO never enters these custom surface lighting equations
**Static integration gap, separate from missing light sources.** `Settings/PC_Renderer.asset:73-81` enables SSAO with `AfterOpaque: 0`, so this is lighting-integrated AO rather than an after-opaque framebuffer multiply. The custom shaders above neither declare `_SCREEN_SPACE_OCCLUSION` nor sample the screen AO factor. `AdelaideGround.shader:239,250` samples material-map AO and uses it on SH; that is not the SSAO buffer. `Surroundings.shader:191` and `SuburbBuildings.shader:94` also use SH directly. DepthNormals passes supply input to the AO calculation but do not themselves apply AO to surface colour.

**Trigger/consequence:** an object contacts grass or landscape while AO is enabled. SSAO can be generated from that contact, but these surfaces do not use it in their forward RGB. Existing AO presence therefore does not prove contact shading coverage; the appearance remains unobserved.

**Fix:** implement the URP 17.3 screen AO variant and direct/indirect factors in custom lighting, or intentionally exclude these surfaces and document the limitation. This can share the H1 shader integration work while retaining separate acceptance.

**Acceptance:** static contract verifies actual AO sample + direct/indirect multiplication, not merely a keyword; pinned-URP shader compilation remains required. Later native capture toggling SSAO under a grounded object distinguishes receiving grass from pavement. Treat AO strength as a render-required design choice.

### 4. P3 — generated star colour and brightness variation is discarded
**Static data/material mismatch.** `AirsidePrototype.Sky.cs:875-895` generates per-vertex warm/cool RGB and brightness, then `:907` calls `mesh.SetColors(colors)`. `StarSharedMaterial` at `:922-929` requests UnlitSky. `AirsideMaterialLibrary.cs:381-388` selects stock URP/Unlit (fallback Unlit/Color); `:398-409` returns before other surface setup. `AuthoredMaterialKey` at `:684-698` has no UnlitSky template, so no authored shader intercepts this path. Stock URP Unlit does not multiply mesh COLOR into the output. Every star instead receives the same material/property-block RGB from `Sky.cs:987-988`; size variation survives, generated colour/brightness does not.

**Trigger/consequence:** night star field active. Authored random star temperatures and vertex brightness cannot be displayed. This is a proved unused data channel; whether variation should be preserved is an optional visual design decision.

**Fix:** a dedicated unlit vertex-colour star shader, multiplying vertex colour by the global fade/twinkle tint. Avoid changing all UnlitSky users (sun and sky dome).

**Acceptance:** headless generation fixture proves different star vertex RGB; source contract connects COLOR to fragment result; pinned shader compile confirms the implementation. Render only needed to judge chosen intensity and appearance.

### 5. P3 — star shell remains opaque geometry only 512 m from the camera
**Static geometric/render-state error; impact conditional on a matching ray.** `AirsidePrototype.Sky.cs:870` sets radius `128 * StarDistanceScale`, and `AirsidePrototype.cs:3144` still sets scale 4, giving 512 m. `Sky.cs:913-916` assigns stock opaque UnlitSky; `AirsideMaterialLibrary.cs:393-409` returns without turning off depth writes or changing Geometry queue. The shell follows the camera (`Sky.cs:974-975`). `Airside/Simulation/SkyTraffic.cs:55` now draws traffic out to 26,000 m. A star triangle on the ray to any aircraft farther than 512 m is nearer opaque geometry and wins the depth test. The comment that 430 m covers the flight envelope is stale.

**Fix:** render a dedicated star pass as celestial background with ZWrite Off, keeping foreground opaque/transparent geometry in front; sun/moon already establish the desired background pattern. Merely increasing the radius needs far-clip-aware testing and does not resolve future envelope expansion.

**Acceptance:** headless render-state contract asserts star material/pass ZWrite Off and background queue; a numerical fixture using a star ray and target at 1 km documents current nearer-surface failure. Native depth ordering verification is still required for opaque aircraft and transparent weather. This does not claim any observed missing aircraft.

## Backlog disposition

- **H1:** confirmed missing additional lighting. Correction: Forward+ and extra directional fill must be handled; per-object count 24 is not the Forward+ scene-light budget. Suburb simplification can be deliberate.
- **H2:** confirmed Surroundings and SuburbBuildings have no ShadowCaster and FallBack Off. Hill-shadow desirability is a design/render question, not a proved defect. PC shadow distance is 140 m (Medium 55 m), so simply adding a pass will not produce distant hill shadows in the default overview. Do not silently extend shadow distance for this optional item.
- **H3:** confirmed PC LDR grading value 0 + HDR enabled + ACES/Bloom volume. No static proof of broken output; keep as controlled native A/B hypothesis, not an automatic one-line fix.
- **F1:** cannot verify historical 37 fps or any current frame-budget failure from source. Caches already remove recurring cloud renderer-array allocation (`Atmosphere.cs:41-44`) and puddle/spray renderer caches are populated once. Rain is one dynamic mesh, 768 quads/3,072 vertices/1,536 triangles, reuses its vertex array, one renderer/draw rather than 768 renderer draws (`WeatherEffects.cs:9-54,84-103`). Medium's legacy RainDropCount helper has no current production caller; both tiers still rebuild all 768 rain quads. A performance risk to measure, not a proved defect.
- **F1 bounded layer audit:** 16 cloud proxies + up to 16 umbras (`Sky.cs:1153`, prototype constant16), cloud shader maximum16 ray steps per fragment (`WeatherVolume.shader:85`), with two cloudDensity calls per step; each density evaluates two 3D noises and eight hash corners per noise. One stratus sheet, 24 horizon quads and one fullscreen height-fog quad (`Atmosphere.cs:16-17,170-190`). Fog already uses analytic height integration plus three horizontal noise samples (`HeightFog.shader:56-75`). These cap geometry and loop work, not screen coverage or GPU time. A layer-count test cannot prove an fps budget.
- **F2:** ACES/Bloom/Vignette/ColorAdjustments and SSAO existence confirmed. `docs/plans/visual-overhaul-plan.md:86-89` is a scope list, not explicit absent-feature assertion. Document partial delivery rather than saying every Phase4 item was missing or now complete. SSAO coverage needs finding3. Apron reflection probes and wetness updates already exist (`Sky.cs:115-121`), so “wet-ground reflections remaining” is too broad; an upgrade/quality acceptance may remain.
- **C1:** lack of tree vertex sway confirmed. Trees and buildings share SuburbBuildings tiled meshes (`AirsideAdelaideSuburbs.cs:20,164-171,301` onward). A naive wind offset by world height would also bend houses and move entire crowns relative to their anchor. Any implementation needs explicit vegetation masks + per-tree root/pivot data (or separate tree renderer) before a sway term. This is new optional polish. Weather global currently has fixed magnitude7 and stops being updated when WeatherLayers is off (`Atmosphere.cs:71-79`); reusing it alone gives stale motion when toggled off and cannot produce zero amplitude at calm. Use separate bounded tree wind input, explicitly zeroed when disabled, and test tree roots/building vertices stay fixed.

## Rejected apparent defects / boundaries

- Sun and moon at420 m are **not** the same depth bug as stars: `Sky.cs:1001-1002,1017-1018,1032-1033` explicitly set ZWrite0 and Background queue. They are drawn before foreground opaque geometry; their radius alone does not prove foreground occlusion.
- WeatherLayers off hides clouds and atmosphere but not rain (`Sky.cs:75-80`). Label says “Weather layers”, and the intended scope is not documented clearly enough here to call rain continuation a defect. Resolve scope before changing it; operational weather must remain unchanged.
- Current geometry/noise counts, fixed-speed atmosphere drift, night brightness, missing distant hill shadows and LDR-vs-HDR grading do not establish unacceptable visuals without a render or owner choice.
