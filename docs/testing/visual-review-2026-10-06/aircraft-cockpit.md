# Aircraft and cockpit code audit — 6 October 2026

Read-only audit of `/workspace/Airside` at `000ab56b`. Read `AGENTS.md`, the current `GAME.md` handoffs and `docs/testing/VISUAL_BACKLOG_2026-10-06.md`. No game sources changed, no Unity/editor/player runs. Four concrete findings below; no optional styling changes counted as defects. Paths and lines refer to this revision.

## 1. P2 — Fan blur discs cover only about half of the real fan radius

**Location:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.AircraftVisuals.cs:2296–2314`, particularly line 2301.

**Trigger/consequence:** With propeller blur enabled, run a jet's engines fast enough for `ApplyJetFanBlurToHub` to hide its individual blades (same file, lines 495–526; `showBlades = blend < 0.92f`). The replacement disc is much smaller than the blade ring. The outer fan ring loses its blades at speed, leaving a small central disc rather than preserving the full fan face. This contradicts the intended non-see-through intake in the adjacent ADR 0168 comment.

**Coordinate chain checked:** `BuildNarrowbody7378` in `AirsidePrototype.cs:3557–3596` creates an identity/unit-scale root, instantiates the metre-authored kit with only its ground-height offset, nests blades, rebakes the fan hub, then builds discs. `RebakeJetFanPivots`/`RebakePropellerPivot` preserve world geometry. The disc size is calculated from the largest **individual blade's bounding-box half-width**, not that blade's distance from the hub. A radial blade's bounding box is centered partway along its radius, so its extents cannot represent rotor radius.

**Headless evidence run:** Used the repository's `render-aircraft-thumbnails.py.load_parts` on the exact runtime glTF/bin files, then reproduced line 2301 with each blade's XYZ extent and compared with blade vertices' XY radial distance from the fan center. At the initial identity pose on the shipped glTF path (an alternative imported prefab needs its own geometry check):

| Runtime type | Created disc radius (m) | Actual blade radius (m) | Fraction covered |
|---|---:|---:|---:|
| A320 | 0.461 | 0.777 | 59.4% |
| B38M | 0.461 | 0.780 | 59.1% |
| A359 | 0.613 | 1.420 | 43.1% |
| B78X | 0.606 | 1.417 | 42.8% |

**Fix:** Calculate maximum radial distance from the rebaked hub in **fan-local XY**, preferably using vertices (or bounds corners transformed to that space). Remove or increase the current 2.7 m diameter cap: A359's 1.420 m fan radius already needs ~2.84 m diameter before the small coverage margin.

**Meaningful regression:** Extend `AircraftDispatchTests.Boeing7378_BuildsArticulatedTurbofansAndUsesItsOwnWheelScale` across jet families. Assert that each built `FanDisc` radius contains the fan blade outer radius to a small tolerance at identity and under a rotated/scaled parent. Existing test only checks disc existence, blade count and wheel scale. An asset-only Python check can compare the inferred disc radius to shipped fan vertices immediately. A native screenshot is needed only to judge the resulting appearance, not to establish this geometric defect.

## 2. P3 — Aircraft navigation lights have port/starboard colours reversed

**Locations:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.Lights.cs:148–153` (`NavLensColor`) and `:184–188` (point-light colour). Initial material colours also reverse these in `AirsidePrototype.AircraftVisuals.cs:1690–1691`.

**Trigger/consequence:** Look at an active aircraft's wingtip navigation lights, especially after dark. The **right/starboard lamp is red and left/port lamp green**; the aviation convention is port red, starboard green. Both emissive geometry and the real point lights use the reversed mapping, so changing only the material default does not fix it.

**Coordinate/name chain checked:** `AirsideAircraftParts.cs:35–49` maps `nav_light_left` / `NavLight L` directly to `AircraftNavigationLight.Left`. These are not camera-relative names. The shipping B38M generator places `nav_light_left` at X=-17.89 and `nav_light_right` at X=+17.89 (`scripts/generate-air-005-narrowbody-737-8.py:706–707`). The aircraft faces +Z; cockpit pilot eyes and left-wing meshes use negative X, so there is no caller transform that exchanges the sides. `UpdateAircraftLightsAndGear` applies `NavLensColor` to those enums and calls `EnsureNavPointLight`.

**Fix:** Make Left red, Right green, Tail white in one shared mapping used by lens material and point-light initialization. Also correct initial kit colours so cold/first-frame geometry agrees.

**Meaningful regression:** Use a shared pure palette mapping to assert red dominance for Left, green dominance for Right and neutral white for Tail. If testing the runtime object, instantiate one authored type and verify that the negative-X named wingtip renderer and its point light are red, and positive-X ones green. Current naming tests do not check colour correspondence. No native screenshot required to prove the mismatch.

## 3. P3 — Touchdown smoke and skid marks fire before jets touch the runway

