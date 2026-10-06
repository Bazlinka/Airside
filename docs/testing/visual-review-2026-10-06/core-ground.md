# Core/environment/ground-services review

Read-only static audit of Airside `main` `000ab56b`, 6 October 2026. Read `AGENTS.md`, top `GAME.md`, and existing visual backlog. No Unity/app tests, source modifications, commits, or visual acceptance. Read glTF JSON accessor bounds as geometry evidence. Paths below are relative to `/workspace/Airside/`. These are actionable coordinate defects, not a reopening of Bailey's accepted visual style. Render impact/severity at overview still needs a capture.

## 1. P2 — Adelaide surf animation stretches the entire coast about the airport origin

**Evidence:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.cs:3277` changes each collected `Coast foam*` renderer's local Z scale. `AirsideAdelaideSurroundings.cs:489–493` puts `FoamQuad`'s actual runway-frame x/z coordinates directly into mesh vertices; `:511–513` creates the child at identity. `AdelaideCoastLandform.cs:114–151,158–168` uses real coastline endpoints without recentering. `Simulation/AdelaideCoast.cs:24` includes z=12930.2m. Coast, water and land use the same root, but only foam is scaled.

**Trigger/consequence:** Normal Adelaide coastal-motion update after collection (`AirsidePrototype.cs:3193–3196`). Applied scales are first layer 1.012–1.122, other layers 1.288–1.428. Every vertex moves by `(scaleZ−1)*z`, moving distant northern ribbons hundreds/thousands of metres off their source shoreline and changing their along-coast span. Root translation during flight-origin changes cannot cancel this relative displacement. This is code-proven; how noticeable it is from a particular camera needs rendering.

**Fix:** Keep identity geometry transforms for geographic foam. Animate alpha only, or pulse vertices along each segment's outward normal relative to its stored endpoints. If pulses in geometry are desired, use local segment pivots/widths rather than globally scaling a whole geographic mesh.

**Meaningful future headless acceptance:** For actual `AdelaideCoast` segments at minimum/maximum phase, verify each ribbon's midline stays within the designed metre-scale offset of its originating segment, endpoints retain their alongshore coordinate, and width stays within declared landward/seaward limits. Include positive and negative z examples thousands of metres from origin; an origin-only synthetic fixture misses this bug.

**Correction to Claude claim:** The 2.2 scale at `:3252` is overwritten in the same update because `_coastFoam` is also `_coastFoamLayers[0]`. Do not report a persistent 2.2 stretch. The final 1.012–1.428 scales still prove the defect. Primitive KI coast pads use different local geometry and should be treated separately.

## 2. P2 — Authored service tyres rotate about the kit origin, around the wrong axis; broad selection also rotates fixed wheels

**Evidence:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.GroundService.cs:197–200` rotates any descendant name containing `wheel` (except `arch`/`hub`) around its own +X axis. The glTF loader creates each mesh transform at zero/identity (`ArtGltfLoader.cs:49–51,165–169`) while positions stay baked in vertices. `BuildServiceVehicle` (`GroundService.cs:634–644,744,769–773`) does not rebake tyre pivots. `AirsidePrototype.cs:2009–2033` nests the kit below a −90-degree yaw; it does not alter each wheel's mesh-frame axis or recenter it.

The preferred `mdl_fuel_truck_small_v06.gltf` wheel_fl has POSITION bounds `[1.10,-0.02,0.48]` to `[1.70,0.58,0.72]`: axle centre `(1.40,0.28,0.60)`, tyre circular extent in XY, lateral axle along local Z. Node has name/mesh only, no translation. The baggage v06 tyre nodes similarly use offset POSITIONs and thin Z extents. Code rotates X, not their Z axle. A 90-degree X rotation changes fuel tyre centre `(1.4,.28,.6)` to `(1.4,-.6,.28)` inside the kit: ~0.936m displacement. Parent yaw rotates this displacement but cannot remove it.

