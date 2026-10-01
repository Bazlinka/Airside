using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private static void UpdateControlSurfaces(
            ControlSurfacePart[] parts, AircraftPhase phase, float progress, float bankDegrees, float deltaTime,
            bool drawnOnGround = false)
        {
            // Presentation-only: rudder/elevator deflect with attitude (Batch D life).
            // deltaTime is the presentation clock, so surfaces hold still while paused
            // and sweep 4x faster at 4x speed instead of running on their own timeline.
            if (deltaTime <= 0f)
                return;
            var pitch = PhasePitchDegrees(phase, progress);
            var elevator = Mathf.Clamp(-pitch * 1.4f, -22f, 22f);
            var rudder = Mathf.Clamp(-bankDegrees * 0.9f, -18f, 18f);
            var wingFlex = AirsideReusableMotion.WingFlexDegrees(phase, progress);
            for (var i = 0; i < parts.Length; i++)
            {
                var child = parts[i].Transform;
                if (child == null)
                    continue;
                switch (parts[i].Kind)
                {
                    case ControlSurfaceKind.Rudder:
                    {
                        var euler = child.localEulerAngles;
                        var current = euler.y > 180f ? euler.y - 360f : euler.y;
                        euler.y = Mathf.MoveTowards(current, rudder, deltaTime * 90f);
                        child.localEulerAngles = euler;
                        break;
                    }
                    case ControlSurfaceKind.Elevator:
                    {
                        // Soft elevator cue on the whole tailplane when no separate elevator mesh.
                        var euler = child.localEulerAngles;
                        var current = euler.x > 180f ? euler.x - 360f : euler.x;
                        var target = elevator * parts[i].Factor;
                        euler.x = Mathf.MoveTowards(current, target, deltaTime * 80f);
                        child.localEulerAngles = euler;
                        break;
                    }
                    case ControlSurfaceKind.Aileron:
                    {
                        var euler = child.localEulerAngles;
                        var current = euler.x > 180f ? euler.x - 360f : euler.x;
                        var target = Mathf.Clamp(bankDegrees * 0.8f * parts[i].Factor, -18f, 18f);
                        euler.x = Mathf.MoveTowards(current, target, deltaTime * 90f);
                        child.localEulerAngles = euler;
                        break;
                    }
                    case ControlSurfaceKind.Wing:
                    {
                        // Flex the authored wing roots in opposite directions so both tips
                        // rise under load. Keep the cue subtle and ease it between phases.
                        var euler = child.localEulerAngles;
                        var current = euler.z > 180f ? euler.z - 360f : euler.z;
                        euler.z = Mathf.MoveTowards(current, wingFlex * parts[i].Factor, deltaTime * 3.5f);
                        child.localEulerAngles = euler;
                        break;
                    }
                    case ControlSurfaceKind.Flap:
                    {
                        // Takeoff flap is set for the roll and milked off after rotation —
                        // it used to keep extending all the way through the climb.
                        var deploy = AirsideReusableMotion.FlapDegrees(phase, progress, drawnOnGround);
                        var euler = child.localEulerAngles;
                        var current = euler.x > 180f ? euler.x - 360f : euler.x;
                        euler.x = Mathf.MoveTowards(current, deploy, deltaTime * 40f);
                        child.localEulerAngles = euler;
                        break;
                    }
                    case ControlSurfaceKind.Spoiler:
                    {
                        // Spoilers pop on touchdown and stow as the rollout ends, rather
                        // than creeping up from zero through the whole flare.
                        var raise = phase == AircraftPhase.Landing
                            ? 35f * Mathf.Clamp01(Mathf.InverseLerp(
                                  AirsideFlightPath.TouchdownProgress,
                                  AirsideFlightPath.TouchdownProgress + 0.06f, progress)
                                - Mathf.InverseLerp(0.86f, 1f, progress))
                            : 0f;
                        var euler = child.localEulerAngles;
                        var current = euler.x > 180f ? euler.x - 360f : euler.x;
                        euler.x = Mathf.MoveTowards(current, -raise, deltaTime * 55f);
                        child.localEulerAngles = euler;
                        break;
                    }
                }
            }
        }

        private void UpdateTerminalFlag()
        {
            if (_terminalFlag == null)
            {
                _terminalFlag = AirsideSceneIndex.Find("flag_cloth");
                if (_terminalFlag == null)
                    return;
            }

            // Soft flap on the terminal flag cloth (0025 item 7) — presentation only.
            var flap = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.FlagFlapHz * Mathf.PI * 2f) * 12f;
            var ripple = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.FlagRippleHz * Mathf.PI * 2f) * 4f;
            _terminalFlag.localRotation = Quaternion.Euler(flap * 0.15f, 0f, flap + ripple);
        }

        private void CollectHoldShortMarkings(Renderer[] renderers = null)
        {
            _holdShortRenderers.Clear();
            // Prefix scan — A1/A2 fillet bars and kit meshes rename often; exact lists go stale.
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Hold short", StringComparison.Ordinal)
                    || n.StartsWith("hold_short", StringComparison.Ordinal))
                {
                    if (!_holdShortRenderers.Contains(renderer))
                        _holdShortRenderers.Add(renderer);
                }
            }
        }

        /// <summary>
        /// True for paved / painted surfaces that keep a residual damp sheen in clear
        /// weather. Constant for the lifetime of a renderer, so it is resolved once in
        /// <see cref="CollectWetSurfaces"/> rather than re-tested every frame.
        /// </summary>
        private static bool IsPavedSurfaceName(string name)
        {
            return (name is "Hangar taxi link"
                    or "Service lane link W" or "Service lane link E" or "Fuel pad link"
                    or "ARFF apron link"
                    or "Access road stub" or "Access road elbow")
                || name.StartsWith("Service lane", StringComparison.Ordinal)
                || name.StartsWith("Fuel pad", StringComparison.Ordinal)
                || name.StartsWith("Access road", StringComparison.Ordinal)
                || name.StartsWith("Car park aisle", StringComparison.Ordinal)
                || name.StartsWith("Stand 3 apron", StringComparison.Ordinal)
                || name.StartsWith("Car park bay", StringComparison.Ordinal)
                || name.StartsWith("Runway 05", StringComparison.Ordinal) || name.StartsWith("Runway 12", StringComparison.Ordinal) || name.StartsWith("Taxiway ", StringComparison.Ordinal)
                || name.StartsWith("Pavement fillet", StringComparison.Ordinal)
                || name.StartsWith("Runway E", StringComparison.Ordinal)
                || name.StartsWith("Runway mid", StringComparison.Ordinal)
                || name.StartsWith("Runway blast", StringComparison.Ordinal)
                || name.StartsWith("Apron ", StringComparison.Ordinal)
                || name.StartsWith("Apron joint", StringComparison.Ordinal)
                || name.StartsWith("Apron slab", StringComparison.Ordinal)
                || name.StartsWith("Apron fringe", StringComparison.Ordinal)
                || name.StartsWith("Apron corner", StringComparison.Ordinal)
                || name.StartsWith("Runway marking", StringComparison.Ordinal)
                || name.StartsWith("Runway edge", StringComparison.Ordinal)
                || name.StartsWith("Threshold", StringComparison.Ordinal)
                || name.StartsWith("Hold short", StringComparison.Ordinal)
                || name.StartsWith("Taxi edge", StringComparison.Ordinal)
                || name.StartsWith("Taxi lead", StringComparison.Ordinal)
                || name.StartsWith("Taxiway A", StringComparison.Ordinal)
                || name.StartsWith("Hangar apron", StringComparison.Ordinal)
                || name.StartsWith("Stand stop", StringComparison.Ordinal)
                || name.StartsWith("Stand number", StringComparison.Ordinal)
                || name.StartsWith("Access turn", StringComparison.Ordinal)
                || name.StartsWith("Access centreline", StringComparison.Ordinal)
                || name.StartsWith("Access edge", StringComparison.Ordinal)
                || name.StartsWith("Taxi exit centre", StringComparison.Ordinal)
                || name.StartsWith("Drop-off zebra", StringComparison.Ordinal)
                || name.StartsWith("Overflow bay", StringComparison.Ordinal)
                || name.StartsWith("Bay line", StringComparison.Ordinal)
                || name.StartsWith("Stall line", StringComparison.Ordinal)
                || name.StartsWith("Taxi arrow", StringComparison.Ordinal)
                || name.StartsWith("Runway digit", StringComparison.Ordinal)
                || name.StartsWith("Stand lead", StringComparison.Ordinal)
                || name.StartsWith("Apron chevron", StringComparison.Ordinal)
                || name.StartsWith("Hold short", StringComparison.Ordinal)
                || name.StartsWith("Taxi centre", StringComparison.Ordinal)
                || name.StartsWith("Aiming point", StringComparison.Ordinal)
                || name.StartsWith("TDZ ", StringComparison.Ordinal)
                || name.StartsWith("Threshold stripe", StringComparison.Ordinal)
                || name.StartsWith("Jetty ", StringComparison.Ordinal)
                || name.StartsWith("ARFF apron", StringComparison.Ordinal)
                || name.StartsWith("Fuel ", StringComparison.Ordinal)
                || name.StartsWith("Terminal canopy", StringComparison.Ordinal)
                || name.StartsWith("Stand box", StringComparison.Ordinal)
                || name.StartsWith("Access road shoulder", StringComparison.Ordinal)
                || name.StartsWith("Runway shoulder", StringComparison.Ordinal)
                || name.StartsWith("Access turn shoulder", StringComparison.Ordinal)
                || name.StartsWith("Car park kerb", StringComparison.Ordinal)
                || name.StartsWith("runway_centre", StringComparison.Ordinal)
                || name.StartsWith("runway_edge_left", StringComparison.Ordinal)
                || name.StartsWith("runway_edge_right", StringComparison.Ordinal)
                || name.StartsWith("runway_threshold", StringComparison.Ordinal)
                || name.StartsWith("taxi_centreline", StringComparison.Ordinal)
                || name.StartsWith("taxi_edge_", StringComparison.Ordinal)
                || name.StartsWith("taxi_arrow_", StringComparison.Ordinal)
                || name.StartsWith("hold_short_", StringComparison.Ordinal)
                || name.StartsWith("threshold_", StringComparison.Ordinal)
                || name.StartsWith("stand_stop_", StringComparison.Ordinal)
                || name.StartsWith("aiming_", StringComparison.Ordinal)
                || name.StartsWith("tdz_", StringComparison.Ordinal)
                || name.StartsWith("chevron_", StringComparison.Ordinal)
                || name.StartsWith("digit_", StringComparison.Ordinal)
                || name.StartsWith("apron_arrow_", StringComparison.Ordinal);
        }

        /// <summary>
        /// Decision 0025 item 5 — second realtime probe on the terminal landside so
        /// authored glass / canopy posts catch window spill at dusk.
        /// </summary>
        private static ReflectionProbe BuildTerminalReflectionProbe()
        {
            if (!AirsideRuntimeQuality.UseTerminalProbe)
                return null;
            var go = new GameObject("Terminal reflection probe");
            go.transform.position = new Vector3(26f, 3.2f, 27f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 64;
            probe.size = new Vector3(32f, 16f, 22f);
            probe.center = Vector3.zero;
            probe.intensity = 0.95f;
            probe.boxProjection = true;
            probe.shadowDistance = 18f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 60f;
            return probe;
        }

        private static Light[] BuildLandsideStreetlights()
        {
            // Poles + warm point lights along access road and car park edge.
            var positions = new[]
            {
                new Vector3(23.5f, 0f, 34f),
                new Vector3(23.5f, 0f, 40f),
                new Vector3(29f, 0f, 46f),
                new Vector3(40f, 0f, 46f),
                new Vector3(52f, 0f, 46f),
                new Vector3(48f, 0f, 40f)
            };
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var steel = new Color(0.35f, 0.36f, 0.38f);
            var head = new Color(0.25f, 0.26f, 0.28f);
            var lampColor = new Color(1f, 0.92f, 0.7f);
            // Apron flood masts are oversized for drop-off — shrink landside kit stems.
            var landsideMastScale = Vector3.one * 0.62f;
            var count = AirsideRuntimeQuality.LandsideLightCount(positions.Length);
            var lights = new Light[count];
            for (var i = 0; i < count; i++)
            {
                var pos = positions[i];
                var kitMast = ArtGltfLoader.TryPlaceCombined(
                    lightingKit,
                    new[]
                    {
                        ("flood_pole", steel),
                        ("flood_head", head),
                        ("flood_lamp", lampColor)
                    },
                    pos, Quaternion.identity, $"Streetlight {i}", out _,
                    landsideMastScale);
                if (!kitMast)
                {
                    CreateBlock($"Streetlight pole {i}", pos + new Vector3(0f, 2.2f, 0f), new Vector3(0.14f, 4.4f, 0.14f), steel);
                    CreateBlock($"Streetlight head {i}", pos + new Vector3(0.35f, 4.35f, 0f), new Vector3(0.7f, 0.18f, 0.35f), head);
                    CreateBlock($"Streetlight lamp {i}", pos + new Vector3(0.55f, 4.2f, 0f), new Vector3(0.28f, 0.16f, 0.28f), lampColor);
                }

                var go = new GameObject($"Landside streetlight {i + 1}");
                // Kit masts are scaled ~0.62f — keep the point light near the shorter head.
                var lightHeight = kitMast ? 2.55f : 4.1f;
                go.transform.position = pos + new Vector3(0.35f, lightHeight, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.9f, 0.7f);
                light.range = 16f;
                light.intensity = 0.02f;
                lights[i] = light;
            }

            return lights;
        }

        private void BuildAirfield()
        {
            var root = new GameObject("Airfield");
            _airfieldRoot = root.transform;
            AirsideStaticWorld.WorldRoot = _airfieldRoot;
            if (AirsideBareField.Enabled)
            {
                BuildBareAdelaideField();
                return;
            }

            if (!AirsideTerrainGround.TryBuild(_airfieldRoot))
                BuildAirfieldTerrainBase();
            if (AirsideCombinedSurfaces.UseTileOperational)
                BuildAirfieldTerrain11Operational();
            else
                BuildCombinedOperationalSurfaces();
            if (AirsideFocusMode.ShowBuildings)
                BuildAirfieldTerrain12();
            if (AirsideFocusMode.ShowBuildings || AirsideFocusMode.ShowEnvironment
                || AirsideFocusMode.ShowWorldProps)
                BuildAirfieldApronAndBuildings();
        }

        /// <summary>
        /// Authored Adelaide ground mesh, one 3100 × 45 m runway with shoulders, and
        /// real-metre paint from <see cref="AirsideRunwayMarkings"/>. No taxiways,
        /// apron, buildings, signs or props. Falls back to a grass slab if the mesh
        /// builder cannot resolve the CC0 maps.
        /// </summary>
        private void BuildBareAdelaideField()
        {
            _bareGroundFollowsLandform = AirsideAdelaideGroundMesh.TryBuild(_airfieldRoot);
            if (!_bareGroundFollowsLandform)
            {
                var grass = Shade(AirsideTheme.DryGrass, 0.62f);
                CreateBlock(
                    AirsideBareField.GroundObjectName,
                    new Vector3(0f, AirsideBareField.GroundCenterY, 0f),
                    new Vector3(
                        AirsideBareField.GroundLengthMetres,
                        AirsideBareField.GroundHeightMetres,
                        AirsideBareField.GroundWidthMetres),
                    grass,
                    PreferSurfaceBasecolor("tx_grass_kingscote"),
                    new Vector2(
                        AirsideBareField.GroundLengthMetres / 47f,
                        AirsideBareField.GroundWidthMetres / 37f));
            }

            // The coastal plain and Gulf St Vincent past the airfield edge, from the real OSM coast.
            // ADR 0209: bake seasonal dry-grass tint from the Adelaide calendar day once at build.
            var seasonClock = _operations?.Clock ?? AirlineClock.Default;
            var seasonDay = seasonClock.LocalAt(_clock.Now).DayOfYear;
            if (AirsideAdelaideSurroundings.TryBuild(_airfieldRoot, seasonDay, out var surroundingsMaterial)
                && surroundingsMaterial != null)
            {
                // The suburbs around the field (ADR 0159), fading with the land they stand on.
                // Spread across frames so the mesh build does not freeze the first picture (ADR 0162).
                StartCoroutine(AirsideAdelaideSuburbs.BuildGradually(_airfieldRoot,
                    surroundingsMaterial.GetFloat("_HorizonFadeStart"),
                    surroundingsMaterial.GetFloat("_HorizonFadeEnd")));
            }

            BuildBareAdelaidePavement();
            var pavementY = AirsideAdelaideGround.PavementWorldY;
            // Roads ride the same real relief the surroundings now have (ADR 0158).
            // ADR 0184: one road network draws every road, from the runway aprons to the far suburbs. It builds on a
            // worker thread and lands a few milliseconds a frame.
            var roadHeight = AirsideAdelaideSurroundings.RoadHeight(pavementY);
            if (AirsideAdelaideRoadNetworkMesh.CanBuild())
            {
                BuildYpadRoadLampGlows(roadHeight);
                BuildYpadSignalGlows(roadHeight);
                AirsideAdelaideEmergencyAviation.TryBuild(_airfieldRoot, roadHeight);
                StartCoroutine(AirsideAdelaideRoadNetworkMesh.BuildAsync(_airfieldRoot, pavementY, roadHeight));
            }

            BuildCloudBands();
            // Real Adelaide used to omit these entirely because only the legacy compact
            // environment called BuildHorizonDome. Keep the real-scale clear-colour sky,
            // but give it the same astronomical bodies and a camera-centred star shell.
            BuildSunAndMoonDiscs();
            BuildStarField();
            // ADR 0181: the real aerodrome boundary, on by default (the old 3.4 × 2.3 km rectangle it replaces
            // was opt-in because it caged the field; the real fence is a kilometre and more out).
            if (!AirsideBareField.HasLaunchFlag(NoBoundaryFenceFlag))
                BuildAdelaideBoundaryFence();
        }

        /// <summary>
        /// Real YPAD pavement (ADR 0045): 05/23, 12/30 at its real crossing, and every
        /// taxiway and apron from the OpenStreetMap layout.
        /// </summary>
        private static void BuildBareAdelaidePavement()
        {
            var asphalt = new Color(0.16f, 0.18f, 0.2f);
            var taxiAsphalt = new Color(0.18f, 0.19f, 0.21f);
            var shoulderColor = new Color(0.42f, 0.36f, 0.28f);
            var paint = Color.white;
            var dirtAlbedo = AirsideAdelaideGround.LayerBasecolorPath(AirsideAdelaideGround.LayerWornDirt);
            if (ArtRuntimePaths.ResolveExisting(dirtAlbedo) == null)
                dirtAlbedo = PreferSurfaceBasecolor("tx_grass_kingscote");

            BuildBareMainRunway(asphalt, shoulderColor, dirtAlbedo, paint);
            BuildBareCrossRunway(asphalt, shoulderColor, dirtAlbedo, paint);
            BuildYpadTaxiwaysAndAprons(taxiAsphalt, paint);
        }

        private static void BuildBareMainRunway(
            Color asphalt, Color shoulderColor, string dirtAlbedo, Color paint)
        {
            var runway = CreateBlock(
                AirsideAdelaidePavement.MainRunwayName,
                new Vector3(0f, AirsideBareField.RunwayCenterY, 0f),
                new Vector3(
                    AirsideBareField.RunwayLengthMetres,
                    AirsideBareField.RunwayHeightMetres,
                    AirsideBareField.RunwayWidthMetres),
                asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideBareField.RunwayLengthMetres / 42f,
                    AirsideBareField.RunwayWidthMetres / 11f));
            ApplyRunwayMultiScale(runway.transform);
            BuildBareRunwayRubberMarks();

            var shoulderWidth = AirsideAdelaidePavement.ShoulderWidthMetres;
            var shoulderZ = AirsideBareField.RunwayHalfWidth + shoulderWidth * 0.5f;
            CreateBlock(
                "Runway 05/23 shoulder N",
                new Vector3(0f, AirsideBareField.RunwayCenterY - 0.01f, shoulderZ),
                new Vector3(AirsideBareField.RunwayLengthMetres + 40f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideBareField.RunwayLengthMetres + 40f) / 29f, shoulderWidth / 9f));
            CreateBlock(
                "Runway 05/23 shoulder S",
                new Vector3(0f, AirsideBareField.RunwayCenterY - 0.01f, -shoulderZ),
                new Vector3(AirsideBareField.RunwayLengthMetres + 40f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideBareField.RunwayLengthMetres + 40f) / 29f, shoulderWidth / 9f));

            var markings = new GameObject("Runway 05/23 markings").transform;
            if (_airfieldRoot != null)
                markings.SetParent(_airfieldRoot, false);

            // The long thin lines widen with distance so they stay continuous instead of shimmering (no UnityEngine
            // types in the marking maths, so the same marks feed both).
            CreateWidenedStripPaint(markings, "Runway 05/23 edge left",
                new[] { AirsideRunwayMarkings.EdgeLeft }, paint);
            CreateWidenedStripPaint(markings, "Runway 05/23 edge right",
                new[] { AirsideRunwayMarkings.EdgeRight }, paint);
            CreateWidenedStripPaint(markings, "Runway 05/23 centre",
                AirsideRunwayMarkings.CentrelineDashes(), paint);
            CreateCombinedStripPaint(markings, "Runway 05/23 threshold",
                AirsideRunwayMarkings.ThresholdStripes(), paint);
            CreateCombinedStripPaint(markings, "Runway 05/23 numbers",
                AirsideRunwayMarkings.DesignationNumerals(), paint);
            CreateCombinedStripPaint(markings, "Aiming point 05/23",
                AirsideRunwayMarkings.AimingPoints(), paint);
            CreateCombinedStripPaint(markings, "TDZ marks 05/23",
                AirsideRunwayMarkings.TouchdownZones(), paint);
        }

        private static void BuildBareCrossRunway(
            Color asphalt, Color shoulderColor, string dirtAlbedo, Color paint)
        {
            var root = new GameObject(AirsideAdelaidePavement.CrossRunwayName).transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);
            root.position = new Vector3(
                AirsideAdelaidePavement.CrossCenterX,
                AirsideBareField.RunwayCenterY - AirsideAdelaidePavement.CrossRunwayDropMetres,
                AirsideAdelaidePavement.CrossCenterZ);
            root.rotation = Quaternion.Euler(0f, AirsideAdelaidePavement.CrossYawDegrees, 0f);

            var slab = CreateLocalBlock(
                root,
                "Runway 12/30 slab",
                Vector3.zero,
                new Vector3(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideBareField.RunwayHeightMetres,
                    AirsideAdelaidePavement.CrossWidthMetres),
                asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"),
                new Vector2(
                    AirsideAdelaidePavement.CrossLengthMetres / 42f,
                    AirsideAdelaidePavement.CrossWidthMetres / 11f));
            ApplyRunwayMultiScale(slab.transform);

            var shoulderWidth = AirsideAdelaidePavement.ShoulderWidthMetres;
            var shoulderZ = AirsideAdelaidePavement.CrossHalfWidth + shoulderWidth * 0.5f;
            CreateLocalBlock(
                root,
                "Runway 12/30 shoulder L",
                new Vector3(0f, -0.01f, shoulderZ),
                new Vector3(AirsideAdelaidePavement.CrossLengthMetres + 30f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideAdelaidePavement.CrossLengthMetres + 30f) / 29f, shoulderWidth / 9f));
            CreateLocalBlock(
                root,
                "Runway 12/30 shoulder R",
                new Vector3(0f, -0.01f, -shoulderZ),
                new Vector3(AirsideAdelaidePavement.CrossLengthMetres + 30f, 0.1f, shoulderWidth),
                shoulderColor,
                dirtAlbedo,
                new Vector2((AirsideAdelaidePavement.CrossLengthMetres + 30f) / 29f, shoulderWidth / 9f));

            var markings = new GameObject("Runway 12/30 markings").transform;
            markings.SetParent(root, false);
            markings.localPosition = Vector3.zero;
            markings.localRotation = Quaternion.identity;

            // Strip marks are local to the rotated root so paint follows 12/30.
            var y = AirsideRunwayMarkings.PaintLiftMetres
                    + AirsideBareField.RunwayHeightMetres * 0.5f;
            SpawnLocalWidenedStripPaint(markings, "Runway 12/30 edge left",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(new[] { AirsideStripMarkings.EdgeLeft(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideAdelaidePavement.CrossWidthMetres) }), paint, y);
            SpawnLocalWidenedStripPaint(markings, "Runway 12/30 edge right",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(new[] { AirsideStripMarkings.EdgeRight(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideAdelaidePavement.CrossWidthMetres) }), paint, y);
            SpawnLocalWidenedStripPaint(markings, "Runway 12/30 centre",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(
                    AirsideStripMarkings.CentrelineDashes(AirsideAdelaidePavement.CrossLengthMetres)),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 threshold",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(
                    AirsideStripMarkings.ThresholdStripes(AirsideAdelaidePavement.CrossLengthMetres)),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 numbers",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(
                    AirsideStripMarkings.DesignationNumerals(
                        AirsideAdelaidePavement.CrossLengthMetres, "12", "30")),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 aiming",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(AirsideStripMarkings.AimingPoints(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideStripMarkings.ShortStripAimingFromThreshold)),
                paint, y);
            SpawnLocalStripPaint(markings, "Runway 12/30 tdz",
                AirsideAdelaidePavement.ClipCrossRunwayPaintToMain(AirsideStripMarkings.TouchdownZones(
                    AirsideAdelaidePavement.CrossLengthMetres,
                    AirsideStripMarkings.ShortStripTouchdownDistances)),
                paint, y);
        }

        /// <summary>
        /// Dark rubber-deposit bands in the touchdown zone — presentation only,
        /// matching the darkened asphalt pilots see on a busy jet runway.
        /// </summary>
        /// <summary>
        /// Tyre rubber in both touchdown zones as seeded streaks on the gear tracks
        /// (<see cref="RunwayRubberMarks"/>), two shades in two meshes, laid between the
        /// runway top and its paint.
        /// </summary>
        private static void BuildBareRunwayRubberMarks()
        {
            var root = new GameObject("Runway 05/23 rubber").transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);

            var runwayTop = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.5f;
            var heavyY = runwayTop + 0.022f;
            var lightY = runwayTop + 0.016f;
            var heavy = new SurfaceMesh();
            var light = new SurfaceMesh();
            var corners = new float[8];
            foreach (var streak in RunwayRubberMarks.All())
            {
                var hx = streak.Length * 0.5f;
                var hz = streak.Width * 0.5f;
                corners[0] = streak.CentreX - hx; corners[1] = streak.CentreZ - hz;
                corners[2] = streak.CentreX + hx; corners[3] = streak.CentreZ - hz;
                corners[4] = streak.CentreX + hx; corners[5] = streak.CentreZ + hz;
                corners[6] = streak.CentreX - hx; corners[7] = streak.CentreZ + hz;
                AddPolygon(streak.Heavy ? heavy : light, corners, streak.Heavy ? heavyY : lightY);
            }

            var asphalt = PreferSurfaceBasecolor("tx_asphalt_runway");
            SpawnSurface(root, "Rubber (old)", light, new Color(0.17f, 0.17f, 0.18f), asphalt, castShadows: false);
            SpawnSurface(root, "Rubber (fresh)", heavy, new Color(0.09f, 0.09f, 0.10f), asphalt, castShadows: false);
        }

        /// <summary>
        /// Second-scale asphalt detail on URP Lit so long-runway tiling does not read
        /// as stretched pixels from follow, without changing the 45 m footprint.
        /// </summary>
        private static void ApplyRunwayMultiScale(Transform runway)
        {
            if (runway == null)
                return;
            var renderer = runway.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;
            var material = renderer.material;
            if (material.mainTexture == null || !material.HasProperty("_DetailAlbedoMap"))
                return;
            // Second UV scale breaks the long-runway landmark repeat without new assets.
            material.SetTexture("_DetailAlbedoMap", material.mainTexture);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(3.7f, 1.9f));
            material.EnableKeyword("_DETAIL_MULX2");
            if (material.HasProperty("_DetailAlbedoMapScale"))
                material.SetFloat("_DetailAlbedoMapScale", 0.35f);
            var normalPath = PreferSurfaceMap("tx_asphalt_runway", "normal");
            if (normalPath != null && material.HasProperty("_DetailNormalMap"))
            {
                var normal = AirsideArtTextures.Load(normalPath, linear: true);
                if (normal != null)
                {
                    material.SetTexture("_DetailNormalMap", normal);
                    material.SetTextureScale("_DetailNormalMap", new Vector2(3.7f, 1.9f));
                }
            }
        }

        private static void CreateWidenedStripPaint(
            Transform parent, string name, AirsideRunwayMarkings.RunwayMark[] marks, Color color)
        {
            if (marks == null || marks.Length == 0)
                return;
            var list = new List<(float, float, float, float)>(marks.Length);
            foreach (var m in marks)
                list.Add((m.CenterX, m.CenterZ, m.LengthX, m.WidthZ));
            // Top face of the old paint cube, so the flat ribbon sits where the paint always did.
            var top = AirsideRunwayMarkings.PaintCenterY + AirsideRunwayMarkings.PaintHeight * 0.5f;
            DistanceWidenedPaint.Create(parent, name, list, top, CreateSharedSurfaceMaterial(color));
        }

        private static void SpawnLocalWidenedStripPaint(
            Transform parent, string name, AirsideStripMarkings.Mark[] marks, Color color, float localY)
        {
            if (marks == null || marks.Length == 0)
                return;
            var list = new List<(float, float, float, float)>(marks.Length);
            foreach (var m in marks)
                list.Add((m.CenterX, m.CenterZ, m.LengthX, m.WidthZ));
            DistanceWidenedPaint.Create(parent, name, list, localY + AirsideRunwayMarkings.PaintHeight * 0.5f,
                CreateSharedSurfaceMaterial(color));
        }

        /// <summary>
        /// One combined paint mesh per marking family so a 3 100 m strip is not
        /// hundreds of unbatched cubes. Names keep the wet-surface collectors working.
        /// </summary>
        private static void CreateCombinedRunwayPaint(
            Transform parent, string name, AirsideRunwayMarkings.RunwayMark[] marks, Color color)
        {
            if (marks == null || marks.Length == 0)
                return;

            var y = AirsideRunwayMarkings.PaintCenterY;
            var h = AirsideRunwayMarkings.PaintHeight;
            var locals = new Matrix4x4[marks.Length];
            for (var i = 0; i < marks.Length; i++)
            {
                var mark = marks[i];
                locals[i] = Matrix4x4.TRS(
                    new Vector3(mark.CenterX, y, mark.CenterZ),
                    Quaternion.identity,
                    new Vector3(mark.LengthX, h, mark.WidthZ));
            }

            var mesh = AirsideMeshUtil.CombineTransformed(BuiltinCube(), locals);
            if (mesh == null)
            {
                foreach (var mark in marks)
                {
                    ParentBlock(
                        parent,
                        name,
                        new Vector3(mark.CenterX, y, mark.CenterZ),
                        new Vector3(mark.LengthX, h, mark.WidthZ),
                        color);
                }

                return;
            }

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSharedSurfaceMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.SetParent(parent, false);
            AirsideSceneIndex.Remember(go);
        }

        /// <summary>
        /// P0 — one slab per operational surface instead of the 745-tile Terrain11 dump.
        /// Names keep the wet-surface / hold-short collectors working.
        /// </summary>
        private static void BuildCombinedOperationalSurfaces()
        {
            var grass = Shade(AirsideTheme.Eucalyptus, 0.62f);
            var asphalt = new Color(0.16f, 0.18f, 0.2f);
            var concrete = new Color(0.34f, 0.36f, 0.37f);
            var taxiAsphalt = new Color(0.22f, 0.24f, 0.26f);
            var runwayLen = AirportLayout.RunwayLength;
            var runwayHalf = AirportLayout.RunwayHalfLength;
            var taxiSpan = AirportLayout.TaxiwayEastX - AirportLayout.AlphaJunctionX + 16f;
            var taxiCentreX = (AirportLayout.TaxiwayEastX + AirportLayout.AlphaJunctionX) * 0.5f;

            CreateBlock("Infield grass", new Vector3(4f, -0.55f, 4.6f), new Vector3(runwayHalf + 8f, 0.28f, 3.4f), grass,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(12f, 2.2f));
            CreateBlock("Runway W", new Vector3(0f, -0.08f, 0f), new Vector3(runwayLen, 0.144f, AirportLayout.RunwayWidth), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(24f, 2.4f));
            CreateBlock("Runway blast W", new Vector3(-runwayHalf - 4f, -0.08f, 0f), new Vector3(8f, 0.14f, 6.4f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 2.2f));
            CreateBlock("Runway blast E", new Vector3(runwayHalf + 4f, -0.08f, 0f), new Vector3(8f, 0.14f, 6.4f), asphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(2.2f, 2.2f));
            CreateBlock("Taxiway A", new Vector3(taxiCentreX, -0.02f, AirportLayout.TaxiwayAlphaZ),
                new Vector3(taxiSpan, 0.12f, 4.2f), taxiAsphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(16f, 1.6f));
            CreateBlock("Taxiway B", new Vector3(taxiCentreX, -0.02f, AirportLayout.TaxiwayBravoZ),
                new Vector3(taxiSpan, 0.12f, 4.2f), taxiAsphalt,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(16f, 1.6f));
            CreateTaxiChordPad("Taxiway B exit", new Vector3(AirportLayout.ArrivalExitX, -0.02f, 0f),
                new Vector3(AirportLayout.ArrivalExitX, -0.02f, AirportLayout.TaxiwayBravoZ), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateTaxiChordPad("Taxiway A entry", new Vector3(AirportLayout.DepartureEntryX, -0.02f, 0f),
                new Vector3(AirportLayout.AlphaJunctionX, -0.02f, AirportLayout.TaxiwayAlphaZ), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.2f, 1.4f));
            CreateBlock("Apron ", new Vector3(20f, 0f, 18f), new Vector3(28f, 0.12f, 16f), concrete,
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(6f, 4f));
        }

        // Fallback ground, used only when the baked Kingscote TerrainData is absent.
        //
        // The former fine-grained outer-ground tile field created tens of thousands of
        // primitives during Awake, preventing the player from reaching its first frame.
        // One textured base keeps the operational airfield visible; the retained inner
        // runway, taxi, apron, buildings, props and context provide the visual detail.
        private static void BuildAirfieldTerrainBase()
        {
            CreateBlock("Airfield terrain base",
                new Vector3(AirsideTerrainField.CentreX, -0.76f, AirsideTerrainField.CentreZ),
                new Vector3(AirsideTerrainField.SizeX + 24f, 0.8f, AirsideTerrainField.SizeZ + 24f),
                Shade(AirsideTheme.DryGrass, 0.62f), PreferSurfaceBasecolor("tx_grass_kingscote"),
                new Vector2(AirsideTerrainField.SizeX * 0.11f, AirsideTerrainField.SizeZ * 0.12f));
        }

        private static void BuildAirfieldTerrain11Operational()
        {
            // Tile dump retired — combined pads plus the WLD-004 kit cover this ground.
            BuildCombinedOperationalSurfaces();
        }

        private static void BuildAirfieldTerrain12()
        {
            // Warm interior spill at dusk/night — only when the terminal kit did not
            // already ship interior glow meshes (avoid stacking cubes on authored glass).
            if (FindBuilt("interior_glow_l") == null
                && FindBuilt("interior_glow_r") == null
                && FindBuilt("interior_glow_mid") == null)
            {
                CreateBlock("Terminal window glow L", new Vector3(20f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
                CreateBlock("Terminal window glow R", new Vector3(32f, 2.35f, 24.5f), new Vector3(5.5f, 1.6f, 0.08f), new Color(1f, 0.82f, 0.45f));
            }

            if (FindBuilt("landside_glass") == null && FindBuilt("interior_glow_desk") == null)
                CreateBlock("Terminal landside glow", new Vector3(26f, 2.2f, 29.4f), new Vector3(10f, 1.4f, 0.08f), new Color(1f, 0.8f, 0.42f));
            BuildTerminalLandsideCanopy();
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_hangar_small_v05.gltf",
                    "Models/Buildings/mdl_hangar_small_authored_v01.gltf",
                    "Models/Buildings/mdl_hangar_small_v04.gltf",
                    "Models/Buildings/mdl_hangar_small_v03.gltf",
                    "Models/Buildings/mdl_hangar_small_v02.gltf",
                    "Models/Buildings/mdl_hangar_small_v01.gltf"),
                new Vector3(-20f, 0f, 20f),
                name =>
                {
                    if (name.StartsWith("glass_pane", StringComparison.Ordinal)
                        || name is "side_window" or "side_window_b" or "office_window"
                        or "skylight_l" or "skylight_r" or "skylight_mid")
                        return new Color(0.18f, 0.36f, 0.48f, 0.42f);
                    if (name.StartsWith("office_mullion", StringComparison.Ordinal)
                        || name.StartsWith("skylight_frame", StringComparison.Ordinal)
                        || name is "side_mullion_l" or "side_mullion_r"
                        or "side_sill_l" or "side_sill_r" or "office_sill" or "office_header")
                        return new Color(0.4f, 0.44f, 0.48f);
                    return name switch
                    {
                        "door_opening" or "door_panel_l" or "door_panel_r" or "door_rib_l" or "door_rib_r"
                            or "door_bar_l1" or "door_bar_l2" or "door_bar_l3" or "door_bar_l4"
                            or "door_bar_r1" or "door_bar_r2" or "door_bar_r3" or "door_bar_r4"
                            or "door_handle_l" or "door_handle_r"
                            or "door_warning_l" or "door_warning_r"
                            or "personnel_door" or "personnel_frame" or "rear_door" => new Color(0.22f, 0.24f, 0.26f),
                        "roof_ridge" or "roof_ridge_cap" or "roof_vent_ridge"
                            or "roof_panel_l" or "roof_panel_r" or "crane_beam" or "crane_trolley"
                            or "crane_hook" or "gutter_front" or "gutter_back" or "gutter_end_l" or "gutter_end_r"
                            or "roof_rib_1" or "roof_rib_2" or "roof_rib_3" or "roof_rib_4"
                            or "roof_rib_5" or "roof_rib_6" or "roof_rib_7"
                            or "flood_can_l" or "flood_can_r" or "downpipe_l" or "downpipe_r"
                            or "sign_board" or "sign_glyph" or "rear_vent"
                            or "fascia_front" or "fascia_back" or "office_roof" or "office_fascia"
                            or "office_downpipe" or "crane_rail_l" or "crane_rail_r"
                            or "gable_front_l" or "gable_front_r" or "gable_back_l" or "gable_back_r"
                            or "gable_apex_front" or "gable_apex_back" or "door_header" or "door_threshold"
                            => new Color(0.4f, 0.44f, 0.48f),
                        "buttress_l" or "buttress_r" or "door_track_l" or "door_track_r" or "door_track_mid"
                            or "door_track_brace_l" or "door_track_brace_r"
                            or "plinth" or "side_louvre_l" or "side_louvre_r"
                            or "workbench" or "tool_cabinet" or "floor_drain" or "floor_mark_bay"
                            or "side_vent" or "side_vent_b" or "office_lean" or "office_door"
                            or "column_ml" or "column_mr"
                            or "cladding_face_l" or "cladding_face_r"
                            or "cladding_face_front" or "cladding_face_back"
                            or "girth_band_1" or "girth_band_2" or "girth_band_3"
                            or "corner_trim_fl" or "corner_trim_fr"
                            or "corner_trim_bl" or "corner_trim_br"
                            or "office_step" or "office_awning" or "roof_flash_front" or "roof_flash_back"
                            or "flood_mount_l" or "flood_mount_r" or "girth_band_4" or "service_door_step"
                            => new Color(0.42f, 0.46f, 0.5f),
                        "door_peek_l" or "door_peek_r" => new Color(0.18f, 0.36f, 0.48f, 0.42f),
                        _ => name.StartsWith("wall_rib_", StringComparison.Ordinal)
                            ? new Color(0.42f, 0.46f, 0.5f)
                            : new Color(0.45f, 0.5f, 0.54f)
                    };
                },
                () =>
                {
                    CreateBlock("Hangar", new Vector3(-20f, 2.5f, 20f), new Vector3(14f, 5f, 9f), new Color(0.45f, 0.5f, 0.54f),
                        "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(2.5f, 1.5f));
                    CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
                },
                "Textures/Environment/tx_terminal_glass_mask_v01.png",
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(2.5f, 1.5f),
                surfaceMeshNames: new[] { "hangar_shell", "roof", "buttress", "door_track", "side_vent", "cladding", "wall_rib", "girth", "gable" });
            // Sliding door slab only when the hangar kit did not ship panel doors.
            if (FindBuilt("Hangar door") == null
                && FindBuilt("door_panel_l") == null
                && FindBuilt("door_panel_r") == null)
                CreateBlock("Hangar door", new Vector3(-20f, 2.0f, 24.6f), new Vector3(8f, 4f, 0.2f), new Color(0.22f, 0.24f, 0.26f));
            // Prefer hangar-kit bay props (workbench / tool cabinet) over greybox densify.
            if (FindBuilt("workbench") == null && FindBuilt("tool_cabinet") == null)
                BuildHangarBayInterior();
            if (FindBuilt("side_window") == null
                && FindBuilt("side_window_b") == null
                && FindBuilt("office_window") == null
                && FindBuilt("glass_pane") == null
                && FindBuilt("glass_pane_l") == null
                && FindBuilt("glass_pane_r") == null)
                CreateBlock("Hangar window glow", new Vector3(-20f, 3.2f, 24.55f), new Vector3(4.5f, 1.8f, 0.08f), new Color(1f, 0.75f, 0.35f));
            PlaceBuildingOrFallback(
                PreferArtKit(
                    "Models/Buildings/mdl_operations_shed_v05.gltf",
                    "Models/Buildings/mdl_operations_shed_authored_v01.gltf",
                    "Models/Buildings/mdl_operations_shed_v04.gltf",
                    "Models/Buildings/mdl_operations_shed_v03.gltf",
                    "Models/Buildings/mdl_operations_shed_v02.gltf",
                    "Models/Buildings/mdl_operations_shed_v01.gltf"),
                new Vector3(-8f, 0f, 26f),
                name =>
                {
                    if (name.StartsWith("glass_pane", StringComparison.Ordinal)
                        || name is "window_l" or "window_r" or "window_side" or "window_side_b")
                        return new Color(0.2f, 0.4f, 0.5f, 0.42f);
                    if (name.StartsWith("window_mullion", StringComparison.Ordinal)
                        || name.StartsWith("wall_rib", StringComparison.Ordinal)
                        || name is "window_transom_l" or "window_transom_r"
                        or "window_header_l" or "window_header_r"
                        or "window_sill_l" or "window_sill_r"
                        or "girth_band_1" or "girth_band_2" or "girth_band_3"
                        or "cladding_face_l" or "cladding_face_r"
                        or "cladding_face_front" or "cladding_face_back")
                        return new Color(0.48f, 0.5f, 0.46f);
                    return name switch
                    {
                        "door" or "door_frame" or "door_knob" or "door_kick" => new Color(0.35f, 0.38f, 0.34f),
                        "interior_glow" => new Color(1f, 0.82f, 0.5f),
                        "interior_desk" => new Color(0.42f, 0.4f, 0.36f),
                        "signage" => AirsideTheme.SafetyYellow,
                        "signage_glyph" => new Color(0.12f, 0.18f, 0.28f),
                        "porch_roof" or "porch_beam" or "porch_light" or "porch_fascia" or "porch_soffit"
                            or "porch_riser" or "porch_post_l" or "porch_post_r"
                            or "porch_post_mid_l" or "porch_post_mid_r"
                            or "roof_ridge" or "roof_ridge_cap" or "roof_panel" or "roof_panel_l" or "roof_panel_r"
                            or "roof_gutter" or "roof_fascia" or "roof_downpipe_l" or "roof_downpipe_r"
                            or "roof_vent_a" or "roof_vent_b" or "roof_eave_back"
                            or "roof_flash_front" or "roof_flash_back"
                            or "gable_front_l" or "gable_front_r" or "gable_back_l" or "gable_back_r"
                            or "gable_apex_front" or "gable_apex_back"
                            or "antenna_mast" or "antenna_dish" or "antenna_boom" or "antenna_guy" or "antenna_guy_b"
                            or "radio_antenna_whip"
                            or "ac_unit" or "ac_unit_b" or "ac_grille" or "ac_pipe" or "ac_pipe_b" or "radio_rack"
                            or "vent_pipe" or "wall_vent" or "flood_can" or "flood_can_b"
                            or "flood_mount" or "flood_mount_b"
                            or "step_rail_l" or "step_rail_r"
                            or "side_louvre" or "side_louvre_b" or "mailbox" or "bench" or "plinth"
                            or "shed_corner_l" or "shed_corner_r"
                            or "window_ledge_l" or "window_ledge_r"
                            or "window_awning_l" or "window_awning_r"
                            or "power_box" or "hose_reel"
                            => new Color(0.48f, 0.5f, 0.46f),
                        _ => new Color(0.55f, 0.58f, 0.52f)
                    };
                },
                () => CreateBlock("Ops shed", new Vector3(-8f, 1.4f, 26f), new Vector3(6f, 2.8f, 4f), new Color(0.55f, 0.58f, 0.52f),
                    "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png", new Vector2(1.5f, 1.2f)),
                "Textures/Environment/tx_terminal_glass_mask_v01.png",
                surfaceTextureRelativePath: "Textures/Surfaces/tx_corrugated_metal_basecolor_v01.png",
                surfaceTextureTiling: new Vector2(1.5f, 1.2f),
                surfaceMeshNames: new[] { "shed_body", "porch", "roof", "cladding", "wall_rib", "girth" });
            if (FindBuilt("interior_glow") == null
                && FindBuilt("window_l") == null
                && FindBuilt("window_r") == null)
                CreateBlock("Ops shed window glow", new Vector3(-8f, 1.5f, 24.1f), new Vector3(3.2f, 1.1f, 0.08f), new Color(1f, 0.78f, 0.4f));

            // Soft wear accent only — large stain sheets were opaque black patches (PNG alpha ignored).
            CreateDecalQuad("Runway wear W", new Vector3(-52f, 0.02f, 0f), new Vector3(18f, 1f, 1.2f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Runway wear mid", new Vector3(0f, 0.02f, 0f), new Vector3(18f, 1f, 1.15f),
                "Textures/Decals/dc_runway_wear_v01.png");
            CreateDecalQuad("Runway wear E", new Vector3(52f, 0.02f, 0f), new Vector3(18f, 1f, 1.2f),
                "Textures/Decals/dc_runway_wear_v01.png");
            // Soft hangar apron so the hangar does not sit on raw grass (regional strip cue).
            CreateBlock("Hangar apron", new Vector3(-20.5f, -0.01f, 16.4f), new Vector3(16f, 0.09f, 8.4f), new Color(0.34f, 0.36f, 0.37f),
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(3.2f, 2.2f));
            CreateBlock("Hangar apron wing", new Vector3(-26.6f, -0.01f, 17.5f), new Vector3(8.2f, 0.08f, 5.2f), new Color(0.33f, 0.35f, 0.36f),
                PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(2.2f, 1.4f));
            CreateTaxiChordPad("Hangar taxi link", new Vector3(-12f, -0.02f, 9f), new Vector3(-18f, -0.02f, 14f), 4.2f,
                PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.4f, 1.1f));
            // Hangar apron lead paint — yellow lead-in toward the door.
            CreateBlock("Hangar apron centre", new Vector3(-20f, 0.04f, 17.2f), new Vector3(0.12f, 0.02f, 4.2f), new Color(0.95f, 0.85f, 0.2f));
            CreateBlock("Hangar apron edge N", new Vector3(-20f, 0.04f, 20.2f), new Vector3(8.5f, 0.02f, 0.1f), Color.white);
            CreateBlock("Hangar apron edge S", new Vector3(-20f, 0.04f, 13.2f), new Vector3(8.5f, 0.02f, 0.1f), Color.white);
            CreateBlock("Hangar apron stop", new Vector3(-20f, 0.04f, 19.4f), new Vector3(3.2f, 0.02f, 0.12f), new Color(0.95f, 0.85f, 0.2f));

            // Soft fringe so the apron doesn't float as a hard cutout (REF densify).
            // N/S only — E/W fringe cubes read as blocks beside taxi/stand lead-ins.
        }

        /// <summary>
        /// Soft grass/sand mounds around the airfield so the ground plane reads as
        /// terrain rather than a flat slab (0025 item 3). Presentation only.
        /// </summary>
        private static void BuildTerrainMicroRelief()
        {
            var terrainKit = PreferArtKit(
                "Models/Environment/mdl_kingscote_context_terrain_v02.gltf",
                "Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            if (!string.IsNullOrEmpty(terrainKit) && ArtGltfLoader.HasKit(terrainKit))
                return;

            var grass = Shade(AirsideTheme.Eucalyptus, 0.62f);
            var dry = Shade(AirsideTheme.DryGrass, 0.9f);
            var sand = Shade(AirsideTheme.Sand, 0.95f);
            CreateBlock("Relief berm N", new Vector3(-8f, 0.28f, 38f), new Vector3(36f, 0.7f, 6f), grass,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 2f));
            CreateBlock("Relief berm S", new Vector3(6f, 0.22f, -24f), new Vector3(40f, 0.55f, 8f), dry,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 2.2f));
            CreateBlock("Dune mound west", new Vector3(-95f, 0.35f, -175f), new Vector3(28f, 0.7f, 18f), sand,
                PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(4f, 2f));
            CreateBlock("Dune mound east", new Vector3(155f, 0.28f, -168f), new Vector3(22f, 0.55f, 14f), sand,
                PreferSurfaceBasecolor("tx_sand_coast"), new Vector2(3.5f, 1.8f));
        }

        /// <summary>
        /// Parked cars, kerbside drop-off and a few landside props so the terminal
        /// approach reads as a working regional airfield (presentation only).
        /// </summary>
        private static void BuildLandsideLife()
        {
            var carColors = new[]
            {
                new Color(0.75f, 0.22f, 0.18f),
                new Color(0.92f, 0.92f, 0.9f),
                new Color(0.15f, 0.18f, 0.22f),
                new Color(0.2f, 0.35f, 0.55f),
                new Color(0.85f, 0.7f, 0.25f),
                new Color(0.35f, 0.4f, 0.38f),
                new Color(0.55f, 0.55f, 0.58f),
                new Color(0.12f, 0.45f, 0.35f)
            };

            // Car park bays — place into the same row/column layout as Bay line paint.
            var hasCarPrefab = ArtPresentationLoader.HasPresentation("Models/Vehicles/mdl_parked_car_v02.gltf")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v02")
                               || ArtPresentationLoader.HasPrefab("mdl_parked_car_v01");
            var hasForecourtKerbsEarly = FindBuilt("Car park kerb N") != null;
            var bayCarCount = hasCarPrefab ? (hasForecourtKerbsEarly ? 6 : 4) : 8;
            for (var i = 0; i < bayCarCount; i++)
            {
                if (hasCarPrefab)
                {
                    if (hasForecourtKerbsEarly)
                    {
                        var cols = 3;
                        var row = i / cols;
                        var col = i % cols;
                        var x = 42.5f + col * 5.0f;
                        var z = 43f + row * 4.0f;
                        PlaceParkedCar($"Parked car {i}", new Vector3(x, 0f, z), 180f, carColors[i % carColors.Length]);
                    }
                    else
                    {
                        PlaceParkedCar($"Parked car {i}", new Vector3(42f + i * 3.6f, 0f, 43.2f), 180f, carColors[i % carColors.Length]);
                    }
                }
                else
                {
                    var row = i < 4 ? 0 : 1;
                    var slot = i % 4;
                    PlaceParkedCar($"Parked car {i}", new Vector3(42f + slot * 3.6f, 0f, 43.2f + row * 4.2f), 180f, carColors[i % carColors.Length]);
                }
            }

            // Kerbside drop-off on the access road.
            PlaceParkedCar("Drop-off car", new Vector3(23.5f, 0f, 36f), 0f, carColors[2]);
            PlaceParkedCar("Taxi wait", new Vector3(28.5f, 0f, 36.5f), 8f, new Color(0.92f, 0.78f, 0.15f));

            // Landside furniture: skip near-terminal bench/trolley when PRP-003 forecourt already placed them.
            var forecourtPlaced = FindBuilt("Terminal bench") != null
                                  || FindBuilt("Trolley rail") != null;
            if (!forecourtPlaced)
            {
                PlaceLuggageTrolley("Luggage trolley A", new Vector3(24f, 0f, 31.5f), -15f);
                PlaceLuggageTrolley("Luggage trolley B", new Vector3(25.2f, 0f, 31.5f), 8f);
                if (!hasCarPrefab)
                    PlaceLuggageTrolley("Luggage trolley C", new Vector3(24.6f, 0f, 30.6f), 175f);
                PlaceLandsideBench("Landside bench", new Vector3(29.5f, 0f, 31.2f), 0f);
            }

            PlaceLandsideBench("Car park bench", new Vector3(40.5f, 0f, 40.2f), 90f);

            // Skip PlaceTree positions that overlap the BuildVegetation belt (already placed).
            // Remaining landside trees: only drop-off fringe not covered by veg belt.
            PlaceTree(new Vector3(20f, 0f, 44f), 0.85f);

            // Overflow bay row — one hero ute + visitor when prefab cars are present.
            if (hasCarPrefab)
            {
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 180f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
            }
            else
            {
                PlaceParkedCar("Overflow car A", new Vector3(42f, 0f, 51.2f), 180f, new Color(0.45f, 0.2f, 0.18f));
                PlaceParkedCar("Overflow car B", new Vector3(45.6f, 0f, 51.2f), 180f, new Color(0.7f, 0.72f, 0.75f));
                PlaceParkedCar("Staff ute", new Vector3(49.2f, 0f, 51.2f), 180f, new Color(0.55f, 0.55f, 0.22f));
                PlaceParkedCar("Overflow car C", new Vector3(52.8f, 0f, 51.2f), 180f, new Color(0.25f, 0.3f, 0.45f));
                PlaceParkedCar("Visitor car", new Vector3(38.5f, 0f, 47.5f), 0f, new Color(0.6f, 0.15f, 0.2f));
                if (!forecourtPlaced)
                    PlaceLuggageTrolley("Luggage trolley D", new Vector3(23.4f, 0f, 30.2f), 40f);
            }

            PlaceLandsideBench("Access bench", new Vector3(22f, 0f, 40.5f), 90f);

            // Painted parking bay chevrons — thin when PRP-003 kerbs already frame the park.
            var hasForecourtKerbs = FindBuilt("Car park kerb N") != null;
            var bayPaint = new Color(0.92f, 0.92f, 0.88f);
            if (hasForecourtKerbs)
            {
                CreateBlock("Bay line W", new Vector3(42.0f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay line E", new Vector3(50.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay stop", new Vector3(46.25f, 0.06f, 41.6f), new Vector3(8.5f, 0.02f, 0.08f), bayPaint);
            }
            else
            {
                CreateBlock("Bay line W", new Vector3(40.5f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay line E", new Vector3(54.3f, 0.06f, 43.2f), new Vector3(0.08f, 0.02f, 3.4f), bayPaint);
                CreateBlock("Bay stop", new Vector3(47.4f, 0.06f, 41.6f), new Vector3(14.4f, 0.02f, 0.08f), bayPaint);
                CreateBlock("Overflow bay W", new Vector3(40.5f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f), bayPaint);
                CreateBlock("Overflow bay E", new Vector3(54.3f, 0.06f, 51.2f), new Vector3(0.08f, 0.02f, 3.0f), bayPaint);
                CreateBlock("Overflow stop", new Vector3(47.4f, 0.06f, 52.6f), new Vector3(14.4f, 0.02f, 0.08f), bayPaint);
            }

            CreateBlock("Access dash", new Vector3(26f, 0.06f, hasForecourtKerbs ? 43.3f : 44.3f),
                new Vector3(0.35f, 0.02f, hasForecourtKerbs ? 12.8f : 14.4f), new Color(0.95f, 0.9f, 0.35f));

            // Small general-aviation tie-down markers west of hangar (life, not sim).
            var tieCount = 3;
            for (var i = 0; i < tieCount; i++)
            {
                CreateBlock($"Tie-down {i}", new Vector3(-30f - i * 5.5f, 0.08f, 14f), new Vector3(0.35f, 0.08f, 0.35f),
                    new Color(0.55f, 0.55f, 0.5f));
                CreateBlock($"Tie-down rope {i}", new Vector3(-30f - i * 5.5f, 0.04f, 14.55f),
                    new Vector3(0.06f, 0.04f, 0.9f), new Color(0.35f, 0.35f, 0.32f));
            }
        }

        /// <summary>
        /// Decision 0025 / Batch F3 — prefer PRP-003 bench parts, then Resources, then greybox.
        /// </summary>
        private static void PlaceLandsideBench(string name, Vector3 position, float yawDegrees)
        {
            Transform root = null;
            var kit = PreferArtKit(
                "Models/Props/mdl_terminal_forecourt_kit_v02.gltf",
                "Models/Props/mdl_terminal_forecourt_kit_v01.gltf");
            if (!string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit))
            {
                var wood = new Color(0.4f, 0.32f, 0.22f);
                var steel = new Color(0.45f, 0.46f, 0.48f);
                var rot = Quaternion.Euler(0f, yawDegrees, 0f);
                if (ArtGltfLoader.TryPlaceCombined(
                        kit,
                        new[]
                        {
                            ("bench_seat", wood),
                            ("bench_back", Shade(wood, 0.9f)),
                            ("bench_leg_l", steel),
                            ("bench_leg_r", steel)
                        },
                        position, rot, name, out var combined))
                {
                    combined.position = position;
                    combined.rotation = rot;
                    return;
                }

                root = new GameObject(name).transform;
                var placed = 0;
                void Place(string mesh, Color color)
                {
                    if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                        return;
                    part.SetParent(root, false);
                    part.localPosition = Vector3.zero;
                    part.localRotation = Quaternion.identity;
                    placed++;
                }

                Place("bench_seat", wood);
                Place("bench_back", Shade(wood, 0.9f));
                Place("bench_leg_l", steel);
                Place("bench_leg_r", steel);
                if (placed == 0)
                {
                    DestroyPresentationObject(root.gameObject);
                    root = null;
                }
            }

            if (root == null && ArtPresentationLoader.TryInstantiatePrefab("mdl_landside_bench_v01", out var prefabRoot))
            {
                prefabRoot.name = name;
                root = prefabRoot;
            }

            if (root == null)
            {
                root = new GameObject(name).transform;
                var wood = new Color(0.45f, 0.32f, 0.18f);
                ParentBlock(root, $"{name} seat", new Vector3(0f, 0.35f, 0f), new Vector3(2.2f, 0.12f, 0.55f), wood);
                ParentBlock(root, $"{name} back", new Vector3(0f, 0.7f, -0.22f), new Vector3(2.2f, 0.55f, 0.1f), wood);
            }

            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        /// <summary>
        /// Batch F3 PRP-002 — place modular fence bay / corner / vehicle gate panels.
        /// Returns false when the kit is missing so the CreateBlock ribbon remains the fallback.
        /// </summary>
        private static bool TryBuildPerimeterFenceFromKit()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_fence_gate_kit_v02.gltf",
                "Models/Props/mdl_airfield_fence_gate_kit_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var post = new Color(0.55f, 0.56f, 0.58f);
            var panel = new Color(0.62f, 0.64f, 0.66f);
            var yellow = Shade(AirsideTheme.SafetyYellow, 0.75f);
            var placed = 0;

            void PlacePart(string mesh, Vector3 pos, Quaternion rot, Color color, string name)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, rot, color, out var part))
                    return;
                part.name = name;
                placed++;
            }

            void PlaceBay(Vector3 pos, float yawDeg, string tag)
            {
                var rot = Quaternion.Euler(0f, yawDeg, 0f);
                var parts = AirsideRuntimeQuality.PlaceFenceRails
                    ? new[]
                    {
                        ("fence_bay", panel),
                        ("fence_bay_post_l", post),
                        ("fence_bay_post_r", post),
                        ("fence_bay_rail_top", post),
                        ("fence_bay_rail_mid", post),
                        ("fence_bay_rail_bot", post),
                        ("fence_bay_cap_l", post),
                        ("fence_bay_cap_r", post)
                    }
                    : new[]
                    {
                        ("fence_bay", panel),
                        ("fence_bay_post_l", post),
                        ("fence_bay_post_r", post)
                    };
                if (ArtGltfLoader.TryPlaceCombined(kit, parts, pos, rot, $"Fence bay {tag}", out _))
                {
                    placed++;
                    return;
                }

                PlacePart("fence_bay", pos, rot, panel, $"Fence bay {tag}");
                PlacePart("fence_bay_post_l", pos, rot, post, $"Fence bay post L {tag}");
                PlacePart("fence_bay_post_r", pos, rot, post, $"Fence bay post R {tag}");
                if (!AirsideRuntimeQuality.PlaceFenceRails)
                    return;
                PlacePart("fence_bay_rail_top", pos, rot, post, $"Fence bay rail top {tag}");
                PlacePart("fence_bay_rail_mid", pos, rot, post, $"Fence bay rail mid {tag}");
                PlacePart("fence_bay_rail_bot", pos, rot, post, $"Fence bay rail bot {tag}");
                PlacePart("fence_bay_cap_l", pos, rot, post, $"Fence bay cap L {tag}");
                PlacePart("fence_bay_cap_r", pos, rot, post, $"Fence bay cap R {tag}");
            }

            // North landside (gap for vehicle gate at x≈22–30).
            for (var x = -40; x <= 56; x += 4)
            {
                if (x >= 22 && x <= 30)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, 34f), 0f, $"N {x}");
            }

            // West / east airside — east faces inward with -90 yaw.
            for (var z = -18; z <= 32; z += 4)
            {
                PlaceBay(new Vector3(-44f, 0f, z + 2f), 90f, $"W {z}");
                PlaceBay(new Vector3(44f, 0f, z + 2f), -90f, $"E {z}");
            }

            // South above dunes (gap at runway strip).
            for (var x = -40; x <= 40; x += 4)
            {
                if (x >= -12 && x <= 12)
                    continue;
                PlaceBay(new Vector3(x + 2f, 0f, -20f), 0f, $"S {x}");
            }

            void PlaceCombo(string name, Vector3 pos, Quaternion rot, params (string Name, Color Color)[] parts)
            {
                if (ArtGltfLoader.TryPlaceCombined(kit, parts, pos, rot, name, out _))
                {
                    placed++;
                    return;
                }

                for (var i = 0; i < parts.Length; i++)
                    PlacePart(parts[i].Name, pos, rot, parts[i].Color, $"{name} {parts[i].Name}");
            }

            PlaceCombo("Fence corner NW", new Vector3(-44f, 0f, 34f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner NE", new Vector3(44f, 0f, 34f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner SW", new Vector3(-44f, 0f, -20f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));
            PlaceCombo("Fence corner SE", new Vector3(44f, 0f, -20f), Quaternion.identity,
                ("fence_corner", post), ("fence_corner_brace", post));

            // Vehicle gate at access road. Leaves keep unique yaw; the rest share identity rotation.
            PlacePart("gate_vehicle_leaf_l", new Vector3(24.2f, 0f, 35.6f), Quaternion.Euler(0f, 12f, 0f), yellow, "Gate leaf L");
            PlacePart("gate_vehicle_leaf_r", new Vector3(27.8f, 0f, 35.6f), Quaternion.Euler(0f, -12f, 0f), yellow, "Gate leaf R");
            var gateOrigin = new Vector3(26f, 0f, 34f);
            var chevron = new Color(0.15f, 0.15f, 0.16f);
            var light = new Color(0.95f, 0.35f, 0.12f);
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("gate_post", post),
                        ("gate_post", post),
                        ("gate_vehicle_rail", post),
                        ("gate_vehicle_chevron", chevron),
                        ("gate_vehicle_chevron", chevron),
                        ("gate_sign", AirsideTheme.SafetyYellow),
                        ("gate_sign_frame", post),
                        ("gate_post_light", light),
                        ("gate_post_light", light),
                        ("gate_latch", new Color(0.25f, 0.26f, 0.28f)),
                        ("gate_stop", AirsideTheme.Concrete),
                        ("gate_stop", AirsideTheme.Concrete)
                    },
                    gateOrigin, Quaternion.identity, "Vehicle gate", out _,
                    localOffsets: new[]
                    {
                        new Vector3(-3f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(0f, 0f, 1.55f),
                        new Vector3(-1.8f, 0f, 1.5f),
                        new Vector3(1.8f, 0f, 1.5f),
                        new Vector3(0f, 0f, 0.2f),
                        new Vector3(0f, 0f, 0.15f),
                        new Vector3(-3f, 0f, 0.15f),
                        new Vector3(3f, 0f, 0.15f),
                        new Vector3(0f, 0f, 1.5f),
                        new Vector3(-2.9f, 0f, 0.4f),
                        new Vector3(2.9f, 0f, 0.4f)
                    }))
            {
                placed++;
            }
            else
            {
                PlacePart("gate_post", new Vector3(23f, 0f, 34f), Quaternion.identity, post, "Gate post L");
                PlacePart("gate_post", new Vector3(29f, 0f, 34f), Quaternion.identity, post, "Gate post R");
                PlacePart("gate_vehicle_rail", new Vector3(26f, 0f, 35.55f), Quaternion.identity, post, "Gate vehicle rail");
                PlacePart("gate_vehicle_chevron", new Vector3(24.2f, 0f, 35.5f), Quaternion.identity, chevron, "Gate chevron L");
                PlacePart("gate_vehicle_chevron", new Vector3(27.8f, 0f, 35.5f), Quaternion.identity, chevron, "Gate chevron R");
                PlaceCombo("Gate sign", new Vector3(26f, 0f, 34.2f), Quaternion.identity,
                    ("gate_sign", AirsideTheme.SafetyYellow), ("gate_sign_frame", post));
                PlacePart("gate_post_light", new Vector3(23f, 0f, 34.15f), Quaternion.identity, light, "Gate light L");
                PlacePart("gate_post_light", new Vector3(29f, 0f, 34.15f), Quaternion.identity, light, "Gate light R");
                PlacePart("gate_latch", new Vector3(26f, 0f, 35.5f), Quaternion.identity, new Color(0.25f, 0.26f, 0.28f), "Gate latch");
                PlacePart("gate_stop", new Vector3(23.1f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop L");
                PlacePart("gate_stop", new Vector3(28.9f, 0f, 34.4f), Quaternion.identity, AirsideTheme.Concrete, "Gate stop R");
            }
            PlaceCombo("Pedestrian gate", new Vector3(20.6f, 0f, 34f), Quaternion.identity,
                ("gate_pedestrian", panel), ("gate_pedestrian_frame", post));
            PlaceCombo("Pedestrian gate E", new Vector3(31.4f, 0f, 34f), Quaternion.identity,
                ("gate_pedestrian", panel), ("gate_pedestrian_frame", post));

            return placed >= 20;
        }

        private static void BuildPerimeterFence()
        {
            // Perimeter fence removed — the kit ribbon blocked sight lines on the runway
            // and read as a cage around the airfield rather than a distant landside boundary.
            // Retain an explicit capture switch for regression comparison instead of a
            // literal return that hides the whole method from the compiler.
            if (!AirsideBareField.HasLaunchFlag("-airsidePerimeterFence"))
                return;

            // Batch F3 PRP-002 — modular fence/gate kit; dense CreateBlock ribbon remains fallback.
            if (TryBuildPerimeterFenceFromKit())
                return;

            var post = new Color(0.55f, 0.56f, 0.58f);
            var rail = new Color(0.72f, 0.74f, 0.76f);
            var mesh = new Color(0.62f, 0.64f, 0.66f);
            CreateBlock("Fence N west", new Vector3(-9f, 0.75f, 34f), new Vector3(62f, 1.35f, 0.08f), mesh);
            CreateBlock("Fence N east", new Vector3(43f, 0.75f, 34f), new Vector3(26f, 1.35f, 0.08f), mesh);
            CreateBlock("Fence rail N west", new Vector3(-9f, 1.25f, 34f), new Vector3(62f, 0.05f, 0.05f), rail);
            CreateBlock("Fence rail N east", new Vector3(43f, 1.25f, 34f), new Vector3(26f, 0.05f, 0.05f), rail);
            CreateBlock("Fence W", new Vector3(-44f, 0.75f, 7f), new Vector3(0.08f, 1.35f, 50f), mesh);
            CreateBlock("Fence E", new Vector3(44f, 0.75f, 7f), new Vector3(0.08f, 1.35f, 50f), mesh);
            CreateBlock("Fence rail W", new Vector3(-44f, 1.25f, 7f), new Vector3(0.05f, 0.05f, 50f), rail);
            CreateBlock("Fence rail E", new Vector3(44f, 1.25f, 7f), new Vector3(0.05f, 0.05f, 50f), rail);

            // Vehicle gate leaves at the access road (open inward to landside).
            CreateBlock("Gate post L", new Vector3(23f, 0.9f, 34f), new Vector3(0.22f, 1.8f, 0.22f), post);
            CreateBlock("Gate post R", new Vector3(29f, 0.9f, 34f), new Vector3(0.22f, 1.8f, 0.22f), post);
            CreateBlock("Gate leaf L", new Vector3(24.2f, 0.85f, 35.6f), new Vector3(2.2f, 1.5f, 0.08f), Shade(AirsideTheme.SafetyYellow, 0.75f));
            CreateBlock("Gate leaf R", new Vector3(27.8f, 0.85f, 35.6f), new Vector3(2.2f, 1.5f, 0.08f), Shade(AirsideTheme.SafetyYellow, 0.75f));
            CreateBlock("Gate rail L top", new Vector3(24.2f, 1.45f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail L mid", new Vector3(24.2f, 0.85f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail R top", new Vector3(27.8f, 1.45f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate rail R mid", new Vector3(27.8f, 0.85f, 35.55f), new Vector3(2.0f, 0.06f, 0.06f), rail);
            CreateBlock("Gate hinge L", new Vector3(23.15f, 0.9f, 34.35f), new Vector3(0.12f, 0.35f, 0.12f), mesh);
            CreateBlock("Gate hinge R", new Vector3(28.85f, 0.9f, 34.35f), new Vector3(0.12f, 0.35f, 0.12f), mesh);
            CreateBlock("Gate latch", new Vector3(26f, 0.95f, 35.5f), new Vector3(0.35f, 0.18f, 0.12f), new Color(0.25f, 0.26f, 0.28f));
            CreateBlock("Gate stop L", new Vector3(23.1f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate stop R", new Vector3(28.9f, 0.08f, 34.4f), new Vector3(0.35f, 0.12f, 0.35f), AirsideTheme.Concrete);
            CreateBlock("Gate sign", new Vector3(26f, 2.0f, 34.2f), new Vector3(1.6f, 0.55f, 0.06f), AirsideTheme.SafetyYellow);
            CreateBlock("Gate sign frame", new Vector3(26f, 2.0f, 34.15f), new Vector3(1.75f, 0.68f, 0.04f), post);
            CreateBlock("Gate light L", new Vector3(23f, 1.85f, 34.15f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));
            CreateBlock("Gate light R", new Vector3(29f, 1.85f, 34.15f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));

            CreateBlock("Fence S west", new Vector3(-26f, 0.7f, -20f), new Vector3(28f, 1.3f, 0.08f), mesh);
            CreateBlock("Fence S east", new Vector3(26f, 0.7f, -20f), new Vector3(28f, 1.3f, 0.08f), mesh);
            CreateBlock("Fence rail S west", new Vector3(-26f, 1.15f, -20f), new Vector3(28f, 0.05f, 0.05f), rail);
            CreateBlock("Fence rail S east", new Vector3(26f, 1.15f, -20f), new Vector3(28f, 0.05f, 0.05f), rail);
            CreateBlock("Gate chevron L", new Vector3(24.2f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));
            CreateBlock("Gate chevron R", new Vector3(27.8f, 0.85f, 35.45f), new Vector3(1.8f, 0.35f, 0.04f),
                new Color(0.15f, 0.15f, 0.16f));
        }

        /// <summary>
        /// Decision 0025 item 3 — landside canopy, posts and glass so the terminal
        /// entrance reads as a building, not a flat box, from overview and landside.
        /// </summary>
        private static void BuildTerminalLandsideCanopy()
        {
            var steel = new Color(0.48f, 0.5f, 0.52f);
            var glass = new Color(0.18f, 0.42f, 0.55f);
            var soffit = new Color(0.62f, 0.64f, 0.66f);
            // Terminal kits already carry canopy / landside glass — skip greybox densify.
            var hasKitCanopy = FindBuilt("canopy") != null
                || FindBuilt("canopy_soffit") != null
                || FindBuilt("canopy_beam") != null;
            if (!hasKitCanopy)
            {
                CreateBlock("Terminal canopy slab W", new Vector3(22f, 3.55f, 31.2f), new Vector3(8f, 0.18f, 4.2f), soffit);
                CreateBlock("Terminal canopy slab E", new Vector3(30f, 3.55f, 31.25f), new Vector3(7.6f, 0.1728f, 4.074f), Shade(soffit, 0.97f));
                CreateBlock("Terminal canopy edge W", new Vector3(22f, 3.4f, 33.1f), new Vector3(8.1f, 0.22f, 0.25f), steel);
                CreateBlock("Terminal canopy edge E", new Vector3(30.1f, 3.4f, 33.15f), new Vector3(7.7f, 0.2112f, 0.2425f), Shade(steel, 0.95f));
                for (var i = 0; i < 5; i++)
                {
                    var x = 18.5f + i * 3.75f;
                    CreateBlock($"Terminal canopy post {i}", new Vector3(x, 1.7f, 32.6f), new Vector3(0.22f, 3.4f, 0.22f), steel);
                }

                CreateBlock("Terminal landside glass W", new Vector3(22.5f, 2.1f, 29.55f), new Vector3(7f, 2.6f, 0.1f), glass,
                    "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(1.25f, 1.2f));
                CreateBlock("Terminal landside glass E", new Vector3(29.5f, 2.1f, 29.55f), new Vector3(6.65f, 2.496f, 0.097f), glass,
                    "Textures/Environment/tx_terminal_glass_mask_v01.png", new Vector2(1.25f, 1.2f));
                CreateBlock("Terminal entrance frame", new Vector3(26f, 1.6f, 29.5f), new Vector3(3.2f, 2.8f, 0.18f), steel);
                CreateBlock("Terminal doors", new Vector3(26f, 1.45f, 29.35f), new Vector3(2.6f, 2.4f, 0.08f), new Color(0.22f, 0.28f, 0.32f));
                CreateBlock("Terminal canopy glow W", new Vector3(22.5f, 3.35f, 31.2f), new Vector3(6f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
                CreateBlock("Terminal canopy glow E", new Vector3(29.5f, 3.35f, 31.25f), new Vector3(5.7f, 0.0576f, 3.104f), new Color(1f, 0.85f, 0.55f));
            }
            else if (FindBuilt("canopy_light_l") == null
                     && FindBuilt("canopy_light_r") == null
                     && FindBuilt("canopy_light_mid") == null)
            {
                CreateBlock("Terminal canopy glow W", new Vector3(22.5f, 3.35f, 31.2f), new Vector3(6f, 0.06f, 3.2f), new Color(1f, 0.85f, 0.55f));
                CreateBlock("Terminal canopy glow E", new Vector3(29.5f, 3.35f, 31.25f), new Vector3(5.7f, 0.0576f, 3.104f), new Color(1f, 0.85f, 0.55f));
            }

            // Batch F3 PRP-003 — prefer forecourt kit for bench/planter/bollards/sign; blocks remain fallback.
            if (!TryPlaceForecourtFromKit())
            {
                CreateBlock("Terminal bench", new Vector3(21f, 0.35f, 31.6f), new Vector3(2.4f, 0.35f, 0.55f), new Color(0.4f, 0.32f, 0.22f));
                CreateBlock("Terminal planter", new Vector3(31.5f, 0.35f, 31.8f), new Vector3(1.4f, 0.5f, 1.0f), AirsideTheme.Concrete);
                CreateBlock("Terminal planter scrub", new Vector3(31.5f, 0.85f, 31.8f), new Vector3(1.1f, 0.55f, 0.7f), Shade(AirsideTheme.Eucalyptus, 0.85f));
            }
        }

        private static void BuildVegetation()
        {
            // Stylised eucalyptus clumps — denser belts so overview reads as KI bush, not
            // a handful of props (0025 item 3). PlaceTree prefers VEG-001 v02→v01.
            var scrubKit = PreferArtKit(
                "Models/Environment/mdl_kingscote_scrub_kit_v02.gltf",
                "Models/Environment/mdl_kingscote_scrub_kit_v01.gltf");
            var hasScrubKit = !string.IsNullOrEmpty(scrubKit) && ArtGltfLoader.HasKit(scrubKit);

            var trees = new (Vector3 Pos, float Scale)[]
            {
                (new Vector3(-32f, 0f, 30f), 1.1f),
                (new Vector3(-38f, 0f, 22f), 0.9f),
                (new Vector3(-28f, 0f, 36f), 1.25f),
                (new Vector3(-42f, 0f, 34f), 1.05f),
                (new Vector3(-46f, 0f, 26f), 0.88f),
                (new Vector3(-24f, 0f, 40f), 0.95f),
                (new Vector3(40f, 0f, 30f), 1.0f),
                (new Vector3(52f, 0f, 34f), 1.15f),
                (new Vector3(58f, 0f, 28f), 0.85f),
                (new Vector3(46f, 0f, 40f), 1.05f),
                (new Vector3(36f, 0f, 52f), 1.2f),
                (new Vector3(62f, 0f, 42f), 0.92f),
                (new Vector3(-52f, 0f, 8f), 1.3f),
                (new Vector3(-48f, 0f, -8f), 0.95f),
                (new Vector3(-56f, 0f, -2f), 1.1f),
                (new Vector3(-44f, 0f, 14f), 0.82f),
                (new Vector3(50f, 0f, -10f), 1.05f),
                (new Vector3(56f, 0f, 8f), 0.9f),
                (new Vector3(62f, 0f, -4f), 1.15f),
                (new Vector3(54f, 0f, 18f), 0.78f),
                (new Vector3(-18f, 0f, 42f), 0.8f),
                (new Vector3(-8f, 0f, 46f), 0.95f),
                (new Vector3(8f, 0f, 44f), 1.05f),
                (new Vector3(16f, 0f, 50f), 0.88f),
                // South fringe above the dunes (keep clear of runway strip).
                (new Vector3(-40f, 0f, -28f), 0.9f),
                (new Vector3(-28f, 0f, -32f), 1.0f),
                (new Vector3(28f, 0f, -30f), 0.95f),
                (new Vector3(40f, 0f, -26f), 1.1f),
                (new Vector3(-60f, 0f, 20f), 1.2f),
                (new Vector3(68f, 0f, 16f), 1.05f),
                // Extra belt density so overview reads as continuous KI bush (0025 item 3).
                (new Vector3(-34f, 0f, 48f), 1.0f),
                (new Vector3(-20f, 0f, 52f), 1.15f),
                (new Vector3(4f, 0f, 54f), 0.9f),
                (new Vector3(22f, 0f, 50f), 1.05f),
                (new Vector3(44f, 0f, 56f), 1.2f),
                (new Vector3(-58f, 0f, 32f), 0.95f),
                (new Vector3(70f, 0f, 30f), 1.1f),
                (new Vector3(-64f, 0f, -10f), 1.05f),
                (new Vector3(66f, 0f, -14f), 0.88f),
                (new Vector3(-50f, 0f, -30f), 1.0f),
                (new Vector3(48f, 0f, -32f), 1.12f),
                // Far paddock belt — denser KI fringe from overview (0025 item 3).
                (new Vector3(-70f, 0f, 40f), 1.25f),
                (new Vector3(-66f, 0f, 50f), 1.05f),
                (new Vector3(74f, 0f, 38f), 1.15f),
                (new Vector3(72f, 0f, 48f), 0.95f),
                (new Vector3(-72f, 0f, 8f), 1.1f),
                (new Vector3(76f, 0f, 6f), 1.0f),
                (new Vector3(-12f, 0f, 58f), 1.2f),
                (new Vector3(30f, 0f, 58f), 1.08f),
                // Inland paddock densify — close the gaps between belts (0025 item 3).
                (new Vector3(-38f, 0f, 56f), 1.1f),
                (new Vector3(12f, 0f, 60f), 0.95f),
                (new Vector3(52f, 0f, 60f), 1.15f),
                (new Vector3(-68f, 0f, -22f), 1.05f),
                (new Vector3(70f, 0f, -20f), 0.9f),
                (new Vector3(-55f, 0f, 55f), 1.2f),
                (new Vector3(58f, 0f, 54f), 1.0f),
                (new Vector3(-25f, 0f, -36f), 0.85f),
                (new Vector3(18f, 0f, -34f), 1.0f),
                // Close N/E paddock holes from overview (0025 item 3).
                (new Vector3(-42f, 0f, 62f), 1.15f),
                (new Vector3(-15f, 0f, 62f), 1.05f),
                (new Vector3(6f, 0f, 64f), 0.92f),
                (new Vector3(26f, 0f, 62f), 1.1f),
                (new Vector3(48f, 0f, 64f), 1.0f),
                (new Vector3(64f, 0f, 58f), 1.18f),
                (new Vector3(-75f, 0f, 28f), 1.08f),
                (new Vector3(78f, 0f, 24f), 0.95f),
                (new Vector3(-8f, 0f, -38f), 0.88f),
                (new Vector3(36f, 0f, -36f), 1.02f)
            };
            // Place the full belt with authored VEG-001 silhouettes when the kit is
            // present (v02 densifies far paddock too). Primitive greybox still covers
            // every slot if the kit is missing.
            var treeCount = trees.Length;
            for (var i = 0; i < treeCount; i++)
                PlaceTree(trees[i].Pos, trees[i].Scale);

            // Low shrub / scrub clusters along fence and car-park edges.
            var shrubs = new[]
            {
                new Vector3(-30f, 0f, 33f), new Vector3(-22f, 0f, 35f), new Vector3(-14f, 0f, 33.5f),
                new Vector3(12f, 0f, 34.5f), new Vector3(34f, 0f, 33f), new Vector3(40f, 0f, 36f),
                new Vector3(54f, 0f, 50f), new Vector3(50f, 0f, 54f), new Vector3(60f, 0f, 44f),
                new Vector3(-50f, 0f, 4f), new Vector3(-54f, 0f, -12f), new Vector3(48f, 0f, -16f),
                new Vector3(-36f, 0f, -24f), new Vector3(32f, 0f, -22f), new Vector3(0f, 0f, 38f)
            };
            for (var i = 0; i < shrubs.Length; i++)
                PlaceShrub(shrubs[i], 0.7f + (i % 4) * 0.12f);

            // Extra inland scrub — thin when VEG-002 already stamps authored clumps.
            var inlandScrub = new[]
            {
                new Vector3(-62f, 0f, 44f), new Vector3(-58f, 0f, 52f), new Vector3(-45f, 0f, 58f),
                new Vector3(-30f, 0f, 58f), new Vector3(-5f, 0f, 56f), new Vector3(18f, 0f, 58f),
                new Vector3(40f, 0f, 58f), new Vector3(62f, 0f, 52f), new Vector3(68f, 0f, 42f),
                new Vector3(72f, 0f, 22f), new Vector3(70f, 0f, -8f), new Vector3(-70f, 0f, -6f),
                new Vector3(-66f, 0f, 18f), new Vector3(8f, 0f, 40f), new Vector3(-4f, 0f, 36f)
            };
            var inlandCount = inlandScrub.Length;
            for (var i = 0; i < inlandCount; i++)
                PlaceShrub(inlandScrub[i], 0.75f + (i % 5) * 0.1f);

            // Fence-line scrub carpet — denser when VEG-002 kit stamps authored clumps.
            var fenceStep = hasScrubKit ? 5 : 4;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                fenceStep += 3;
            for (var x = -70; x <= 70; x += fenceStep)
            {
                PlaceShrubClump(new Vector3(x, 0f, 36f + (x % 5) * 0.2f), 0.55f + (Mathf.Abs(x) % 4) * 0.08f);
                if (x % (fenceStep * 2) == 0)
                    PlaceShrubClump(new Vector3(x + 1.5f, 0f, 40f), 0.7f);
            }

            var sideStep = hasScrubKit ? 6 : 5;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                sideStep += 3;
            for (var z = -20; z <= 50; z += sideStep)
            {
                PlaceShrubClump(new Vector3(-48f - (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
                PlaceShrubClump(new Vector3(50f + (z % 3) * 0.4f, 0f, z), 0.6f + (Mathf.Abs(z) % 3) * 0.1f);
            }

            // Between apron fringe and N fence.
            var fringeStep = hasScrubKit ? 3 : 3;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                fringeStep += 3;
            for (var x = 6; x <= 34; x += fringeStep)
                PlaceShrubClump(new Vector3(x, 0f, 28.5f + (x % 2) * 0.4f), 0.5f);

            // Dense coastal scrub belt — prefer VEG-002 clumps over greybox cubes.
            var coastStep = hasScrubKit ? 4 : 5;
            if (AirsideRuntimeQuality.Current != AirsideRuntimeQuality.Ladder.High)
                coastStep += 3;
            for (var x = -55; x <= 55; x += coastStep)
            {
                var zJitter = ((x * 13) % 7) * 0.15f;
                PlaceShrub(new Vector3(x, 0f, -39.5f + zJitter), 0.7f + (Mathf.Abs(x) % 4) * 0.06f);
                if (x % (coastStep * 2) == 0)
                    PlaceShrub(new Vector3(x + 1.5f, 0f, -37.5f), 0.65f);
                // Extra dune-edge stamp so overview matches the scrub style sheet belt.
                if (hasScrubKit && AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High && x % 8 == 0)
                    PlaceShrub(new Vector3(x + 0.8f, 0f, -41.2f + zJitter * 0.5f), 0.85f);
            }
        }

        /// <summary>
        /// Batch F3 WLD-004 — soft hill/dune accents outside operational geometry.
        /// Does not replace runway/apron/stand code-owned surfaces.
        /// </summary>
        private static bool TryPlaceContextTerrainAccents()
        {
            var kit = PreferArtKit(
                "Models/Environment/mdl_kingscote_context_terrain_v02.gltf",
                "Models/Environment/mdl_kingscote_context_terrain_v01.gltf");
            if (string.IsNullOrEmpty(kit) || !ArtGltfLoader.HasKit(kit))
                return false;

            var placed = 0;
            void Place(string mesh, Vector3 pos, Quaternion rot, Color color, string name, float scale = 1f)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, pos, rot, color, out var part, localScale: Vector3.one * scale))
                    return;
                part.name = name;
                placed++;
            }

            var euc = Shade(AirsideTheme.Eucalyptus, 0.45f);
            var dry = Shade(AirsideTheme.DryGrass, 0.55f);
            var sand = Shade(AirsideTheme.Sand, 0.75f);
            Place("hill_a", new Vector3(-85f, 0f, 68f), Quaternion.identity, euc, "Context hill NW", 2.8f);
            Place("hill_b", new Vector3(90f, 0f, 62f), Quaternion.Euler(0f, 25f, 0f), dry, "Context hill NE", 2.6f);
            Place("hill_c", new Vector3(-95f, 0f, 8f), Quaternion.Euler(0f, 40f, 0f), euc, "Context hill W", 2.4f);
            Place("hill_a", new Vector3(100f, 0f, 4f), Quaternion.Euler(0f, -30f, 0f), dry, "Context hill E", 2.3f);
            // WLD-004 v02 densify — extra mid-horizon hills kept clear of ops.
            Place("hill_b", new Vector3(-78f, 0f, 48f), Quaternion.Euler(0f, 12f, 0f), dry, "Context hill NW mid", 1.9f);
            Place("hill_c", new Vector3(82f, 0f, 46f), Quaternion.Euler(0f, -18f, 0f), euc, "Context hill NE mid", 1.85f);
            Place("dune_a", new Vector3(-40f, 0f, -52f), Quaternion.identity, sand, "Context dune SW", 2.0f);
            Place("dune_b", new Vector3(35f, 0f, -50f), Quaternion.Euler(0f, 15f, 0f), sand, "Context dune SE", 1.9f);
            Place("dune_a", new Vector3(-12f, 0f, -54f), Quaternion.Euler(0f, -8f, 0f), Shade(sand, 0.92f), "Context dune S mid", 1.55f);
            Place("dune_b", new Vector3(12f, 0f, -53f), Quaternion.Euler(0f, 22f, 0f), Shade(sand, 0.95f), "Context dune S mid E", 1.5f);
            Place("berm", new Vector3(0f, 0f, -42f), Quaternion.identity, Shade(sand, 0.9f), "Context coast berm", 2.8f);
            // Near-field coast / paddock accents from the same WLD-004 kit (textured slabs remain).
            Place("coast_sand", new Vector3(-55f, -0.2f, -48f), Quaternion.identity, sand, "Context coast sand W", 1.8f);
            Place("coast_sand", new Vector3(55f, -0.2f, -48f), Quaternion.Euler(0f, 180f, 0f), sand, "Context coast sand E", 1.8f);
            Place("coast_shallows", new Vector3(-30f, -0.5f, -58f), Quaternion.identity, new Color(0.32f, 0.62f, 0.72f), "Context shallows W", 1.4f);
            Place("coast_shallows", new Vector3(30f, -0.5f, -58f), Quaternion.Euler(0f, 180f, 0f), new Color(0.32f, 0.62f, 0.72f), "Context shallows E", 1.4f);
            Place("coast_shallows", new Vector3(0f, -0.45f, -56f), Quaternion.identity, new Color(0.35f, 0.66f, 0.74f), "Context shallows mid", 1.2f);
            Place("coast_water", new Vector3(0f, -0.8f, -70f), Quaternion.identity, new Color(0.18f, 0.38f, 0.52f), "Context coast water", 2.2f);
            Place("paddock_n", new Vector3(-50f, -0.3f, 42f), Quaternion.identity, dry, "Context paddock NW", 2.1f);
            Place("paddock_s", new Vector3(50f, -0.3f, 42f), Quaternion.Euler(0f, 180f, 0f), dry, "Context paddock NE", 2.0f);
            Place("paddock_e", new Vector3(62f, -0.3f, -20f), Quaternion.identity, euc, "Context paddock E", 1.75f);
            Place("paddock_w", new Vector3(-62f, -0.3f, -18f), Quaternion.Euler(0f, 20f, 0f), euc, "Context paddock W", 1.75f);
            return placed > 0;
        }

        /// <summary>
        /// Soft contact blobs under primary buildings so Lit surfaces read grounded
        /// without waiting for a full shadow-cascade bake (0025 items 3+5).
        /// </summary>
        private static void BuildBuildingContactShadows()
        {
            // Soft, tight discs — oversized near-black cylinders read as ground patches at night.
            PlaceContactShadow("Terminal contact", new Vector3(26f, 0.035f, 27f), new Vector3(14f, 0.02f, 4.5f), 0.14f);
            PlaceContactShadow("Hangar contact", new Vector3(-20f, 0.035f, 20f), new Vector3(10f, 0.02f, 7f), 0.14f);
            PlaceContactShadow("Ops contact", new Vector3(-8f, 0.035f, 26f), new Vector3(5f, 0.02f, 3.5f), 0.12f);
            PlaceContactShadow("Fuel farm contact", new Vector3(-34f, 0.035f, 22f), new Vector3(5.5f, 0.015f, 4.5f), 0.1f);
            PlaceContactShadow("ARFF contact", new Vector3(-28f, 0.035f, 30f), new Vector3(5.5f, 0.015f, 4.5f), 0.1f);
            PlaceContactShadow("Canopy contact", new Vector3(26f, 0.035f, 31.5f), new Vector3(10f, 0.015f, 3.2f), 0.08f);
        }

        private void CollectHangarDoorPanels()
        {
            _hangarDoorPanels.Clear();
            // Authored / kit hangar doors — slide L/R panels instead of a single greybox slab.
            foreach (var name in new[]
                     {
                         "door_panel_l", "door_panel_r", "door_rib_l", "door_rib_r",
                         "door_bar_l1", "door_bar_l2", "door_bar_l3", "door_bar_l4",
                         "door_bar_r1", "door_bar_r2", "door_bar_r3", "door_bar_r4",
                         "door_handle_l", "door_handle_r",
                         "door_warning_l", "door_warning_r"
                     })
            {
                var t = AirsideSceneIndex.Find(name);
                if (t == null)
                    continue;
                var openDelta = name.Contains("_l", StringComparison.Ordinal) ? -3.6f : 3.6f;
                _hangarDoorPanels.Add((t, t.localPosition.x, openDelta));
            }

            // Avoid stacking the procedural slab on top of authored hangar doors.
            if (_hangarDoorPanels.Count > 0 && _hangarDoor != null)
            {
                _hangarDoor.gameObject.SetActive(false);
                _hangarDoor = null;
            }
        }

        private void UpdateHangarDoor()
        {
            if (_hangarDoor == null && _hangarBayLight == null && _hangarDoorPanels.Count == 0)
                return;

            // Presentation-only: hangar door slides open by day, closes at night.
            // Also opens wider when a commercial aircraft is near the hangar apron.
            var daylight = PresentationDaylight;
            var openAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((daylight - 0.15f) / 0.35f));
            // Only an aircraft actually near the hangar opens the door further. The test used
            // to be "anything on the field is parked or taxiing", which in airline mode is
            // true around the clock, so the door never closed at night.
            var hangar = _hangarDoor != null ? _hangarDoor.position
                : _hangarDoorPanels.Count > 0 && _hangarDoorPanels[0].Panel != null
                    ? _hangarDoorPanels[0].Panel.position
                    : (Vector3?)null;
            if (hangar.HasValue)
            {
                for (var i = 0; i < VisualFlights.Count && i < _commercialAircraft.Length; i++)
                {
                    var flight = VisualFlights[i];
                    if (flight.Operation.Phase is not (AircraftPhase.TaxiIn or AircraftPhase.AtStand
                        or AircraftPhase.TaxiOut or AircraftPhase.Pushback))
                        continue;
                    var view = _commercialAircraft[i];
                    if (view == null || !view.gameObject.activeInHierarchy)
                        continue;
                    var offset = view.position - hangar.Value;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > HangarDoorOpensWithinMetres * HangarDoorOpensWithinMetres)
                        continue;
                    openAmount = Mathf.Max(openAmount, 0.85f);
                    break;
                }
            }

            if (_hangarDoor != null)
            {
                var targetX = Mathf.Lerp(_hangarDoorClosedX, _hangarDoorClosedX - 7.2f, openAmount);
                var pos = _hangarDoor.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, Time.unscaledDeltaTime * 1.8f);
                _hangarDoor.position = pos;
            }

            for (var i = 0; i < _hangarDoorPanels.Count; i++)
            {
                var (panel, closedX, openDelta) = _hangarDoorPanels[i];
                if (panel == null)
                    continue;
                var local = panel.localPosition;
                var target = closedX + openDelta * openAmount;
                local.x = Mathf.MoveTowards(local.x, target, Time.unscaledDeltaTime * 1.6f);
                panel.localPosition = local;
            }

            // Warm bay spill: brighter when the door is open by day; soft night work-light when closed.
            if (_hangarBayLight != null)
            {
                var daySpill = openAmount * 1.55f;
                var nightGlow = (1f - daylight) * 0.72f;
                _hangarBayLight.intensity = Mathf.Max(0.1f, daySpill + nightGlow);
                // Warmer at night and when the door is shut. The daylight tint used to be written
                // and then immediately overwritten by the door tint, so it never applied.
                var dayTint = Color.Lerp(new Color(1f, 0.82f, 0.55f), new Color(1f, 0.92f, 0.7f), daylight);
                var doorTint = Color.Lerp(new Color(1f, 0.78f, 0.48f), new Color(1f, 0.92f, 0.72f), openAmount);
                _hangarBayLight.color = Color.Lerp(dayTint, doorTint, 0.5f);
            }
        }

        private static void BuildStandMarking(float x, float z, string name)
        {
            // Markings kit already paints stand_stop + digits + chevrons — skip yellow densify.
            if (FindBuilt("stand_stop_a") != null
                || FindBuilt("stand_stop_b") != null
                || FindBuilt("stand_stop_c") != null)
                return;

            CreateBlock(name, new Vector3(x, 0.08f, z), new Vector3(0.18f, 0.03f, 4.2f), new Color(0.96f, 0.77f, 0.12f));
            CreateBlock($"{name} stop", new Vector3(x, 0.08f, z + 1.9f), new Vector3(3.4f, 0.03f, 0.18f), new Color(0.96f, 0.77f, 0.12f));
        }

        /// <summary>
        /// Soft elliptical ground shadow under each aircraft (presentation only).
        /// </summary>
        private static void EnsureGroundShadow(Transform aircraft)
        {
            if (aircraft.Find("GroundShadow") != null)
                return;

            var profile = aircraft.GetComponent<AircraftVisualProfileComponent>();
            var width = profile != null ? profile.ShadowWidthMetres : 22.5f;
            var depth = profile != null ? profile.ShadowDepthMetres : 16.5f;
            var centre = profile != null ? profile.VisualCentreOffsetMetres : Vector3.zero;
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "GroundShadow";
            DestroyPresentationObject(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(aircraft, false);
            shadow.transform.localPosition = new Vector3(centre.x, -0.55f, centre.z);
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localScale = new Vector3(width, 0.012f, depth);
            var material = AirsideMaterialLibrary.CreateShared(new Color(0.05f, 0.06f, 0.08f, 0.16f),
                AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            SetRendererColor(renderer, new Color(0.05f, 0.06f, 0.08f, 0.16f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void UpdateGroundShadow(Transform aircraft)
        {
            var parts = PartsFor(aircraft);
            var shadow = parts.Shadow;
            if (shadow == null)
                return;

            var groundY = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.5f + 0.02f;
            var profile = parts.Profile;
            var visualCentre = profile != null
                ? aircraft.TransformPoint(profile.VisualCentreOffsetMetres)
                : aircraft.position;
            var ground = new Vector3(visualCentre.x, groundY, visualCentre.z);
            shadow.position = ground;
            shadow.rotation = Quaternion.identity;
            var altitude = Mathf.Max(0f, aircraft.position.y - AirsideFlightPath.GroundY);
            var t = Mathf.Clamp01(altitude / 18f);
            var baseWidth = profile != null ? profile.ShadowWidthMetres : 22.5f;
            var baseDepth = profile != null ? profile.ShadowDepthMetres : 16.5f;
            var width = Mathf.Lerp(baseWidth, baseWidth * 1.3333f, t);
            var depth = Mathf.Lerp(baseDepth, baseDepth * 1.3333f, t);
            var sx = aircraft.lossyScale.x > 0.001f ? width / aircraft.lossyScale.x : width;
            var sy = aircraft.lossyScale.y > 0.001f ? 0.03f / aircraft.lossyScale.y : 0.03f;
            var sz = aircraft.lossyScale.z > 0.001f ? depth / aircraft.lossyScale.z : depth;
            shadow.localScale = new Vector3(sx, sy, sz);

            var renderer = parts.ShadowRenderer;
            if (renderer == null)
                return;
            var color = GetRendererColor(renderer);
            // Softer contact so realtime URP shadows remain the primary read.
            color.a = Mathf.Lerp(0.28f, 0.04f, t);
            SetRendererColor(renderer, color);
            shadow.gameObject.SetActive(aircraft.gameObject.activeInHierarchy);
        }

        /// <summary>
        /// Prefer denser surface basecolours (v02 fidelity board) when present; keep v01 fallback.
        /// </summary>
        private static string PreferSurfaceBasecolor(string stem) => PreferSurfaceMap(stem, "basecolor");

        private static string PreferSurfaceMap(string stem, string map)
        {
            if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(map))
                return null;
            var cacheKey = stem + "|" + map;
            if (SurfaceBasecolorCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string chosen = null;
            var v03 = $"Textures/Surfaces/{stem}_{map}_v03.png";
            if (ArtRuntimePaths.ResolveExisting(v03) != null)
                chosen = v03;
            else
            {
                var v02 = $"Textures/Surfaces/{stem}_{map}_v02.png";
                if (ArtRuntimePaths.ResolveExisting(v02) != null)
                    chosen = v02;
                else
                    chosen = $"Textures/Surfaces/{stem}_{map}_v01.png";
            }

            SurfaceBasecolorCache[cacheKey] = chosen;
            return chosen;
        }

        private static void PlaceBuildingOrFallback(
            string artRelativePath,
            Vector3 worldPosition,
            System.Func<string, Color?> colorFor,
            System.Action fallback,
            string glassTextureRelativePath = null,
            string surfaceTextureRelativePath = null,
            Vector2? surfaceTextureTiling = null,
            string[] surfaceMeshNames = null)
        {
            if (ArtPresentationLoader.TryInstantiate(artRelativePath, null, out var root, rename: null, colorFor: colorFor))
            {
                root.position = worldPosition;
                AirsideStaticWorld.Attach(root);
                if (!string.IsNullOrEmpty(glassTextureRelativePath))
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    {
                        var n = child.name;
                        if (n is not ("glass_front" or "landside_glass" or "windows" or "cabin_windows"
                            or "door_glass" or "window_l" or "window_r" or "window_side" or "window_side_b"
                            or "windshield" or "rear_window" or "side_window" or "side_window_b"
                            or "office_window" or "skylight_l" or "skylight_r" or "skylight_mid"
                            or "door_peek_l" or "door_peek_r" or "boarding_glass" or "service_window")
                            && !n.StartsWith("glass_pane", StringComparison.Ordinal))
                            continue;
                        var renderer = child.GetComponent<Renderer>();
                        if (renderer == null)
                            continue;
                        var texture = TryLoadArtTexture(glassTextureRelativePath);
                        if (texture != null)
                            ApplyRendererTexture(renderer, texture, new Vector2(3f, 1.5f));
                    }
                }

                if (!string.IsNullOrEmpty(surfaceTextureRelativePath))
                {
                    var surface = TryLoadArtTexture(surfaceTextureRelativePath);
                    if (surface != null)
                    {
                        var tiling = surfaceTextureTiling ?? new Vector2(2f, 1.5f);
                        foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.name is "glass_front" or "door_opening" or "entrance"
                                or "window_l" or "window_r" or "window_side" or "window_side_b"
                                or "cabin_windows" or "cockpit" or "landside_glass"
                                or "windshield" or "rear_window" or "side_window" or "side_window_b"
                                or "office_window" or "skylight_l" or "skylight_r" or "skylight_mid")
                                continue;
                            if (child.name.StartsWith("glass_pane", StringComparison.Ordinal))
                                continue;
                            if (surfaceMeshNames != null && surfaceMeshNames.Length > 0)
                            {
                                var match = false;
                                for (var i = 0; i < surfaceMeshNames.Length; i++)
                                {
                                    if (child.name == surfaceMeshNames[i] ||
                                        child.name.StartsWith(surfaceMeshNames[i], StringComparison.Ordinal))
                                    {
                                        match = true;
                                        break;
                                    }
                                }

                                if (!match)
                                    continue;
                            }

                            var renderer = child.GetComponent<Renderer>();
                            if (renderer == null)
                                continue;
                            ApplyRendererTexture(renderer, surface, tiling, 0.28f);
                        }
                    }
                }

                return;
            }

            fallback();
        }

        private static void PlaceWorldMarkings()
        {
            // WLD-001 centreline: kit strip runs along Z; rotate onto the X runway axis.
            const string kit = "Models/Props/mdl_airfield_markings_kit_v01.gltf";
            var usedCentre = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_centreline", Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            // Extra dash meshes only when the strip missed — otherwise they double the GPU.
            if (!usedCentre)
            {
                CreateBlock("Runway marking mid W", new Vector3(-20f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid E", new Vector3(20f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid 0", new Vector3(0f, 0.022f, 0f), new Vector3(2.2f, 0.025f, 0.24f), Color.white);
                CreateBlock("Runway marking mid W2", new Vector3(-34f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid W3", new Vector3(-6f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid E2", new Vector3(6f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking mid E3", new Vector3(34f, 0.022f, 0f), new Vector3(2.0f, 0.025f, 0.22f), Color.white);
                CreateBlock("Runway marking", new Vector3(0f, 0.02f, 0f), new Vector3(86f, 0.03f, 0.26f), Color.white);
            }

            // Kit edge / threshold strips when present; greybox fallbacks keep the strip readable.
            var usedEdgeL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_left", new Vector3(0f, 0.025f, -3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedEdgeR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_edge_right", new Vector3(0f, 0.025f, 3.35f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            if (!usedEdgeL)
                CreateBlock("Runway edge L", new Vector3(0f, 0.025f, -3.35f), new Vector3(86f, 0.02f, 0.16f), Color.white);

            if (!usedEdgeR)
                CreateBlock("Runway edge R", new Vector3(0f, 0.025f, 3.35f), new Vector3(86f, 0.02f, 0.16f), Color.white);

            var thresholdW = AirportLayout.WestThresholdX;
            var thresholdE = AirportLayout.EastThresholdX;
            var usedThresholdW = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(thresholdW, 0.03f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedThresholdE = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "runway_threshold", new Vector3(thresholdE, 0.03f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            // Extra bar meshes only when a threshold strip missed.
            if (!usedThresholdW || !usedThresholdE)
            {
                foreach (var bar in new[] { "threshold_bar_a", "threshold_bar_b", "threshold_bar_c", "threshold_bar_d" })
                {
                    if (!usedThresholdW)
                        ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(-44f, 0.032f, 0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
                    if (!usedThresholdE)
                        ArtGltfLoader.TryPlaceNamedMesh(kit, bar, new Vector3(44f, 0.032f, 0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
                }
            }

            var usedSideWL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(-44f, 0.032f, -3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideWR = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(-44f, 0.032f, 3.0f), Quaternion.Euler(0f, 90f, 0f), Color.white, out _);
            var usedSideEL = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_l", new Vector3(44f, 0.032f, -3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            var usedSideER = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "threshold_side_r", new Vector3(44f, 0.032f, 3.0f), Quaternion.Euler(0f, -90f, 0f), Color.white, out _);
            if (!usedThresholdW)
                CreateBlock("Threshold W", new Vector3(thresholdW, 0.03f, 0f), new Vector3(2.2f, 0.02f, 5.4f), Color.white);
            if (!usedThresholdE)
                CreateBlock("Threshold E", new Vector3(thresholdE, 0.03f, 0f), new Vector3(2.2f, 0.02f, 5.4f), Color.white);

            var holdYellow = new Color(0.95f, 0.82f, 0.12f);
            var holdX = AirsideFlightPath.HoldShortX;
            var holdZ = AirportTaxiNetwork.RunwayHoldingPositionZ;
            var usedHoldA = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_a", new Vector3(holdX, 0.05f, holdZ + 0.1f), Quaternion.identity, holdYellow, out var holdA);
            var usedHoldB = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_b", new Vector3(holdX, 0.05f, holdZ + 0.6f), Quaternion.identity, holdYellow, out var holdB);
            var usedHoldC = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_c", new Vector3(4f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdC);
            var usedHoldD = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_d", new Vector3(4f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdD);
            if (holdA != null) holdA.name = "Hold short A";
            if (holdB != null) holdB.name = "Hold short B";
            if (holdC != null) holdC.name = "Hold short C";
            if (holdD != null) holdD.name = "Hold short D";
            if (!usedHoldA)
                CreateBlock("Hold short A", new Vector3(holdX, 0.05f, holdZ + 0.1f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldB)
                CreateBlock("Hold short B", new Vector3(holdX, 0.05f, holdZ + 0.6f), new Vector3(4.2f, 0.03f, 0.22f), holdYellow);
            if (!usedHoldC)
                CreateBlock("Hold short C", new Vector3(4f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldD)
                CreateBlock("Hold short D", new Vector3(4f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Eastern Alpha hold bars (toward stands / runway 27 end) for denser taxi authenticity.
            var usedHoldE = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_e", new Vector3(22f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdE);
            var usedHoldF = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_f", new Vector3(22f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdF);
            if (holdE != null) holdE.name = "Hold short E";
            if (holdF != null) holdF.name = "Hold short F";
            if (!usedHoldE)
                CreateBlock("Hold short E", new Vector3(22f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldF)
                CreateBlock("Hold short F", new Vector3(22f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Mid-Alpha hold bars (between A1 west and eastern stand lead) for denser taxi authenticity.
            var usedHoldG = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_g", new Vector3(12f, 0.05f, 6.6f), Quaternion.identity, holdYellow, out var holdG);
            var usedHoldH = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "hold_short_h", new Vector3(12f, 0.05f, 7.1f), Quaternion.identity, holdYellow, out var holdH);
            if (holdG != null) holdG.name = "Hold short G";
            if (holdH != null) holdH.name = "Hold short H";
            if (!usedHoldG)
                CreateBlock("Hold short G", new Vector3(12f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            if (!usedHoldH)
                CreateBlock("Hold short H", new Vector3(12f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // West-mid Alpha hold bars (between A1 exit and mid-Alpha) for denser taxi authenticity.
            CreateBlock("Hold short I", new Vector3(-2f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            CreateBlock("Hold short J", new Vector3(-2f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // East-mid Alpha hold bars near A2 exit for denser taxi authenticity.
            CreateBlock("Hold short K", new Vector3(28f, 0.05f, 6.6f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            CreateBlock("Hold short L", new Vector3(28f, 0.05f, 7.1f), new Vector3(3.6f, 0.03f, 0.2f), holdYellow);
            // Readable block digits for 09 / 27 (facing inbound traffic).
            PlaceRunwayDigit('0', new Vector3(-34.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('9', new Vector3(-32.6f, 0.04f, 0f), yaw: 90f);
            PlaceRunwayDigit('2', new Vector3(32.6f, 0.04f, 0f), yaw: -90f);
            PlaceRunwayDigit('7', new Vector3(34.6f, 0.04f, 0f), yaw: -90f);
            // Side stripes beside threshold bars — only when kit sides missed (avoid z-fight).
            if (!usedSideWL)
            {
                CreateBlock("Threshold stripe W L", new Vector3(-36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe W L2", new Vector3(-38.2f, 0.03f, -3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideWR)
            {
                CreateBlock("Threshold stripe W R", new Vector3(-36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe W R2", new Vector3(-38.2f, 0.03f, 3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideEL)
            {
                CreateBlock("Threshold stripe E L", new Vector3(36f, 0.03f, -3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe E L2", new Vector3(38.2f, 0.03f, -3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }
            if (!usedSideER)
            {
                CreateBlock("Threshold stripe E R", new Vector3(36f, 0.03f, 3.05f), new Vector3(2.2f, 0.02f, 0.45f), Color.white);
                CreateBlock("Threshold stripe E R2", new Vector3(38.2f, 0.03f, 3.05f), new Vector3(1.8f, 0.02f, 0.4f), Color.white);
            }

            // One aiming pair per end — six pairs were densify, not ICAO aiming points.
            foreach (var x in new[] { -12f, 12f })
            {
                var placedL = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "aiming_point_l", new Vector3(x, 0.035f, -1.55f), Quaternion.identity, Color.white, out _);
                var placedR = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "aiming_point_r", new Vector3(x, 0.035f, 1.55f), Quaternion.identity, Color.white, out _);
                if (!placedL)
                    CreateBlock($"Aiming point {x} L", new Vector3(x, 0.035f, -1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
                if (!placedR)
                    CreateBlock($"Aiming point {x} R", new Vector3(x, 0.035f, 1.55f), new Vector3(2.8f, 0.025f, 1.1f), Color.white);
            }

            // Two TDZ pairs per end instead of a 14-pair carpet.
            foreach (var x in new[] { -30f, -24f, 24f, 30f })
            {
                var placedL = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "tdz_mark_l", new Vector3(x, 0.03f, -1.4f), Quaternion.identity, Color.white, out _);
                var placedR = ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "tdz_mark_r", new Vector3(x, 0.03f, 1.4f), Quaternion.identity, Color.white, out _);
                if (!placedL)
                    CreateBlock($"TDZ {x} L", new Vector3(x, 0.03f, -1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
                if (!placedR)
                    CreateBlock($"TDZ {x} R", new Vector3(x, 0.03f, 1.4f), new Vector3(1.4f, 0.02f, 0.5f), Color.white);
            }
            // Stand bay numbers on the apron (readable from overview) — kit digit bars preferred.
            PlaceRunwayDigit('1', new Vector3(14.2f, 0.04f, 14f), yaw: 0f);
            PlaceRunwayDigit('2', new Vector3(14.2f, 0.04f, 24f), yaw: 0f);
            PlaceRunwayDigit('3', new Vector3(14.2f, 0.04f, 34f), yaw: 0f);

            var usedStandA = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_a", new Vector3(14f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            var usedStandB = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_b", new Vector3(22f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            if (!usedStandA)
                CreateBlock("Stand stop 1", new Vector3(14f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
            if (!usedStandB)
                CreateBlock("Stand stop 2", new Vector3(22f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));
            var usedStandC = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "stand_stop_c", new Vector3(30f, 0.04f, 16.2f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            if (!usedStandC)
                CreateBlock("Stand stop 3", new Vector3(30f, 0.04f, 16.2f), new Vector3(2.8f, 0.02f, 0.18f), new Color(0.95f, 0.85f, 0.2f));

            // Taxi markings are authored along local X (see generate-batch-b-surfaces).
            // Identity rotation keeps them on Taxiway A; Yaw 90 sent them across the apron
            // and gated off the greybox dashes. Centreline mesh is 20 m — place three copies.
            // Skip 1 m cube densify when any kit segment landed; greybox is one Alpha strip.
            var taxiPaint = new Color(0.95f, 0.85f, 0.2f);
            var usedTaxiFarWest = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(-8f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            var usedTaxiWest = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(8f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            var usedTaxiEast = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_centreline", new Vector3(28f, 0.035f, 9f), Quaternion.identity,
                taxiPaint, out _);
            if (!usedTaxiFarWest && !usedTaxiWest && !usedTaxiEast)
            {
                CreateBlock("Taxi centre Alpha", new Vector3(8f, 0.035f, 9f),
                    new Vector3(60f, 0.02f, 0.11f), taxiPaint);
            }

            CreatePaintStrip("Taxi exit centre A1",
                new Vector3(AirportLayout.DepartureEntryX, 0.035f, 4.5f),
                new Vector3(AirportLayout.AlphaJunctionX, 0.035f, AirportLayout.TaxiwayAlphaZ - 0.66f), 0.12f, taxiPaint);
            CreatePaintStrip("Taxi exit centre B1",
                new Vector3(AirportLayout.ArrivalExitX, 0.035f, 4.5f),
                new Vector3(AirportLayout.ArrivalExitX, 0.035f, AirportLayout.TaxiwayBravoZ - 0.66f), 0.12f, taxiPaint);
            CreatePaintStrip("Taxi exit centre A2",
                new Vector3(AirportLayout.TaxiwayEastX, 0.035f, AirportLayout.TaxiwayAlphaZ - 0.66f),
                new Vector3(AirportLayout.TaxiwayEastX + 12f, 0.035f, 4.5f), 0.12f, taxiPaint);

            // Edges: mesh already carries ±1.85f Z offset — place at taxi centre, identity yaw.
            // Two copies match the dual centreline coverage along Taxiway A.
            var usedTaxiEdgeN = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_n", new Vector3(8f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            var usedTaxiEdgeS = ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_s", new Vector3(8f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_n", new Vector3(28f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            ArtGltfLoader.TryPlaceNamedMesh(
                kit, "taxi_edge_s", new Vector3(28f, 0.035f, 9f), Quaternion.identity, Color.white, out _);
            if (!usedTaxiEdgeN)
            {
                CreateBlock("Taxi edge N", new Vector3(8f, 0.035f, 10.85f),
                    new Vector3(56f, 0.02f, 0.12f), Color.white);
            }

            if (!usedTaxiEdgeS)
            {
                CreateBlock("Taxi edge S", new Vector3(8f, 0.035f, 7.15f),
                    new Vector3(56f, 0.02f, 0.12f), Color.white);
            }
            // Apron lead-in chevrons from taxi to stand lead — kit chevrons when present.
            for (var i = 0; i < 3; i++)
            {
                var z = 11.0f + i * 0.7f;
                var pos = new Vector3(13.6f + i * 0.35f, 0.04f, z);
                var mesh = i % 2 == 0 ? "chevron_lead_a" : "chevron_lead_b";
                if (i == 2) mesh = "chevron_lead_c";
                if (!ArtGltfLoader.TryPlaceNamedMesh(
                        kit, mesh, pos, Quaternion.Euler(0f, 25f, 0f),
                        new Color(0.95f, 0.85f, 0.2f), out _))
                {
                    CreateBlock($"Apron chevron {i}", pos, new Vector3(1.0f, 0.02f, 0.15f),
                        new Color(0.95f, 0.85f, 0.2f));
                }
            }
            // Second lead path toward Stand 2 / 3 for denser apron authenticity.
            for (var i = 0; i < 2; i++)
            {
                var z = 11.2f + i * 0.85f;
                var pos = new Vector3(21.5f + i * 0.4f, 0.04f, z);
                if (!ArtGltfLoader.TryPlaceNamedMesh(
                        kit, i % 2 == 0 ? "chevron_lead_a" : "chevron_lead_b", pos,
                        Quaternion.Euler(0f, 20f, 0f), new Color(0.95f, 0.85f, 0.2f), out _))
                {
                    CreateBlock($"Apron chevron east {i}", pos, new Vector3(0.9f, 0.02f, 0.14f),
                        new Color(0.95f, 0.85f, 0.2f));
                }
            }

            // Taxi direction arrows on Taxiway A (west / mid / east + one apron lead).
            PlaceTaxiArrow(kit, new Vector3(-12f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(8f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(18f, 0.04f, 9f), 90f);
            PlaceTaxiArrow(kit, new Vector3(14f, 0.04f, 12.5f), 0f);
            // Mid-Alpha edge dashes flanking hold bars G/H.
            CreateBlock("Alpha edge mid N", new Vector3(12f, 0.045f, 10.85f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha edge mid S", new Vector3(12f, 0.045f, 7.35f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            // East-mid Alpha edge cues flanking hold bars K/L near A2.
            CreateBlock("Alpha edge east N", new Vector3(28f, 0.045f, 10.85f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha edge east S", new Vector3(28f, 0.045f, 7.35f), new Vector3(2.4f, 0.02f, 0.12f), new Color(1f, 0.92f, 0.2f));
            CreateBlock("Alpha centre mid", new Vector3(12f, 0.042f, 8.85f), new Vector3(1.8f, 0.018f, 0.1f), new Color(1f, 0.92f, 0.2f));
            // Apron entry arrows from densified kit when present.
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_a", new Vector3(12f, 0.04f, 11.5f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_arrow_b", new Vector3(16f, 0.04f, 12.8f), Quaternion.identity,
                new Color(0.95f, 0.85f, 0.2f), out _);
            // Bay-separation joints between Stand 1/2 and 2/3 for denser apron authenticity.
            CreateBlock("Apron joint 1-2", new Vector3(18f, 0.045f, 15.5f), new Vector3(0.12f, 0.02f, 5.2f), new Color(0.55f, 0.56f, 0.57f));
            CreateBlock("Apron joint 2-3", new Vector3(26f, 0.045f, 15.5f), new Vector3(0.12f, 0.02f, 5.2f), new Color(0.55f, 0.56f, 0.57f));
            CreateBlock("Apron joint lead", new Vector3(22f, 0.045f, 12.2f), new Vector3(8.5f, 0.018f, 0.1f), new Color(0.55f, 0.56f, 0.57f));

            // Hold-short across the A1 fillet (path (-24,0)→(-12,9)), not beside it.
            var holdPos = new Vector3(-18f, 0.05f, 4.5f);
            var holdYaw = Mathf.Atan2(12f, 9f) * Mathf.Rad2Deg + 90f;
            var holdRot = Quaternion.Euler(0f, holdYaw, 0f);
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "hold_short_e", holdPos, holdRot, holdYellow, out _))
            {
                var bar = CreateBlock("Hold short A1", holdPos, new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                bar.transform.rotation = holdRot;
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "hold_short_f", holdPos + holdRot * new Vector3(0f, 0f, 0.45f), holdRot, holdYellow, out _))
            {
                var bar2 = CreateBlock("Hold short A1 b", holdPos + holdRot * new Vector3(0f, 0f, 0.45f),
                    new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                bar2.transform.rotation = holdRot;
            }

            // Hold-short across the A2 eastern fillet (path (28,0)→(16,9)).
            var holdPosA2 = new Vector3(22f, 0.05f, 4.5f);
            var holdYawA2 = Mathf.Atan2(-12f, 9f) * Mathf.Rad2Deg + 90f;
            var holdRotA2 = Quaternion.Euler(0f, holdYawA2, 0f);
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "hold_short_c", holdPosA2, holdRotA2, holdYellow, out _))
            {
                var barA2 = CreateBlock("Hold short A2", holdPosA2, new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                barA2.transform.rotation = holdRotA2;
            }

            if (!ArtGltfLoader.TryPlaceNamedMesh(
                    kit, "hold_short_d", holdPosA2 + holdRotA2 * new Vector3(0f, 0f, 0.45f), holdRotA2, holdYellow, out _))
            {
                var barA2b = CreateBlock("Hold short A2 b", holdPosA2 + holdRotA2 * new Vector3(0f, 0f, 0.45f),
                    new Vector3(3.4f, 0.03f, 0.22f), holdYellow);
                barA2b.transform.rotation = holdRotA2;
            }

            // Stand lead-in dashes — skip when markings kit already placed stand stops
            // (otherwise landing/follow cameras see a carpet of yellow cubes).
            if (FindBuilt("stand_stop_a") == null
                && FindBuilt("stand_stop_b") == null
                && FindBuilt("stand_stop_c") == null)
            {
                foreach (var standX in new[] { 14f, 22f, 30f })
                {
                    CreateBlock($"Stand lead {standX}", new Vector3(standX, 0.04f, 13.7f),
                        new Vector3(0.12f, 0.02f, 5.1f), new Color(0.95f, 0.85f, 0.2f));
                }
            }
        }

        /// <summary>
        /// Decision 0025 item 3 — block runway digits readable from overview.
        /// Prefer WLD kit digit bars when present; greybox segments remain the fallback.
        /// Local +Z is digit height; yaw rotates onto the runway axis.
        /// </summary>
        private static void PlaceRunwayDigit(char digit, Vector3 centre, float yaw)
        {
            const string kit = "Models/Props/mdl_airfield_markings_kit_v01.gltf";
            var root = new GameObject($"Runway digit {digit}").transform;
            root.position = centre;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            void Seg(string name, float x, float z, float sx, float sz)
            {
                // Prefer kit digit bars for micro-relief; scale locally to match segment size.
                string mesh;
                if (name.Contains("serif", StringComparison.Ordinal))
                    mesh = "digit_serif";
                else if (sx > sz * 1.2f)
                    mesh = name.Contains("mid", StringComparison.Ordinal) ? "digit_bar_h_short" : "digit_bar_h";
                else
                    mesh = "digit_bar_v";

                if (ArtGltfLoader.TryPlaceNamedMesh(kit, mesh, new Vector3(x, 0f, z), Quaternion.identity, Color.white, out var part))
                {
                    part.SetParent(root, false);
                    part.localPosition = new Vector3(x, 0f, z);
                    part.localScale = new Vector3(
                        Mathf.Max(0.35f, sx / 1.1f),
                        1f,
                        Mathf.Max(0.35f, sz / 1.9f));
                    return;
                }

                ParentBlock(root, name, new Vector3(x, 0f, z), new Vector3(sx, 0.03f, sz), Color.white);
            }

            switch (digit)
            {
                case '0':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("left", -0.55f, 0f, 0.28f, 1.9f);
                    Seg("right", 0.55f, 0f, 0.28f, 1.9f);
                    break;
                case '2':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0f, 1.1f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("ur", 0.55f, 0.5f, 0.28f, 0.9f);
                    Seg("ll", -0.55f, -0.5f, 0.28f, 0.9f);
                    break;
                case '7':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("stem", 0.35f, -0.1f, 0.28f, 1.9f);
                    break;
                case '9':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0.1f, 1.1f, 0.28f);
                    Seg("ul", -0.55f, 0.55f, 0.28f, 0.85f);
                    Seg("ur", 0.55f, 0.55f, 0.28f, 0.85f);
                    Seg("stem", 0.55f, -0.45f, 0.28f, 1.0f);
                    break;
                case '1':
                    Seg("stem", 0f, 0f, 0.32f, 1.9f);
                    Seg("base", 0f, -0.95f, 0.85f, 0.28f);
                    Seg("serif", -0.28f, 0.7f, 0.45f, 0.28f);
                    break;
                case '3':
                    Seg("top", 0f, 0.95f, 1.1f, 0.28f);
                    Seg("mid", 0f, 0f, 1.0f, 0.28f);
                    Seg("bot", 0f, -0.95f, 1.1f, 0.28f);
                    Seg("ur", 0.55f, 0.5f, 0.28f, 0.9f);
                    Seg("lr", 0.55f, -0.5f, 0.28f, 0.9f);
                    break;
            }
        }

        /// <summary>
        /// Decision 0025 items 1+3 — workbench / shelves / drums so the open hangar
        /// bay reads occupied instead of an empty shell.
        /// </summary>
        private static void BuildHangarBayInterior()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_hangar_bay_props_v01", out var props))
            {
                props.name = "Hangar bay props";
                props.position = new Vector3(-20f, 0f, 18.2f);
                props.rotation = Quaternion.Euler(0f, 8f, 0f);
                return;
            }

            var root = new GameObject("Hangar bay props").transform;
            root.position = new Vector3(-20f, 0f, 18.2f);
            ParentBlock(root, "Workbench top", new Vector3(0f, 0.85f, 0f), new Vector3(2.4f, 0.12f, 0.9f), new Color(0.45f, 0.42f, 0.38f));
            ParentBlock(root, "Workbench vise", new Vector3(0.85f, 0.98f, 0.15f), new Vector3(0.35f, 0.28f, 0.35f), new Color(0.35f, 0.36f, 0.38f));
            ParentBlock(root, "Workbench leg L", new Vector3(-1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Workbench leg R", new Vector3(1f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.8f), new Color(0.25f, 0.25f, 0.28f));
            ParentBlock(root, "Shelf frame", new Vector3(-2.2f, 1.1f, -0.1f), new Vector3(0.9f, 1.8f, 0.45f), new Color(0.4f, 0.42f, 0.4f));
            ParentBlock(root, "Shelf board mid", new Vector3(-2.2f, 1.0f, -0.1f), new Vector3(0.85f, 0.08f, 0.4f), new Color(0.5f, 0.45f, 0.35f));
            ParentBlock(root, "Oil drum", new Vector3(-1.6f, 0.55f, 0.9f), new Vector3(0.55f, 1.1f, 0.55f), new Color(0.85f, 0.55f, 0.18f));
            ParentBlock(root, "Oil drum B", new Vector3(-0.9f, 0.45f, 1.1f), new Vector3(0.45f, 0.9f, 0.45f), new Color(0.75f, 0.45f, 0.15f));
            ParentBlock(root, "Tool cart body", new Vector3(1.8f, 0.55f, 0.6f), new Vector3(0.9f, 0.7f, 0.7f), new Color(0.35f, 0.45f, 0.55f));
            ParentBlock(root, "Crate stack", new Vector3(2.3f, 0.45f, -0.5f), new Vector3(0.7f, 0.9f, 0.55f), new Color(0.55f, 0.4f, 0.22f));
            ParentBlock(root, "Tire rack", new Vector3(-0.2f, 0.7f, -1.0f), new Vector3(1.4f, 1.2f, 0.35f), new Color(0.3f, 0.32f, 0.34f));
            ParentBlock(root, "Fire extinguisher", new Vector3(0.9f, 0.55f, -0.9f), new Vector3(0.22f, 0.7f, 0.22f), new Color(0.85f, 0.15f, 0.12f));
            ParentBlock(root, "Parts bin", new Vector3(0.4f, 0.35f, 0.7f), new Vector3(0.55f, 0.4f, 0.4f), new Color(0.55f, 0.55f, 0.2f));
        }

        private static void PlaceSignBoard(string kit, Vector3 position, float yawDegrees)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("sign_post", new Color(0.35f, 0.36f, 0.38f)),
                        ("sign_face", new Color(0.95f, 0.95f, 0.92f)),
                        ("sign_cap", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_brace", new Color(0.4f, 0.42f, 0.44f)),
                        ("sign_reflector", new Color(0.85f, 0.88f, 0.9f)),
                        ("sign_base", new Color(0.3f, 0.32f, 0.34f)),
                        ("sign_glyph_bar", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_glyph_bar_b", new Color(0.12f, 0.35f, 0.55f)),
                        ("sign_glyph_dot", new Color(0.12f, 0.35f, 0.55f))
                    },
                    position, rot, "Airside sign", out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "sign_board", position, rot, new Color(0.12f, 0.35f, 0.55f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_airside_sign_v01", out var prefabRoot))
            {
                prefabRoot.name = "Airside sign";
                prefabRoot.position = position;
                prefabRoot.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                return;
            }

            var root = new GameObject("Airside sign").transform;
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            ParentBlock(root, "Airside sign post", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.0f, 0.12f), new Color(0.35f, 0.36f, 0.38f));
            ParentBlock(root, "Airside sign face", new Vector3(0.08f, 1.35f, 0f), new Vector3(0.04f, 0.9f, 1.1f), new Color(0.95f, 0.95f, 0.92f));
            ParentBlock(root, "Airside sign back", new Vector3(0f, 1.35f, 0f), new Vector3(0.12f, 1.0f, 1.2f), new Color(0.12f, 0.35f, 0.55f));
        }

        private Vector3 RunwayPosition(CommercialFlight flight, Vector3 position)
        {
            if (FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
                && aircraft.AssignedRunway != RunwayDirection.Runway05)
            {
                RunwayFrame.ToWorld(aircraft.AssignedRunway, position.x, position.y, position.z,
                    out var x, out var y, out var z);
                return new Vector3(x, y, z);
            }
            return position;
        }

        private static Material CreateSharedSurfaceMaterial(Color color, string artTextureRelativePath = null, Vector2? textureTiling = null)
        {
            var kind = AirsideMaterialLibrary.InferFromTexturePath(artTextureRelativePath);
            if (kind == AirsideMaterialLibrary.SurfaceKind.Default)
                kind = InferSurfaceKindFromColor(color);
            var albedo = TryLoadArtTexture(artTextureRelativePath);
            return AirsideMaterialLibrary.CreateShared(color, kind, albedo, textureTiling);
        }

        private static AirsideMaterialLibrary.SurfaceKind InferSurfaceKindFromColor(Color color)
        {
            // Heuristic for untextured primitives (cars, props, glow quads, painted lines).
            // Translucent rain/smoke/mist must NOT become Glass — MAT-001 mat_glass is a pane
            // material and reads as bright vertical shafts on thin Cube droplets.
            if (color.a < 0.99f)
            {
                // Only blue-tinted translucency is glazing (every window colour here is). The old
                // rule sent anything but pale sub-0.55-alpha mist to the glass pane material, so
                // tyre smoke (alpha exactly 0.55), dark skid marks and orange engine heat were
                // drawn glossy and mirror-like.
                // Pale blue-grey (rain, spray) stays default so droplets are not pane-bright.
                var glazing = color.r < 0.5f && color.b > color.r + 0.1f && color.b >= color.g;
                return glazing
                    ? AirsideMaterialLibrary.SurfaceKind.Glass
                    : AirsideMaterialLibrary.SurfaceKind.Default;
            }
            // Near-white / cream → painted markings, not aircraft skin (MAT-001 / 0025 item 4).
            if (color.r > 0.85f && color.g > 0.85f && color.b > 0.85f)
                return AirsideMaterialLibrary.SurfaceKind.PaintedLine;
            // Safety-yellow / taxi paint.
            if (color.r > 0.85f && color.g > 0.75f && color.b < 0.45f)
                return AirsideMaterialLibrary.SurfaceKind.PaintedLine;
            if (color.b > color.r + 0.15f && color.b > color.g + 0.05f)
                return AirsideMaterialLibrary.SurfaceKind.Water;
            return AirsideMaterialLibrary.SurfaceKind.PaintedMetal;
        }
    }
}