**Location:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.AircraftVisuals.cs:729–755`, especially line 732.

**Trigger/consequence:** Any non-ATR type reaches `AircraftPhase.Landing` with progress between the global ATR touchdown fraction and its own touchdown fraction. `UpdateTouchdownSmoke` fires once, spawns ground skid marks, starts tyre smoke and pulses the camera although the aircraft is still airborne. The actual contact event then cannot fire because `_touchdownFired` already contains the aircraft ID. Widebodies are almost a second early.

**Caller/path chain checked:** `UpdateAircraftVisual` obtains the fleet type and feeds it to the landing position through `PositionFor`; `AirsideFlightPath.Landing(t,lane,type)` (`AirsideFlightPath.cs:149–179`) does not reach GroundY until `AircraftPerformance.For(type).TouchdownProgress`. In contrast, the VFX gate uses `AirsideFlightPath.TouchdownProgress`, a direct alias for `CircuitProfile.TouchdownProgress` (ATR). `EmitTouchdownWheelSmoke` and `UpdateRollingWheelSmoke` resolve the correct type for speed, so this is the event gate, not an omitted type throughout the whole effect.

**Headless evidence run:** Recomputed the exact profile formulas from the checked-in constants. ATR's gate is 0.288502, versus B738 0.302903, A359 0.320401, B78X 0.321060. On each profile's landing duration that gives smoke 0.494 s early for B738, 0.985 s early for A359 and 0.997 s early for B78X (scheduled whole-second rounding changes only the last few milliseconds). Q400 also fires 0.395 s early.

**Fix:** Resolve the fleet aircraft type in this effect loop and use its performance touchdown fraction. Better, share a pure contact predicate with the landing path/VFX so the event cannot drift again. Preserve the ATR fallback for the demo and rotorcraft exclusion. Continue using real main-gear contacts for placement.

**Meaningful regression:** For each fixed-wing type, evaluate the landing path just before contact and assert the effect predicate false; at and after contact assert true, with once-only semantics. Include a B78X sample halfway between the ATR gate and its own contact fraction: current event condition is true while landing height is positive. Existing performance and cockpit tests verify type-specific path/attitude, not the smoke event. The defect is testable headlessly; a native follow view would only show its visual significance.

## 4. P3 — Cockpit wipers keep sweeping above the rain deck

**Location:** `game/Airside/Assets/Airside/Presentation/AirsidePrototype.Cockpit.cs:207`.

**Trigger/consequence:** Enter a supported cockpit during rainy/storm weather, climb above 1600 m with weather layers enabled. Rain presentation fades to zero above the deck, and sky/fog clear, but windscreen wipers keep sweeping because `SetEnvironment` receives the raw airport precipitation. This is a contradictory weather cue in the same spectator view.

**Envelope chain checked:** `CockpitWeatherEnvelope.RainAtHeight` in `CockpitWeatherEnvelope.cs:20` is zero at and above 1600 m. `AirsidePrototype.WeatherEffects.cs:61–63` multiplies the rain mesh's precipitation by this factor; its alpha is also multiplied by the envelope. `AirsidePrototype.Sky.cs:624–660` clears sky/fog above the deck. `CockpitInterior.SetEnvironment` (`CockpitInterior.cs:123–136`) runs wipers whenever its precipitation argument exceeds 0.04 and receives no altitude. No cockpit-specific code later overrides that sweep. This report concerns the altitude envelope; weather-layer toggles are a separate audit.

**Fix:** Pass the same altitude-adjusted precipitation used by the observed rain envelope to the cockpit interior, or expose a single observer-weather sample used by both. Preserve daylight/panel illumination and the wiper parking behaviour.

**Meaningful regression:** Extend `CockpitWeatherEnvelopeTests.RainStopsAboveDeck` beyond testing the helper in isolation: feed observer precipitation into a wiper state helper at 800/1450/1600 m; assert parked at 1600 m and during zero precipitation, and active below the deck during rain. Runtime transform checks can verify parked wiper rotation and position for the same frame times after applying the integrated observer sample. Existing weather tests prove the envelope math but do not exercise the caller in line 207.

## Checked exclusions / limits

- Cockpit geometry is seat-local and follows the posed parent; main-gear pitch lift is explicitly subtracted from cockpit AGL. Did not report double pitch or origin-rebase speed spikes: the caller paths handle them.
- Glazing batches are named `Cabin windows batch`; `PartsFor` deliberately matches `Cabin windows`, so dynamic window glow also reaches the rendered batch. Did not report glow as lost by batching.
- Exterior visibility checks parent names, preserving nested wings, engines and propellers; original `forceRenderingOff` is restored on leave/destruction. Did not report a general cockpit-exit visibility defect.
- Pausing comments/invariants remain in old source/docs, but current controls and fleet clock are real-time. Did not label unscaled presentation time a pause bug without an actual current pause path.
- No livery fit, cockpit style, cloud visual quality, frame pacing or camera clipping appearance claims from screenshots. Those need the Mac; this audit's findings concern calculable geometry, explicit colour mapping and mismatched event/weather inputs.