**Trigger/consequence:** Fuel/baggage/catering ground vehicle moves through `UpdateVehicle`. Tyres orbit/tumble instead of rolling at the axles; names also include fuel `spare_wheel` and baggage `tug_steering_wheel`, so stationary accessories enter this same roll loop. Geometry/selection proven; on-screen readability needs a render.

**Fix:** Collect explicit road-wheel parts once, preserving source-dependent axle axis; rebake/cloned mesh centres to each axle without changing initial vertex world positions, then rotate around the authored +Z axle. Include hub parts under the axle pivot if they should co-rotate. Exclude spare and steering wheels by an explicit role/name predicate. Keep fallback/prefab axes separate from glTF assumptions.

**Meaningful future headless acceptance:** Load real fuel/baggage POSITION data and exercise production pivot/axis selection for 0°,90°,180°,360°. Assert axle-centre world invariance and axial/radial dimensions (not just vertex distance to origin), preserve initial world vertices, and verify spare/steering/arch parts are unchanged. Check independently created instances do not mutate the shared mesh cache.

**Rejected expansion:** Aircraft wheels already receive `RebakeWheelPivots` (`AirsidePrototype.cs:3875–3902`, model build call sites) and have their own spin logic. Do not generalise the service-vehicle finding to all aircraft tyres. Simple primitive vehicle wheels already have local centres; their defect is an axis question, not the same origin-pivot failure.

## 3. P2 — Boarding stairs use the aircraft motion datum as the actual apron surface

**Evidence:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.Boarding.cs:338–342` derives stair rise from `view.position.y`, docks root at that same Y, then builds geometry with ground-relative coordinates. Stair tyres (`:256–258`) have centre y=.42 and rotated vertical diameter .84, so their contact is local y=0. The fleet parked motion root is at `AirsideFlightPath.GroundY=.72` (`AirsideFlightPath.cs:47`, `AirsidePrototype.FleetVisuals.cs:276`). Adelaide apron top is .039: `AirsideBareField.cs:59–60` makes runway top `−.02+.14/2=.05`; `AirsidePrototype.YpadPavement.cs:27–29` subtracts .011. Neither `BoardingRoot()` (`Boarding.cs:225–230`) nor its trucks has a compensating Y offset. Aircraft geometry has separate model offsets e.g. −.68/−.7 (`AircraftVisualProfile.cs:62,77,108`) and must not be used as evidence that the motion root is physical ground.

**Trigger/consequence:** Jet uses stair-truck boarding on an Adelaide apron. Tyre contact is y=.72, 0.681m above the apron. Top platform still follows door sill because rise/root cancel each other, which can hide this datum mistake in a door-only audit; the bottom floats and stair rise/run are short. `TryWalkPath` (`Boarding.cs:1063,1090,1112–1113`) and remote bus stop (`:1157–1158`) also adopt the motion datum for apron legs.

**Fix:** Introduce a physical pavement/ground contact value for stand/road locations distinct from aircraft motion-root Y. Use it for truck root and stair rise calculation, passenger ground paths, and bus ground reference; continue deriving door tops from their transformed geometry. Check primitive KI/outstation values separately rather than hardcoding Adelaide apron Y globally.

**Meaningful future headless acceptance:** At real jet stands, use .039 pavement top and .72 aircraft motion root in the fixture. Assert stair lowest tyre contact matches pavement within tolerance, platform matches actual door sill, and stair rise/run reflect the physical height difference. Verify generated passenger apron route Y matches pavement; separately use the imported passenger rest/walk mesh minimum to validate actual feet contact. Include fallback/outstation datum cases.

**Confidence boundary/rejected wording:** The stairs' ~.68m gap is directly proven by primitive geometry. Passenger *path* elevation is also proven. I did not decode FBX/skinned walk frames, so I do not claim every passenger's visible soles are exactly .681m above apron; that requires validating imported foot offset. Also do not claim every service vehicle floats .7m: authored service kits already receive −.55m Y placement (`GroundService.cs:744`), so each kit needs its own contact calculation.
