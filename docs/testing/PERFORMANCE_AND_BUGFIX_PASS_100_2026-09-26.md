# Performance repair and 100-gate bug-fix pass — 26 September 2026

Branch: `codex/performance-100-bug-sweep`  
Baseline: `a467c50f` (`main`)  
Target: Unity 6.3 LTS macOS package, normal Adelaide map, 1280×720, clear pinned daylight

## Task packet

- **Player-visible outcome:** restore smooth graphics-on play without removing aircraft glass,
  while running a whole-game 100-gate defect pass and repairing every reproduced fault in scope.
- **Scope:** runtime aircraft rendering, soak diagnostics, Unity object cleanup, asset metadata,
  the asset audit, tests and release evidence.
- **Invariants:** no simulation, route, economy, save-schema, input, camera or authored aircraft
  geometry change; source part names remain available to animation and tooling; all visual assets
  retain their existing licence/source records.
- **Acceptance:** a like-for-like packaged comparison must materially improve frame pacing; glass
  must remain visible in an inspected close view; all 100 named gates and the complete Unity suite
  must pass; asset and aircraft validators must pass; the Mac package must build and launch cleanly.

## Diagnosis

The game update was not the limiting work. The original package spent about 2.3 ms in the measured
update stage but roughly 17 ms on the render thread. A Unity CPU trace had already placed about
12.6 ms in `DrawTransparentObjects`. The new renderer census identified the fan-out: 1,792 active
individual cabin-window renderers plus windscreens and fitted reflections, totalling 2,775
transparent material submissions. Every pane was correct art, but every pane was submitted as a
separate transparent renderer on every visible aircraft.

`AirsideAircraftRenderBatcher` now combines only static glazing meshes that share a material. It
keeps every named source transform and renderer for animation, tests and art tooling, marks the
source renderers off, and renders one uploaded mesh per aircraft/material group. Articulated doors,
lights, fans, gear and all opaque geometry are untouched. Runtime-generated meshes have an explicit
owner so rebuilding or destroying an aircraft releases their memory.

## Like-for-like packaged result

The baseline and revised package were launched one after the other at the same wall-clock/timetable
state, resolution, weather and camera. The steady-state heartbeat changed as follows:

| Metric | Baseline | Revised | Change |
|---|---:|---:|---:|
| Frame rate | 40 fps | 60 fps | +50% |
| p95 frame time | 25.8 ms | 16.8 ms | -35% |
| CPU main-thread frame | 25.0 ms | 16.7 ms | -33% |
| CPU render-thread frame | 17.0 ms | 5.7 ms | -66% |
| Draw calls | 4,297 | 3,757 | -13% |
| Batches | 4,244 | 3,642 | -14% |
| SetPass calls | 1,047 | 453 | -57% |
| Frames over 33 ms in final window | 0 | 0 | unchanged |

A separate 1.5-minute revised soak held 60 fps across five consecutive steady-state windows,
p95 16.9–17.2 ms, render thread 5.6 ms and zero frames over 33 ms in the last four windows. It
completed with no stall or logged exception. Evidence is in git-ignored `work/perf-*.log` files.

## Defects repaired during the pass

- Static panes, windscreens and fitted glass reflections generated thousands of transparent
  submissions. They are now combined by material per aircraft.
- Generated batch meshes outlived destroyed aircraft. `AirsideGeneratedMeshOwner` now releases
  them in play and edit mode; a regression proves cleanup.
- The soak renderer census tried to read CPU triangle arrays after meshes were uploaded as
  non-readable. It now uses index-buffer metadata.
- Static-batched scenery made the census multiply the whole combined mesh by every source
  renderer. It now counts only each renderer's assigned static-batch submesh range.
- Thirty-six presentation construction/destruction paths used delayed `Destroy` while EditMode
  tests were building objects, producing repeated Unity errors and leaving scratch components
  alive until a play frame that never arrives. They now destroy immediately outside play mode.
- Two aircraft test paths globally disabled Unity log-failure checking, hiding those errors from
  the rest of the run. The suppression is removed.
- Nine committed `.meta` files carried only 30 or 31 hexadecimal GUID characters. Unity ignored
  them and regenerated identity locally. Each is now a unique 32-character GUID, with repository
  reference and collision checks.
- The asset audit accepted GUIDs of any length, so all nine metadata faults passed the previous
  gate. It now requires exactly 32 hexadecimal characters and reports malformed values separately.
- The headless test harness attempted to compile the Unity-only batching regression. The explicit
  exclusion preserves its Domain/Simulation-only contract.

## 100 named verification gates

These are a curated whole-game slice of the authoritative Unity run. The remaining 21 fixtures in
the same run also pass; they are not substituted for any gate below.

| # | Verification gate | Tests | Result |
|---:|---|---:|---|
| 1 | `AdelaideAccuracyTests` | 7 | Pass |
| 2 | `AdelaideBuildingsTests` | 3 | Pass |
| 3 | `AdelaideDayPlanTests` | 9 | Pass |
| 4 | `AdelaideGroundLookTests` | 6 | Pass |
| 5 | `AdelaideGroundTests` | 6 | Pass |
| 6 | `AdelaideHourProfileTests` | 4 | Pass |
| 7 | `AdelaideLandCoverTests` | 7 | Pass |
| 8 | `AdelaideLandsideTests` | 4 | Pass |
| 9 | `AdelaidePavementTests` | 15 | Pass |
| 10 | `AdelaideStandMarkingTests` | 5 | Pass |
| 11 | `AdelaideTaxiRouterTests` | 4 | Pass |
| 12 | `AdelaideTerminalArchitectureTests` | 6 | Pass |
| 13 | `AerobridgeTests` | 7 | Pass |
| 14 | `AiTimetableTests` | 2 | Pass |
| 15 | `AircraftAssetTests` | 6 | Pass |
| 16 | `AircraftCatalogueTests` | 8 | Pass |
| 17 | `AircraftDispatchTests` | 9 | Pass |
| 18 | `AircraftOperationTests` | 4 | Pass |
| 19 | `AircraftPartsTests` | 71 | Pass |
| 20 | `AircraftPerformanceTests` | 8 | Pass |
| 21 | `AircraftPickRoutingTests` | 7 | Pass |
| 22 | `AircraftSkinUvTests` | 5 | Pass |
| 23 | `AircraftStatusTests` | 7 | Pass |
| 24 | `AircraftTitlePaintTests` | 10 | Pass |
| 25 | `AirlineCareerTests` | 20 | Pass |
| 26 | `AirlineOperationsTests` | 32 | Pass |
| 27 | `AirlineSaveRestoreTests` | 6 | Pass |
| 28 | `AirlineSaveTests` | 8 | Pass |
| 29 | `AirlineSoakTests` | 1 | Pass |
| 30 | `AirportCurfewTests` | 5 | Pass |
| 31 | `AirportSimulationTests` | 10 | Pass |
| 32 | `AirsideAircraftRenderBatcherTests` | 3 | Pass |
| 33 | `AirsideFramePacingTests` | 17 | Pass |
| 34 | `AirsideNamedChildrenTests` | 1 | Pass |
| 35 | `AirsideSettingsTests` | 3 | Pass |
| 36 | `AirsideStripMarkingsTests` | 8 | Pass |
| 37 | `AirsideTerrainGroundCalibrationTests` | 4 | Pass |
| 38 | `ApproachHoldTests` | 4 | Pass |
| 39 | `ApronSlabJointsTests` | 3 | Pass |
| 40 | `ArrivalClearanceTests` | 2 | Pass |
| 41 | `AwayCatchUpTests` | 6 | Pass |
| 42 | `BoardingFlowTests` | 6 | Pass |
| 43 | `CameraFeelTests` | 21 | Pass |
| 44 | `CampaignTests` | 12 | Pass |
| 45 | `CareerExpansionTests` | 20 | Pass |
| 46 | `CelestialSkyTests` | 9 | Pass |
| 47 | `CircuitProfileTests` | 19 | Pass |
| 48 | `CircuitTrafficTests` | 6 | Pass |
| 49 | `CollisionPass100Tests` | 6 | Pass |
| 50 | `ContractsWorkspaceTests` | 6 | Pass |
| 51 | `CrossStripContinuityTests` | 6 | Pass |
| 52 | `DayCycleAndLocationTests` | 11 | Pass |
| 53 | `DepartureTurnTests` | 8 | Pass |
| 54 | `DualRunwayTowerTests` | 5 | Pass |
| 55 | `EngineStartSequenceTests` | 4 | Pass |
| 56 | `EnrouteProfileTests` | 7 | Pass |
| 57 | `FieldMiniMapTests` | 17 | Pass |
| 58 | `FleetVisualTests` | 5 | Pass |
| 59 | `FleetWorkspaceTests` | 8 | Pass |
| 60 | `FlightBoardTests` | 19 | Pass |
| 61 | `FlightEconomicsTests` | 7 | Pass |
| 62 | `FlightPlannerTests` | 11 | Pass |
| 63 | `FullCareerTests` | 11 | Pass |
| 64 | `GoAroundRejoinTests` | 4 | Pass |
| 65 | `GroundMotionSmoothnessTests` | 4 | Pass |
| 66 | `GroundMotionTests` | 7 | Pass |
| 67 | `GroundSeparationTests` | 1 | Pass |
| 68 | `GroundServiceRunTests` | 16 | Pass |
| 69 | `HudHitTestTests` | 4 | Pass |
| 70 | `HudShellTests` | 8 | Pass |
| 71 | `LightningTests` | 10 | Pass |
| 72 | `LiveClockTests` | 5 | Pass |
| 73 | `LiveTrafficTests` | 12 | Pass |
| 74 | `LiveWeatherTests` | 14 | Pass |
| 75 | `MacRenderBudgetTests` | 3 | Pass |
| 76 | `MaintenanceTests` | 10 | Pass |
| 77 | `MeshNormalSmoothingTests` | 3 | Pass |
| 78 | `OperationsRealismTests` | 10 | Pass |
| 79 | `OperationsSummaryTests` | 10 | Pass |
| 80 | `OperationsWorkspaceTests` | 17 | Pass |
| 81 | `PresentationBugSweepTests` | 44 | Pass |
| 82 | `PresentationLayoutTests` | 100 | Pass |
| 83 | `RampCrewTests` | 10 | Pass |
| 84 | `RegionalCarriersTests` | 6 | Pass |
| 85 | `RouteMapTests` | 4 | Pass |
| 86 | `RouteMapWorkspaceTests` | 20 | Pass |
| 87 | `RunwayRubberMarksTests` | 3 | Pass |
| 88 | `RunwayWeatherTests` | 10 | Pass |
| 89 | `SkyTrafficTests` | 10 | Pass |
| 90 | `StandChoiceAndBoardHistoryTests` | 4 | Pass |
| 91 | `StandNamesTests` | 2 | Pass |
| 92 | `StatsWorkspaceTests` | 17 | Pass |
| 93 | `TaxiPathCleanupTests` | 3 | Pass |
| 94 | `TaxiRunwayNightTests` | 6 | Pass |
| 95 | `TaxiwayEdgeWearTests` | 3 | Pass |
| 96 | `TerminalGateOperationsTests` | 15 | Pass |
| 97 | `TerminalUndercroftMeshTests` | 1 | Pass |
| 98 | `TerrainFieldTests` | 19 | Pass |
| 99 | `TowerAndStandChoiceTests` | 6 | Pass |
| 100 | `WeatherTests` | 4 | Pass |

## Additional evidence

- Unity EditMode: **1,083/1,083 passed**, with log-failure suppression removed and no C#,
  malformed-GUID or unsafe EditMode-destruction warning.
- Asset audit: 1,312 unique GUIDs, 336 byte-identical runtime mirrors, 70 committed character materials.
- Aircraft source validators: all per-type scripts pass; all 13 fleet models connect to the
  fuselage within 5 cm; all 13 retain fitted paint, single-layer panes, recessed interiors and pilots.
- Script syntax: every shell script passes `bash -n`; all Python tools compile.
- Visual QA: revised overview and close 787 follow captures were inspected. The close capture
  retains every cabin/flight-deck aperture, reflection, title and registration and matches the
  baseline composition. Local evidence: `work/review/performance-100/` (git-ignored).

## Intentionally unchanged

- Save schema remains version 13.
- Domain/Simulation remain independent of Unity presentation.
- Airline schedules, movement reservations, costs, progression, world scale and camera controls
  are unchanged.
- No renderer class is hidden wholesale and no transparent pass is disabled.
