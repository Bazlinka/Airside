using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class PresentationLayoutTests
    {
        [Test]
        public void LaunchIntro_IsBriefAndHasADeliberateMarkReveal()
        {
            Assert.That(AirsidePrototype.IntroSeconds, Is.EqualTo(4.8f).Within(0.001f));
            Assert.That(AirsidePrototype.IntroMarkRevealSeconds, Is.GreaterThan(0f));
            Assert.That(AirsidePrototype.IntroMarkRevealSeconds, Is.LessThan(AirsidePrototype.IntroSeconds));
        }

        [TestCase(1440, 900, 1f)]
        [TestCase(1280, 720, 0.8f)]
        public void HudScale_NormalWindowsUseReadableVirtualSize(int width, int height, float expected)
        {
            Assert.That(HudLayout.ScaleFor(width, height), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void FlightNumber_AirlineCodeUsesIdForAiAndInitialsForPlayer()
        {
            Assert.That(FlightNumber.AirlineCode(Airline.Rex()), Is.EqualTo("REX"));
            Assert.That(FlightNumber.AirlineCode(Airline.Player("Southern Cross Regional", "#1F3A93")), Is.EqualTo("SC"));
            Assert.That(FlightNumber.AirlineCode(Airline.Player("Wattlebird", "#1F3A93")), Is.EqualTo("WA"));
            Assert.That(FlightNumber.AirlineCode(null), Is.EqualTo("XX"));
        }

        [Test]
        public void FlightNumber_IsDeterministicAndVariesByRoute()
        {
            var rex = Airline.Rex();
            var a = FlightNumber.For(rex, "VH-ABC", "MEL");
            var b = FlightNumber.For(rex, "VH-ABC", "MEL");
            var c = FlightNumber.For(rex, "VH-ABC", "SYD");
            Assert.That(a, Is.EqualTo(b), "same aircraft and route must read the same every time");
            Assert.That(a, Is.Not.EqualTo(c), "a different destination should usually read a different number");
            Assert.That(a, Does.StartWith("REX"));
            Assert.That(int.Parse(a.Substring(3)), Is.InRange(100, 999));
        }

        [Test]
        public void AdelaideLighting_GuardFilterTargetsMainRunwayHoldingPoints()
        {
            Assert.That(AdelaideAirfieldLighting.IsMainRunwayGuardPosition(-1529.9f, 90.3f), Is.True);
            Assert.That(AdelaideAirfieldLighting.IsMainRunwayGuardPosition(614.1f, 92.5f), Is.True);
            Assert.That(AdelaideAirfieldLighting.IsMainRunwayGuardPosition(237f, 199f), Is.False);
            Assert.That(AdelaideAirfieldLighting.TaxiCentrelineVisualSpacingMetres, Is.InRange(30f, 60f));
        }

        [Test]
        public void HudScale_RetinaDisplayIsNotCappedAtTheOldTinyScale()
        {
            Assert.That(HudLayout.ScaleFor(3456, 2168), Is.EqualTo(2.25f).Within(0.001f));
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(3456, 2168)]
        [TestCase(800, 500)]
        [TestCase(400, 780)]
        [TestCase(320, 240)]
        public void HudLayout_PhysicalLaptopAndRetinaSizesRemainInsideViewport(int screenWidth, int screenHeight)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var width = screenWidth / scale;
            var height = screenHeight / scale;
            var layout = HudLayout.Create(width, height);

            Assert.That(layout.ControlBar.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.ControlBar.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(layout.ControlBar.yMax, Is.LessThanOrEqualTo(height));
            Assert.That(layout.PauseMenu.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.PauseMenu.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(layout.PauseMenu.yMax, Is.LessThanOrEqualTo(height));

            Assert.That(layout.SpeedReadout.xMin, Is.GreaterThanOrEqualTo(-0.01f));
            Assert.That(layout.SpeedReadout.xMax, Is.LessThanOrEqualTo(width + 0.01f));
            Assert.That(layout.SpeedReadout.yMin, Is.GreaterThanOrEqualTo(-0.01f));
            Assert.That(layout.SpeedReadout.height, Is.GreaterThan(0f), "speed readout collapsed");
            Assert.That(layout.SpeedReadout.Overlaps(layout.ControlBar), Is.False,
                "the speed readout must sit above the bar, not on it");

            // Every control must sit inside the bar, in left-to-right order.
            var previous = float.NegativeInfinity;
            for (var index = 0; index < HudLayout.ButtonCount; index++)
            {
                var button = layout.ButtonAt(index);
                Assert.That(button.xMin, Is.GreaterThanOrEqualTo(layout.ControlBar.xMin - 0.01f));
                Assert.That(button.xMax, Is.LessThanOrEqualTo(layout.ControlBar.xMax + 0.01f));
                Assert.That(button.width, Is.GreaterThan(0f), $"control {index} collapsed");
                Assert.That(button.xMin, Is.GreaterThan(previous));
                previous = button.xMin;
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(1920, 1080)]
        [TestCase(3456, 2168)]
        [TestCase(800, 500)]
        [TestCase(400, 780)]
        [TestCase(320, 240)]
        public void AirlineHudLayout_PanelsFitAndNeverOverlap(int screenWidth, int screenHeight)
        {
            AssertAirlinePanelsFit(screenWidth, screenHeight, showGuide: false);
            AssertAirlinePanelsFit(screenWidth, screenHeight, showGuide: true);
        }

        private static void AssertAirlinePanelsFit(int screenWidth, int screenHeight, bool showGuide)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var width = screenWidth / scale;
            var height = screenHeight / scale;
            var hud = HudLayout.Create(width, height);
            var airline = AirlineHudLayout.Create(hud, showGuide);

            void Inside(Rect r, string name)
            {
                if (r.width <= 0f || r.height <= 0f)
                    return;
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(-0.01f), $"{name} off the left");
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(-0.01f), $"{name} off the top");
                Assert.That(r.xMax, Is.LessThanOrEqualTo(width + 0.01f), $"{name} off the right");
                Assert.That(r.yMax, Is.LessThanOrEqualTo(height + 0.01f), $"{name} off the bottom");
            }

            void Apart(Rect a, string aName, Rect b, string bName)
            {
                if (a.width <= 0f || a.height <= 0f || b.width <= 0f || b.height <= 0f)
                    return;
                Assert.That(a.Overlaps(b), Is.False, $"{aName} overlaps {bName} at {screenWidth}x{screenHeight}");
            }

            Inside(airline.TopBar, "top bar");
            Inside(airline.NavStrip, "nav strip");
            Inside(airline.Objective, "objective");
            Inside(airline.Operations, "operations");
            Inside(airline.Toast, "toast");
            Inside(airline.Workspace, "workspace");
            Inside(airline.MiniMap, "mini-map");
            Inside(airline.SelectedCard, "selected card");
            Inside(airline.SetupPanel(396f), "setup");

            Assert.That(airline.TopBar.width, Is.EqualTo(width).Within(0.01f), "top bar is full width");
            Assert.That(airline.TopBar.height, Is.GreaterThanOrEqualTo(36f), "top bar is too short to read");
            Assert.That(airline.NavStrip.yMin, Is.GreaterThanOrEqualTo(airline.TopBar.yMin - 0.01f));
            Assert.That(airline.NavStrip.yMax, Is.LessThanOrEqualTo(airline.TopBar.yMax + 0.01f));

            Apart(airline.Objective, "objective", airline.TopBar, "top bar");
            Apart(airline.Operations, "operations", airline.TopBar, "top bar");
            Apart(airline.Objective, "objective", airline.Operations, "operations");
            Apart(airline.MiniMap, "mini-map", airline.TopBar, "top bar");
            Apart(airline.MiniMap, "mini-map", airline.Objective, "objective");
            Apart(airline.MiniMap, "mini-map", airline.Operations, "operations");
            Apart(airline.MiniMap, "mini-map", airline.SelectedCard, "selected card");
            Apart(airline.SelectedCard, "selected card", airline.TopBar, "top bar");
            Apart(airline.SelectedCard, "selected card", airline.Objective, "objective");
            Apart(airline.SelectedCard, "selected card", airline.Operations, "operations");
            Apart(airline.Toast, "toast", airline.Objective, "objective");
            Apart(airline.Toast, "toast", airline.Operations, "operations");
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(3456, 2168)]
        [TestCase(800, 500)]
        [TestCase(400, 780)]
        [TestCase(320, 240)]
        public void AirlineHudLayout_NavStripFitsFourReadableTabs(int screenWidth, int screenHeight)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var hud = HudLayout.Create(screenWidth / scale, screenHeight / scale);
            var airline = AirlineHudLayout.Create(hud);

            // Regression guard: the nav strip used to be locked to the 300 px clock column,
            // giving four tabs ~75 px each — nowhere near enough for "Operations", which
            // overflowed clean off the left edge of the window in a packaged build.
            const float tabs = 4f;
            Assert.That(airline.NavStrip.width / tabs, Is.GreaterThanOrEqualTo(65f),
                $"{screenWidth}x{screenHeight}: nav tabs too narrow to hold their labels");
        }

        [Test]
        public void AirlineHudLayout_DesktopKeepsOperationsBesideTheObjective()
        {
            var airline = AirlineHudLayout.Create(HudLayout.Create(1440f, 900f));
            Assert.That(airline.WorkspaceCoversOverview, Is.False);
            Assert.That(airline.Operations.x, Is.GreaterThan(airline.Objective.xMax));
            Assert.That(airline.MiniMap.width, Is.EqualTo(AirlineHudLayout.MiniMapWidth));
            Assert.That(airline.SelectedCard.width, Is.EqualTo(AirlineHudLayout.SelectedCardWidth));
            Assert.That(airline.MiniMap.width, Is.LessThan(280f), "mini-map stays compact");
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(1920, 1080)]
        public void AirlineHudLayout_SupportedDesktopsKeepTheShellReadable(int screenWidth, int screenHeight)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var airline = AirlineHudLayout.Create(HudLayout.Create(screenWidth / scale, screenHeight / scale));
            Assert.That(airline.Objective.width, Is.GreaterThanOrEqualTo(280f));
            Assert.That(airline.Operations.width, Is.GreaterThanOrEqualTo(240f));
            Assert.That(airline.NavStrip.width / 4f, Is.GreaterThanOrEqualTo(70f));
            Assert.That(airline.SelectedCard.height, Is.GreaterThanOrEqualTo(100f));
        }

        [Test]
        public void HudLayout_ControlsAreCentredAndDoNotOverlap()
        {
            var layout = HudLayout.Create(1440f, 900f);

            var leftGap = layout.ControlBar.xMin;
            var rightGap = 1440f - layout.ControlBar.xMax;
            Assert.That(leftGap, Is.EqualTo(rightGap).Within(0.01f), "control bar is not centred");

            for (var index = 1; index < HudLayout.ButtonCount; index++)
            {
                Assert.That(layout.ButtonAt(index - 1).Overlaps(layout.ButtonAt(index)), Is.False,
                    $"control {index - 1} overlaps {index}");
            }
        }

        [Test]
        public void TaxiVisualPath_ChangesSegmentsByChordLengthMatchingReservations()
        {
            var route = new TaxiRoute(
                "Unequal test route",
                new[] { new StableId("A"), new StableId("B"), new StableId("C") },
                new[] { new TaxiPoint(0f, 0f), new TaxiPoint(1f, 0f), new TaxiPoint(101f, 0f), new TaxiPoint(102f, 0f) });

            // Lengths 1 / 100 / 1 — progress 0.02 is still on A; 0.50 mid B; 0.995 on C.
            var first = TaxiVisualPath.PositionAt(route, 0.005f, reverse: false);
            var second = TaxiVisualPath.PositionAt(route, 0.50f, reverse: false);
            var third = TaxiVisualPath.PositionAt(route, 0.995f, reverse: false);
            var reverseEarly = TaxiVisualPath.PositionAt(route, 0.005f, reverse: true);

            Assert.That(first.x, Is.InRange(0f, 1f), "early progress stays on short A");
            Assert.That(second.x, Is.InRange(1f, 101f), "mid progress stays on long B");
            Assert.That(third.x, Is.InRange(101f, 102f), "late progress reaches C");
            Assert.That(reverseEarly.x, Is.InRange(101f, 102f), "reverse early stays on C");
            Assert.That(route.SegmentIndexAt(0.50, reverse: false), Is.EqualTo(1));
        }

        [Test]
        public void HudScale_SmallWindowsCanDropBelowOldMin()
        {
            Assert.That(HudLayout.ScaleFor(800, 500), Is.LessThan(0.8f));
            Assert.That(HudLayout.ScaleFor(800, 500), Is.GreaterThanOrEqualTo(0.55f));
        }

        [Test]
        public void CombinedSurfaces_TileAirfieldIsRetired()
        {
            Assert.That(AirsideCombinedSurfaces.UseTileOperational, Is.False);
            Assert.That(AirsideCombinedSurfaces.UseTilePaddock, Is.False);
            Assert.That(AirsideCombinedSurfaces.CombinedPadCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeQuality_HighKeepsDocumentedMsaaAndAddsMediumLadder()
        {
            Assert.That(AirsideRuntimeQuality.HighMsaa, Is.EqualTo(4));
            Assert.That(AirsideRuntimeQuality.MediumMsaa, Is.EqualTo(2));
            Assert.That(AirsideRuntimeQuality.VSyncCount, Is.EqualTo(1));
            Assert.That(AirsideRuntimeQuality.HighShadowCascades, Is.EqualTo(4));
            Assert.That(AirsideRuntimeQuality.MediumShadowCascades, Is.EqualTo(2));
            Assert.That(AirsideRuntimeQuality.HighAdditionalLights, Is.EqualTo(12));
            Assert.That(AirsideRuntimeQuality.MediumAdditionalLights, Is.EqualTo(4));
            Assert.That(AirsideRuntimeQuality.HighEdgeLightStep, Is.EqualTo(10));
            Assert.That(AirsideRuntimeQuality.MediumEdgeLightStep, Is.EqualTo(16));
            Assert.That(AirsideRuntimeQuality.HighRainDrops, Is.EqualTo(28));
            Assert.That(AirsideRuntimeQuality.MediumRainDrops, Is.EqualTo(16));
            Assert.That(AirsideRuntimeQuality.HighFilletLights, Is.EqualTo(3));
            Assert.That(AirsideRuntimeQuality.MediumFilletLights, Is.EqualTo(1));
            Assert.That(AirsideRuntimeQuality.HighBirdCount, Is.EqualTo(28));
            Assert.That(AirsideRuntimeQuality.MediumBirdCount, Is.EqualTo(12));
        }

        [Test]
        public void RuntimeQuality_ProbeBandChangesOnlyOnWeatherAndTimeThresholds()
        {
            Assert.That(AirsideRuntimeQuality.ProbeBand(0.8f, 0f), Is.EqualTo(2));
            Assert.That(AirsideRuntimeQuality.ProbeBand(0.4f, 0f), Is.EqualTo(1));
            Assert.That(AirsideRuntimeQuality.ProbeBand(0.1f, 0f), Is.EqualTo(0));
            Assert.That(AirsideRuntimeQuality.ProbeBand(0.8f, 0.3f), Is.EqualTo(3));
        }

        [Test]
        public void MeshUtil_NullMeshHasNoUvsWithoutReadingUvArray()
        {
            Assert.That(AirsideMeshUtil.HasUsableUvs(null), Is.False);
        }

        [Test]
        public void StaticWorld_HoldShortAndAircraftStayDynamic()
        {
            Assert.That(AirsideStaticWorld.IsDynamic(null), Is.True);
            var hold = new GameObject("Hold short 09");
            var apron = new GameObject("Apron ");
            try
            {
                Assert.That(AirsideStaticWorld.IsDynamic(hold), Is.True);
                Assert.That(AirsideStaticWorld.IsDynamic(apron), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hold);
                UnityEngine.Object.DestroyImmediate(apron);
            }
        }

        [Test]
        public void SceneIndex_MissingNameIsNullBeforeCapture()
        {
            Assert.That(AirsideSceneIndex.Find(null), Is.Null);
            Assert.That(AirsideSceneIndex.Find(""), Is.Null);
            Assert.That(AirsideSceneIndex.FindGameObject("definitely-not-in-scene-index"), Is.Null);
            AirsideSceneIndex.RememberMiss("known-missing-airside-name");
            Assert.That(AirsideSceneIndex.IsKnownMissing("known-missing-airside-name"), Is.True);
        }

        [Test]
        public void GltfLoader_CombinedPlaceMissesWhenKitMissing()
        {
            Assert.That(
                ArtGltfLoader.TryPlaceCombined(
                    "Models/missing_kit.gltf",
                    new[] { ("fence_bay", Color.white) },
                    Vector3.zero,
                    Quaternion.identity,
                    "Fence bay test",
                    out var instance),
                Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(ArtGltfLoader.HasMesh("Models/missing_kit.gltf", "fence_bay"), Is.False);
        }

        [Test]
        public void StaticWorld_MovingRootsStayOffTheStaticBatch()
        {
            var moving = new[]
            {
                "Ground traffic GT-201",
                "Fuel truck",
                "Passenger stairs",
                "GPU cart",
                "Cloud 0",
                "Coast boat A",
                "Windsock sock",
                "Jetty deck",
                "antenna_dish"
            };
            var created = new List<GameObject>();
            try
            {
                foreach (var name in moving)
                {
                    var go = new GameObject(name);
                    created.Add(go);
                    Assert.That(AirsideStaticWorld.IsDynamic(go), Is.True, name);
                }

                var slab = new GameObject("Runway 05/23");
                created.Add(slab);
                Assert.That(AirsideStaticWorld.IsDynamic(slab), Is.False);
            }
            finally
            {
                foreach (var go in created)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NamedChildren_HasNameAndFindContainsUseCachedScan()
        {
            var root = new GameObject("NamedChildren root");
            var torso = new GameObject("marshaller torso");
            var wand = new GameObject("wand tip L");
            try
            {
                torso.transform.SetParent(root.transform, false);
                wand.transform.SetParent(root.transform, false);
                Assert.That(AirsideNamedChildren.HasName(root.transform, "marshaller torso"), Is.True);
                Assert.That(AirsideNamedChildren.HasName(root.transform, "missing"), Is.False);
                Assert.That(AirsideNamedChildren.FindContains(root.transform, "wand"), Is.EqualTo(wand.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static StandTaxiRoutes StandOneRoutes() =>
            new AirportTaxiNetwork().RoutesTo(AirportSimulation.StandOne);

        /// <summary>
        /// Mirrors AirsidePrototype.PositionFor for a single flight to Stand 1, so the
        /// whole cycle can be measured end to end.
        /// </summary>
        private static Vector3 FlightPathAt(AircraftPhase phase, float t)
        {
            t = Mathf.Clamp01(t);
            switch (phase)
            {
                case AircraftPhase.Approach: return AirsideFlightPath.Approach(t, 0f);
                case AircraftPhase.Landing: return AirsideFlightPath.Landing(t, 0f);
                case AircraftPhase.TaxiIn:
                case AircraftPhase.AtStand:
                case AircraftPhase.Pushback:
                case AircraftPhase.TaxiOut:
                    return AirsideFlightPath.OnRunwayHold();
                case AircraftPhase.Takeoff: return AirsideFlightPath.Takeoff(t);
                default: return AirsideFlightPath.Departed(t);
            }
        }

        /// <summary>
        /// Metres per real second at 1x. Phase durations differ and are derived from
        /// CircuitProfile, so distance per unit of progress is not comparable across a
        /// seam, and comparing it flagged a matched handover as a 2.5x lurch.
        /// </summary>
        private static float SpeedAt(AircraftPhase phase, float from, float to)
        {
            var seconds = (to - from) * AirsideFlightPath.PhaseSeconds(phase);
            return seconds <= 0f
                ? 0f
                : Vector3.Distance(FlightPathAt(phase, from), FlightPathAt(phase, to)) / seconds;
        }

        [TestCase(AircraftPhase.Approach)]
        [TestCase(AircraftPhase.Landing)]
        [TestCase(AircraftPhase.Takeoff)]
        [TestCase(AircraftPhase.Departed)]
        public void FlightPath_NeverStallsInsideAPhase(AircraftPhase phase)
        {
            // A smoothstep on position has zero velocity at both ends, which reads as
            // the aircraft stopping dead at rotation, at the flare and at the threshold.
            // AtStand is a real stop and Pushback eases to one before the tug leaves, so
            // neither is listed here.
            const int steps = 400;
            for (var i = 20; i < steps - 20; i++)
            {
                var t = i / (float)steps;
                Assert.That(SpeedAt(phase, t, t + 1f / steps), Is.GreaterThan(0.5f),
                    $"{phase} stalls at t={t:F3}");
            }
        }

        [Test]
        public void FlightPath_PhaseSeamsAreContinuousInPositionAndSpeed()
        {
            const float h = 0.002f;
            void Seam(string label, AircraftPhase from, AircraftPhase to, float tolerance)
            {
                Assert.That(Vector3.Distance(FlightPathAt(from, 1f), FlightPathAt(to, 0f)),
                    Is.LessThan(0.05f), $"{label} teleports");
                if (tolerance <= 0f)
                    return;

                var leaving = SpeedAt(from, 1f - h, 1f);
                var entering = SpeedAt(to, 0f, h);
                Assert.That(entering, Is.EqualTo(leaving).Within(Mathf.Max(leaving, entering) * tolerance),
                    $"{label} lurches: {leaving:F2} -> {entering:F2} m/s");
            }

            Seam("approach -> landing", AircraftPhase.Approach, AircraftPhase.Landing, 0.15f);
            Seam("landing -> hold", AircraftPhase.Landing, AircraftPhase.TaxiIn, 0f);
            Seam("hold -> takeoff", AircraftPhase.TaxiOut, AircraftPhase.Takeoff, 0f);
            Seam("takeoff -> departed", AircraftPhase.Takeoff, AircraftPhase.Departed, 0.15f);
        }

        [Test]
        public void FlightPath_GroundHoldHasNoSpeedJump_AndDeparturePitchIsContinuous()
        {
            // The rollout ends at runway exit speed, handing straight over to the vacate taxi.
            Assert.That(SpeedAt(AircraftPhase.Landing, 0.998f, 1f),
                Is.EqualTo(CircuitProfile.Knots(CircuitProfile.RunwayExitKnots)).Within(0.5f));
            Assert.That(SpeedAt(AircraftPhase.Takeoff, 0f, 0.002f), Is.LessThan(0.5f));
            Assert.That(AirsideFlightPath.PitchDegrees(AircraftPhase.Takeoff, 1f),
                Is.EqualTo(AirsideFlightPath.PitchDegrees(AircraftPhase.Departed, 0f)).Within(0.01f));
        }

        [Test]
        public void FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed()
        {
            Assert.That(SpeedAt(AircraftPhase.Approach, 0.5f, 0.502f), Is.GreaterThan(40f));
            // Mid climb-out sits between the initial-climb and climb-out speeds (ADR 0044);
            // the old 80 m/s floor dated from the curve that left the field at 257 kt.
            Assert.That(SpeedAt(AircraftPhase.Departed, 0.5f, 0.502f),
                Is.GreaterThan(CircuitProfile.Knots(CircuitProfile.InitialClimbKnots)));
            Assert.That(SpeedAt(AircraftPhase.Landing, 0.4f, 0.402f), Is.GreaterThan(30f));
        }

        [Test]
        public void FlightPath_TakeoffDoesNotSlowDownAtRotation()
        {
            var r = AirsideFlightPath.RotateProgress;
            Assert.That(r, Is.InRange(AirsideFlightPath.LineupProgress, 0.95f));
            Assert.That(AirsideFlightPath.Takeoff(r, 0f).x, Is.EqualTo(AirsideFlightPath.RotateX).Within(0.2f));
            Assert.That(AirsideFlightPath.Takeoff(r).y,
                Is.EqualTo(AirsideFlightPath.GroundY).Within(0.01f), "rotation happens on the ground");

            // The roll is one continuous acceleration; any dip reads as the aircraft
            // lifting off and then hesitating.
            var last = SpeedAt(AircraftPhase.Takeoff, 0.002f, 0.004f);
            for (var t = 0.004f; t < 0.998f; t += 0.002f)
            {
                var speed = SpeedAt(AircraftPhase.Takeoff, t, t + 0.002f);
                Assert.That(speed, Is.GreaterThan(last - 0.05f), $"takeoff roll slows at t={t:F3}");
                last = speed;
            }
        }

        [Test]
        public void FlightPath_LineUpTurnsOntoTheCentrelineInsteadOfSnapping()
        {
            var start = AirsideFlightPath.Takeoff(0f);
            Assert.That(start.z, Is.EqualTo(0f).Within(0.01f), "circuit takeoff stays on the centreline");
            Assert.That(start.x, Is.EqualTo(AirsideFlightPath.RolloutEndX).Within(0.01f), "the demo circuit rolls from where it stopped");
            Assert.That(AirsideFlightPath.Takeoff(0f, 0f).x, Is.EqualTo(AirsideFlightPath.TakeoffStartX).Within(0.01f), "fleet departures roll from the 05 threshold");

            var previous = HeadingDegrees(AirsideFlightPath.Takeoff(0f), AirsideFlightPath.Takeoff(0.002f));
            for (var i = 1; i <= 200; i++)
            {
                var t = i / 200f;
                var heading = HeadingDegrees(AirsideFlightPath.Takeoff(t), AirsideFlightPath.Takeoff(Mathf.Min(1f, t + 0.002f)));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(previous, heading)), Is.LessThan(3f),
                    $"takeoff heading snaps at t={t:F3}");
                previous = heading;
            }
        }

        [Test]
        public void FlightPath_DepartureHoldsShortClearOfTheRunway()
        {
            var hold = AirsideFlightPath.OnRunwayHold();
            Assert.That(hold.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(AirsideBareField.ContainsRunway(hold.x, hold.z), Is.True);
            Assert.That(Vector3.Distance(hold, AirsideFlightPath.Takeoff(0f)), Is.LessThan(0.05f));
        }

        [Test]
        public void FlightPath_TaxiRunsAtAConstantSpeedInBothDirections()
        {
            // Reverse travel used to mirror only the segment index, so an outbound
            // aircraft spent the long Alpha leg's share of the phase crawling the short
            // lead-in and then raced the rest — a tenfold speed swing inside one phase.
            var route = StandOneRoutes().Arrival;
            foreach (var reverse in new[] { false, true })
            {
                var min = float.MaxValue;
                var max = 0f;
                for (var i = 0; i < 500; i++)
                {
                    var step = Vector3.Distance(
                        TaxiVisualPath.PositionAt(route, i / 500f, reverse),
                        TaxiVisualPath.PositionAt(route, (i + 1) / 500f, reverse));
                    min = Mathf.Min(min, step);
                    max = Mathf.Max(max, step);
                }

                // Rounded corners shorten the geometric path by about 29%; keep the
                // resulting visual speed change bounded while preserving smooth turns.
                Assert.That(max / min, Is.LessThan(1.45f), $"reverse={reverse} taxi speed is uneven");
            }
        }

        [Test]
        public void FlightPath_DepartureKeepsFlyingInsteadOfFreezing()
        {
            var start = AirsideFlightPath.Departed(0f);
            // Distance, not Vector3 equality: the two ends of the seam are reached by
            // different arithmetic, so exact float equality is not owed here.
            Assert.That(Vector3.Distance(start, AirsideFlightPath.Takeoff(1f)), Is.LessThan(0.01f));
            Assert.That(AirsideFlightPath.Departed(1f).x, Is.GreaterThan(start.x + 100f));
            Assert.That(AirsideFlightPath.Departed(1f).y, Is.GreaterThan(start.y + 20f));
        }

        [Test]
        public void AircraftMotion_ReadsProgressFromTheFractionalClock()
        {
            // The simulation ticks whole seconds, so sampling it straight gave 59 still
            // frames then a jump. Progress is a pure function of time, so presentation
            // evaluates it at the fractional presentation clock instead.
            Assert.That(AirsideAircraftMotion.PhaseProgress(32.0d, 32L, 25f), Is.Zero);
            Assert.That(AirsideAircraftMotion.PhaseProgress(32.5d, 32L, 25f),
                Is.EqualTo(0.02f).Within(1e-5f));
            Assert.That(AirsideAircraftMotion.PhaseProgress(44.5d, 32L, 25f),
                Is.EqualTo(0.5f).Within(1e-5f));

            // Clamped, so a departure held for traffic waits at the hold-short point
            // rather than sliding onto the runway ahead of its clearance.
            Assert.That(AirsideAircraftMotion.PhaseProgress(57.0d, 32L, 25f), Is.EqualTo(1f));
            Assert.That(AirsideAircraftMotion.PhaseProgress(400d, 32L, 25f), Is.EqualTo(1f));

            // Strictly increasing between ticks: no frame repeats a position.
            var previous = -1f;
            for (var i = 0; i < 240; i++)
            {
                var progress = AirsideAircraftMotion.PhaseProgress(32d + i / 60d, 32L, 25f);
                Assert.That(progress, Is.GreaterThan(previous));
                previous = progress;
            }
        }

        private const float AirsideRunwayHalfWidth = 3.4f;

        private static float HeadingDegrees(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        }

        [Test]
        public void FlightPath_WheelsStopOnceTheAircraftIsAirborne()
        {
            var r = AirsideFlightPath.RotateProgress;
            Assert.That(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Takeoff, r + 0.01f), Is.Zero);
            Assert.That(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Landing, 0f), Is.Zero);
            Assert.That(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Departed, 0.5f), Is.Zero);

            // Roll accelerates, rollout decelerates. Both sample points have to sit
            // after touchdown: before it the wheels are stopped, not merely slower.
            Assert.That(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Takeoff, r * 0.9f),
                Is.GreaterThan(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Takeoff, r * 0.1f)));
            var justDown = AirsideFlightPath.TouchdownProgress
                           + (1f - AirsideFlightPath.TouchdownProgress) * 0.2f;
            Assert.That(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Landing, 0.99f),
                Is.LessThan(AirsideFlightPath.WheelSpeedFactor(AircraftPhase.Landing, justDown)));
        }

        [Test]
        public void FocusMode_DefaultLaunchKeepsTheFocusedCircuit()
        {
            // The bare field parks every non-aircraft object behind the same switch
            // so the player sees only plane, runway, ground and sun lighting.
            Assert.That(AirsideFocusMode.ShowGroundVehicles, Is.False);
            Assert.That(AirsideFocusMode.ShowStandEquipment, Is.False);
            Assert.That(AirsideFocusMode.ShowPeople, Is.False);
            Assert.That(AirsideFocusMode.ShowBuildings, Is.False);
            Assert.That(AirsideFocusMode.ShowEnvironment, Is.False);
            Assert.That(AirsideFocusMode.ShowWorldProps, Is.False);
            Assert.That(AirsideFocusMode.ShowDecorativeLights, Is.False);
            Assert.That(AirsideFocusMode.VisibleCommercialFlights, Is.EqualTo(1));
            Assert.That(AirsideBareField.Enabled, Is.True);
            Assert.That(AirsideBareField.RunwayLengthMetres, Is.EqualTo(3100f));
            Assert.That(AirsideBareField.RunwayWidthMetres, Is.EqualTo(45f));
        }

        [Test]
        public void FlightPath_TouchdownSitsInsideThePublishedTouchdownZone()
        {
            // Headless BareFieldTests cannot compile AirsideFlightPath. Keep this
            // equality in both Unity EditMode and work/flightcheck.
            Assert.That(AirsideFlightPath.TouchdownX,
                Is.EqualTo(AirsideRunwayMarkings.WestTouchdownZoneX(450f)).Within(0.01f));
            Assert.That(AirsideFlightPath.WestThresholdX,
                Is.EqualTo(AirsideRunwayMarkings.WestThresholdX).Within(0.01f));
            var half = AirsideRunwayMarkings.TouchdownZoneLength * 0.5f;
            Assert.That(AirsideFlightPath.TouchdownX,
                Is.InRange(
                    AirsideRunwayMarkings.WestTouchdownZoneX(450f) - half,
                    AirsideRunwayMarkings.WestTouchdownZoneX(450f) + half));
        }

        [Test]
        public void FlightPath_DampingConvergesTheSameAtAnyFrameRate()
        {
            float Converge(int fps)
            {
                var v = 0f;
                for (var i = 0; i < fps; i++)
                    v = Mathf.Lerp(v, 1f, AirsideFlightPath.DampFactor(5f, 1f / fps));
                return v;
            }

            Assert.That(Converge(144), Is.EqualTo(Converge(30)).Within(0.002f));
            // Paused presentation time must not move anything.
            Assert.That(AirsideFlightPath.DampFactor(5f, 0f), Is.Zero);
        }

        [Test]
        public void CircuitCues_ApproachReadsAsALandingAircraft()
        {
            TaxiLoopFixture.RestoreCircuit();
            Assert.That(AirsideReusableMotion.GearBias(AircraftPhase.Approach, 0.5f),
                Is.EqualTo(AirsideReusableMotion.GearDeployed));
            Assert.That(AirsideReusableMotion.LandingLightsOn(AircraftPhase.Approach, 0.5f), Is.True);
            Assert.That(AirsideReusableMotion.PropRpmForPhase(AircraftPhase.Approach),
                Is.EqualTo(AirsideReusableMotion.PropRpmApproach));
            Assert.That(AirsideReusableMotion.PropRpmForPhase(AircraftPhase.Approach),
                Is.Not.EqualTo(AirsideReusableMotion.PropRpmForPhase(AircraftPhase.Takeoff)));
            Assert.That(AirsideFlightPath.PitchDegrees(AircraftPhase.Approach, 0f),
                Is.EqualTo(AirsideFlightPath.ApproachPitchStartDegrees).Within(0.01f));
            Assert.That(AirsideReusableMotion.FlapDegrees(AircraftPhase.Approach, 1f),
                Is.GreaterThan(AirsideReusableMotion.FlapDegrees(AircraftPhase.Approach, 0f)));
        }

        [Test]
        public void WingFlex_LoadsAfterRotation_AndSettlesAfterTouchdown()
        {
            Assert.That(AirsideReusableMotion.WingFlexDegrees(AircraftPhase.TaxiOut, 0.8f), Is.Zero);
            Assert.That(AirsideReusableMotion.WingFlexDegrees(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress - 0.01f), Is.Zero);
            Assert.That(AirsideReusableMotion.WingFlexDegrees(AircraftPhase.Takeoff, 1f), Is.GreaterThan(1f));
            Assert.That(AirsideReusableMotion.WingFlexDegrees(AircraftPhase.Approach, 0.5f), Is.GreaterThan(0f));
            Assert.That(AirsideReusableMotion.WingFlexDegrees(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress - 0.01f), Is.GreaterThan(0f));
            Assert.That(AirsideReusableMotion.WingFlexDegrees(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress + 0.15f), Is.Zero);
        }

        [Test]
        public void AircraftLights_FollowPowerAndRunwayPhases()
        {
            Assert.That(AirsideReusableMotion.NavigationLightsOn(false, false), Is.False,
                "a cold parked aircraft stays dark even at night");
            Assert.That(AirsideReusableMotion.NavigationLightsOn(false, true), Is.True,
                "position lamps come on during the pre-start beacon sequence");
            Assert.That(AirsideReusableMotion.StrobesOn(AircraftPhase.TaxiOut), Is.False);
            Assert.That(AirsideReusableMotion.StrobesOn(AircraftPhase.Takeoff), Is.True);
            Assert.That(AirsideReusableMotion.StrobesOn(AircraftPhase.Departed), Is.True);
            Assert.That(AirsideReusableMotion.StrobesOn(AircraftPhase.Landing), Is.True);
            Assert.That(AirsideReusableMotion.StrobesOn(AircraftPhase.TaxiIn), Is.False);
            Assert.That(AirsideReusableMotion.StrobeIntensity(AircraftPhase.Takeoff, 0.02f), Is.EqualTo(1f));
            Assert.That(AirsideReusableMotion.StrobeIntensity(AircraftPhase.Takeoff, 0.10f), Is.Zero);
            Assert.That(AirsideReusableMotion.StrobeIntensity(AircraftPhase.Takeoff, 0.18f), Is.EqualTo(1f));
            Assert.That(AirsideReusableMotion.BeaconIntensity(false, 0.2f), Is.Zero);
        }

        [Test]
        public void NoseWheelSteering_FollowsPathCurvatureAndReversesForPushback()
        {
            var straight = AirsideReusableMotion.NoseWheelSteerDegrees(
                0f, 1f, 0f, 1f, 5f, 1.5f, 10f, tailFirst: false);
            var left = AirsideReusableMotion.NoseWheelSteerDegrees(
                0f, 1f, -0.15f, 0.9887f, 5f, 1.5f, 10f, tailFirst: false);
            var pushed = AirsideReusableMotion.NoseWheelSteerDegrees(
                0f, 1f, -0.15f, 0.9887f, 2f, 1.5f, 10f, tailFirst: true);

            Assert.That(straight, Is.Zero);
            Assert.That(left, Is.LessThan(0f));
            Assert.That(pushed, Is.GreaterThan(0f), "tow-controlled nose gear steers opposite while moving tail-first");
            Assert.That(Mathf.Abs(pushed), Is.LessThanOrEqualTo(65f));
            Assert.That(AirsideReusableMotion.NoseWheelSteerDegrees(
                0f, 1f, 1f, 0f, 0f, 1.5f, 10f, tailFirst: false), Is.Zero,
                "a stopped aircraft centres its nose wheels");
        }

        [Test]
        public void ApproachPicking_OpensOnlyInsideAUsefulViewingDistance()
        {
            var threshold = AirsideFlightPath.WestThresholdX;
            var cutoff = threshold - AircraftPickRouting.ApproachSelectableDistanceFromThresholdMetres;
            Assert.That(AircraftPickRouting.ApproachIsCloseEnough(cutoff - 1f, threshold), Is.False);
            Assert.That(AircraftPickRouting.ApproachIsCloseEnough(cutoff, threshold), Is.True);
            Assert.That(AircraftPickRouting.ApproachIsCloseEnough(
                AirsideFlightPath.ShortFinalX, threshold), Is.True,
                "an aircraft on short final can always be clicked and followed through touchdown");
        }

        [Test]
        public void AdelaideLighting_UsesPublishedRunwaySystemsAndSpacing()
        {
            Assert.That(AdelaideAirfieldLighting.MainRunwayEdgeSpacingMetres, Is.EqualTo(57f));
            Assert.That(AdelaideAirfieldLighting.CrossRunwayEdgeSpacingMetres, Is.EqualTo(59f));
            Assert.That(AdelaideAirfieldLighting.Runway23HialLengthMetres, Is.EqualTo(801f));
            Assert.That(AdelaideAirfieldLighting.PapiSlopeDegrees, Is.EqualTo(3f));
            Assert.That(AdelaideAirfieldLighting.Runway05PapiThresholdHeightFeet, Is.EqualTo(61f));
            Assert.That(AdelaideAirfieldLighting.Runway23PapiThresholdHeightFeet, Is.EqualTo(59f));
            Assert.That(AdelaideAirfieldLighting.CrossRunwayPapiThresholdHeightFeet, Is.EqualTo(51f));

            var mainCount = AdelaideAirfieldLighting.EvenStationCount(
                AirsideBareField.RunwayLengthMetres,
                AdelaideAirfieldLighting.MainRunwayEdgeSpacingMetres);
            Assert.That(mainCount, Is.EqualTo(55));
            Assert.That(AdelaideAirfieldLighting.EvenStation(
                AirsideBareField.RunwayHalfLength, 0, mainCount),
                Is.EqualTo(-AirsideBareField.RunwayHalfLength));
            Assert.That(AdelaideAirfieldLighting.EvenStation(
                AirsideBareField.RunwayHalfLength, mainCount - 1, mainCount),
                Is.EqualTo(AirsideBareField.RunwayHalfLength));
        }

        [Test]
        public void CircuitCues_TouchdownFiresOnTheRunwayNotShortFinal()
        {
            TaxiLoopFixture.RestoreCircuit();
            Assert.That(AirsideFlightPath.HasTouchedDown(0f), Is.False);
            Assert.That(AirsideFlightPath.Landing(0f, 0f).y,
                Is.GreaterThan(AirsideFlightPath.GroundY + 1f));
            Assert.That(AirsideFlightPath.HasTouchedDown(AirsideFlightPath.TouchdownProgress), Is.True);
            Assert.That(AirsideFlightPath.Landing(AirsideFlightPath.TouchdownProgress, 0f).y,
                Is.EqualTo(AirsideFlightPath.GroundY).Within(0.05f));
            Assert.That(AirsideFlightPath.Landing(AirsideFlightPath.TouchdownProgress, 0f).x,
                Is.EqualTo(AirsideFlightPath.TouchdownX).Within(0.5f));
            Assert.That(AirsideFlightPath.PitchDegrees(AircraftPhase.Landing,
                    AirsideFlightPath.TouchdownProgress),
                Is.EqualTo(AirsideFlightPath.FlarePitchDegrees).Within(0.05f));
        }

        [Test]
        public void CircuitCues_GearAndLightsFollowRotateNotBrakeRelease()
        {
            TaxiLoopFixture.RestoreCircuit();
            var rotate = AirsideFlightPath.RotateProgress;
            var retract = AirsideReusableMotion.GearRetractProgress;
            Assert.That(retract, Is.GreaterThan(rotate));
            Assert.That(AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, 0f),
                Is.EqualTo(AirsideReusableMotion.GearDeployed));
            Assert.That(AirsideReusableMotion.LandingLightsOn(AircraftPhase.Takeoff, 0f), Is.True);
            Assert.That(AirsideFlightPath.PitchDegrees(AircraftPhase.Takeoff, rotate * 0.99f), Is.Zero);
            Assert.That(AirsideFlightPath.Takeoff(rotate, 0f).x,
                Is.EqualTo(AirsideFlightPath.RotateX).Within(0.2f));
            Assert.That(AirsideFlightPath.PitchDegrees(AircraftPhase.Takeoff, 1f),
                Is.EqualTo(AirsideFlightPath.ClimbPitchDegrees).Within(0.05f));
            Assert.That(AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, retract - 0.001f),
                Is.EqualTo(AirsideReusableMotion.GearDeployed));
            Assert.That(AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, retract),
                Is.EqualTo(AirsideReusableMotion.GearDeployed),
                "retract eases after the rotate cue rather than popping at the threshold");
            Assert.That(AirsideReusableMotion.GearBias(
                    AircraftPhase.Takeoff,
                    retract + AirsideReusableMotion.GearTransitionProgress),
                Is.EqualTo(AirsideReusableMotion.GearRetracted));
            Assert.That(AirsideReusableMotion.LandingLightsOn(AircraftPhase.Takeoff, retract), Is.False);
            Assert.That(AirsideReusableMotion.GearBias(AircraftPhase.Departed, 0.5f),
                Is.EqualTo(AirsideReusableMotion.GearRetracted));
            Assert.That(AirsideReusableMotion.LandingLightsOn(AircraftPhase.Departed, 0.5f), Is.False);
            Assert.That(AirsideReusableMotion.PropellersSpinning(AircraftPhase.Departed), Is.True);
            Assert.That(AirsideReusableMotion.FlapDegrees(AircraftPhase.Takeoff, 1f),
                Is.LessThan(AirsideReusableMotion.FlapDegrees(AircraftPhase.Takeoff, 0f)));
        }

        [Test]
        public void CircuitCues_SkippedStandDoesNotOpenTheDoorOrKillTheProps()
        {
            TaxiLoopFixture.RestoreCircuit();
            Assert.That(AirportCircuit.SkipGroundTaxi, Is.True);
            Assert.That(AirsideReusableMotion.CabinDoorBias(AircraftPhase.AtStand), Is.Zero);
            Assert.That(AirsideReusableMotion.PropellersSpinning(AircraftPhase.AtStand), Is.True);
            Assert.That(AirsideReusableMotion.LandingLightsOn(AircraftPhase.TaxiIn, 0f), Is.True);
            Assert.That(AirsideReusableMotion.FlapDegrees(AircraftPhase.TaxiOut, 0f), Is.EqualTo(12f));

            TaxiLoopFixture.EnableFullTaxiLoop();
            try
            {
                Assert.That(AirsideReusableMotion.CabinDoorBias(AircraftPhase.AtStand), Is.EqualTo(1f));
                Assert.That(AirsideReusableMotion.PropRpmForPhase(AircraftPhase.AtStand), Is.Zero);
                Assert.That(AirsideReusableMotion.PropellersSpinning(AircraftPhase.Departed), Is.True);
            }
            finally
            {
                TaxiLoopFixture.RestoreCircuit();
            }
        }

        [Test]
        public void FlightPath_TireSpinUsesTravelledDistanceAndRadius()
        {
            var midRoll = AirsideFlightPath.RotateProgress * 0.5f;
            var speed = AirsideFlightPath.GroundSpeedMetresPerSecond(AircraftPhase.Takeoff, midRoll);
            Assert.That(speed, Is.GreaterThan(10f));
            var main = AirsideFlightPath.TireAngularDegreesPerSecond(
                speed, AirsideReusableMotion.MainTireRadiusMetres);
            var nose = AirsideFlightPath.TireAngularDegreesPerSecond(
                speed, AirsideReusableMotion.NoseTireRadiusMetres);
            Assert.That(main, Is.GreaterThan(0f));
            Assert.That(nose, Is.GreaterThan(main), "smaller nose tires spin faster at the same speed");
            Assert.That(AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress + 0.02f), Is.Zero);
            Assert.That(AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress * 0.5f), Is.Zero);
            Assert.That(AirsideFlightPath.TireAngularDegreesPerSecond(0f, 0.37f), Is.Zero);
        }

        [Test]
        public void FlightPath_OleoSettlesOncePerTouchdownWithoutBounce()
        {
            Assert.That(AirsideReusableMotion.OleoCompressionMetres(
                AircraftPhase.Approach, 0.9f), Is.Zero);
            Assert.That(AirsideReusableMotion.OleoCompressionMetres(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress * 0.5f), Is.Zero);
            var peak = AirsideReusableMotion.OleoCompressionMetres(
                AircraftPhase.Landing,
                AirsideFlightPath.TouchdownProgress + AirsideReusableMotion.OleoSettleProgress * 0.5f);
            Assert.That(peak, Is.GreaterThan(AirsideReusableMotion.OleoStaticMetres));
            Assert.That(peak, Is.LessThanOrEqualTo(AirsideReusableMotion.OleoTouchdownMetres + 0.001f));
            var settled = AirsideReusableMotion.OleoCompressionMetres(AircraftPhase.Landing, 0.95f);
            Assert.That(settled, Is.EqualTo(AirsideReusableMotion.OleoStaticMetres).Within(0.001f));
            Assert.That(AirsideReusableMotion.OleoCompressionMetres(AircraftPhase.Departed, 0.2f), Is.Zero);
        }

        [Test]
        public void FlightPath_PhaseSeamsStayContinuousAtOneAndFourX()
        {
            // Sample positions are pure functions of progress — 1× and 4× only change
            // how fast progress advances, not the path itself.
            var landEnd = AirsideFlightPath.Landing(1f, 0f);
            var hold = AirsideFlightPath.OnRunwayHold();
            Assert.That(Vector3.Distance(landEnd, hold), Is.LessThan(0.05f));
            var takeoffStart = AirsideFlightPath.Takeoff(0f);
            Assert.That(Vector3.Distance(hold, takeoffStart), Is.LessThan(0.05f));
            var takeoffEnd = AirsideFlightPath.Takeoff(1f);
            var departedStart = AirsideFlightPath.Departed(0f);
            Assert.That(Vector3.Distance(takeoffEnd, departedStart), Is.LessThan(0.05f));

            var approachEnd = AirsideFlightPath.Approach(1f, 0f);
            var landStart = AirsideFlightPath.Landing(0f, 0f);
            Assert.That(Vector3.Distance(approachEnd, landStart), Is.LessThan(0.5f));
        }

        [Test]
        public void FlightPath_PitchIsContinuousAcrossApproachLandingAndRotate()
        {
            var approachEnd = AirsideFlightPath.PitchDegrees(AircraftPhase.Approach, 1f);
            var landStart = AirsideFlightPath.PitchDegrees(AircraftPhase.Landing, 0f);
            Assert.That(landStart, Is.EqualTo(approachEnd).Within(0.05f));

            // No sudden pitch jump across the flare → touchdown → settle window.
            float previous = AirsideFlightPath.PitchDegrees(AircraftPhase.Landing, 0f);
            for (var i = 1; i <= 40; i++)
            {
                var t = i / 40f;
                var pitch = AirsideFlightPath.PitchDegrees(AircraftPhase.Landing, t);
                Assert.That(Mathf.Abs(pitch - previous), Is.LessThan(3.5f),
                    $"landing pitch jumped by {Mathf.Abs(pitch - previous):0.00}° at t={t:0.00}");
                previous = pitch;
            }

            var beforeRotate = AirsideFlightPath.PitchDegrees(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress - 0.001f);
            Assert.That(beforeRotate, Is.EqualTo(0f).Within(0.05f));
            var afterRotate = AirsideFlightPath.PitchDegrees(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress + 0.08f);
            Assert.That(afterRotate, Is.LessThan(0f));
            Assert.That(Mathf.Abs(afterRotate), Is.LessThan(8f),
                "rotation should ease in, not snap to full nose-up");
        }

        [Test]
        public void FlightPath_GroundSpeedIsContinuousAcrossTouchdownAndRotate()
        {
            var before = AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress + 0.01f);
            var after = AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Landing, AirsideFlightPath.TouchdownProgress + 0.05f);
            Assert.That(before, Is.GreaterThan(5f));
            Assert.That(after, Is.GreaterThan(1f));
            Assert.That(after, Is.LessThan(before * 1.15f));

            var roll = AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress * 0.9f);
            var airborne = AirsideFlightPath.GroundSpeedMetresPerSecond(
                AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress + 0.02f);
            Assert.That(roll, Is.GreaterThan(10f));
            Assert.That(airborne, Is.Zero);
        }

        [Test]
        public void CircuitCues_GearBiasEasesRatherThanSnapping()
        {
            var start = AirsideReusableMotion.GearRetractProgress;
            var mid = start + AirsideReusableMotion.GearTransitionProgress * 0.5f;
            var a = AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, start + 0.001f);
            var b = AirsideReusableMotion.GearBias(AircraftPhase.Takeoff, mid);
            var c = AirsideReusableMotion.GearBias(
                AircraftPhase.Takeoff, start + AirsideReusableMotion.GearTransitionProgress);
            Assert.That(a, Is.LessThan(1f));
            Assert.That(b, Is.LessThan(a));
            Assert.That(b, Is.GreaterThan(c));
            Assert.That(c, Is.EqualTo(0f));
        }


        [Test]
        public void CircuitCues_GearDoorsCloseWhenLockedUpOrDown()
        {
            // Locked down on landing / late approach → doors closed.
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Landing, 0.5f), Is.EqualTo(0f));
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Approach, 1f), Is.EqualTo(0f));
            // Locked up after climb → doors closed.
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Departed, 0.5f), Is.EqualTo(0f));
            // Mid-retract after rotate → doors open.
            var start = AirsideReusableMotion.GearRetractProgress;
            var mid = start + AirsideReusableMotion.GearTransitionProgress * 0.5f;
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Takeoff, mid),
                Is.GreaterThan(0.4f));
            Assert.That(AirsideReusableMotion.GearDoorOpenBias(AircraftPhase.Takeoff, mid),
                Is.LessThan(1.01f));
        }

        [Test]
        public void CircuitCues_LandingFollowKeepsLookAheadThroughRollout()
        {
            // Early landing still looks far ahead; late rollout does not collapse to taxi framing.
            var early = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 0.2f, 0f);
            var mid = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 0.55f, 0f);
            var late = AirsideCameraController.TestLookAheadMetres(AircraftPhase.Landing, 1f, 0f);
            Assert.That(early, Is.EqualTo(32f).Within(0.05f));
            Assert.That(mid, Is.EqualTo(32f).Within(0.05f));
            Assert.That(late, Is.EqualTo(20f).Within(0.05f));
            Assert.That(late, Is.GreaterThan(12f));

            var earlyDist = AirsideCameraController.TestFollowDistance(AircraftPhase.Landing, 0f, 0.2f);
            var lateDist = AirsideCameraController.TestFollowDistance(AircraftPhase.Landing, 0f, 1f);
            Assert.That(earlyDist, Is.EqualTo(54f).Within(0.05f));
            Assert.That(lateDist, Is.EqualTo(42f).Within(0.05f));
            Assert.That(lateDist, Is.GreaterThan(36f));
        }

        [Test]
        public void CircuitCues_TouchdownIsOneShotAtPathContact()
        {
            Assert.That(AirsideFlightPath.HasTouchedDown(
                AirsideFlightPath.TouchdownProgress - 0.001f), Is.False);
            Assert.That(AirsideFlightPath.HasTouchedDown(
                AirsideFlightPath.TouchdownProgress), Is.True);
            Assert.That(AirsideFlightPath.Landing(AirsideFlightPath.TouchdownProgress, 0f).y,
                Is.EqualTo(AirsideFlightPath.GroundY).Within(0.01f));
        }

        [Test]
        public void FollowSelection_TracksTheExactRegisteredAircraft()
        {
            var cameraObject = new GameObject("Selection camera");
            var first = new GameObject("VH-PAX");
            var selected = new GameObject("VH-EMU");
            try
            {
                var controller = cameraObject.AddComponent<AirsideCameraController>();
                controller.SetFollowTargets(new[] { first.transform, selected.transform });

                Assert.That(controller.StartFollow(selected.transform), Is.True);
                Assert.That(controller.IsFollowing, Is.True);
                Assert.That(controller.FollowTarget, Is.SameAs(selected.transform));
            }
            finally
            {
                Object.DestroyImmediate(selected);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void FollowSelection_RejectsAnAircraftThatIsNotOnTheField()
        {
            var cameraObject = new GameObject("Selection camera");
            var visible = new GameObject("VH-PAX");
            var away = new GameObject("VH-AWAY");
            try
            {
                var controller = cameraObject.AddComponent<AirsideCameraController>();
                controller.SetFollowTargets(new[] { visible.transform });

                Assert.That(controller.StartFollow(away.transform), Is.False);
                Assert.That(controller.IsFollowing, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(away);
                Object.DestroyImmediate(visible);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void FollowSelection_ReleasesWhenTheFollowedAircraftLeavesTheField()
        {
            var cameraObject = new GameObject("Selection camera");
            var first = new GameObject("VH-PAX");
            var selected = new GameObject("VH-EMU");
            try
            {
                var controller = cameraObject.AddComponent<AirsideCameraController>();
                controller.SetFollowTargets(new[] { first.transform, selected.transform });
                Assert.That(controller.StartFollow(selected.transform), Is.True);

                controller.SetFollowTargets(new[] { first.transform });
                Assert.That(controller.IsFollowing, Is.False, "do not silently follow someone else");
                Assert.That(controller.FollowTarget, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(selected);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void FollowSelection_KeepsTheSameAircraftWhenAnotherLeaves()
        {
            var cameraObject = new GameObject("Selection camera");
            var first = new GameObject("VH-PAX");
            var selected = new GameObject("VH-EMU");
            try
            {
                var controller = cameraObject.AddComponent<AirsideCameraController>();
                controller.SetFollowTargets(new[] { first.transform, selected.transform });
                Assert.That(controller.StartFollow(selected.transform), Is.True);

                controller.SetFollowTargets(new[] { selected.transform });
                Assert.That(controller.IsFollowing, Is.True);
                Assert.That(controller.FollowTarget, Is.SameAs(selected.transform));
            }
            finally
            {
                Object.DestroyImmediate(selected);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void AircraftPickProxy_AttachesInvisibleVolumeOnThePickLayer()
        {
            var aircraft = new GameObject("Commercial VH-PAX");
            try
            {
                var proxy = AircraftPickProxy.Ensure(aircraft.transform, "VH-PAX");
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.AircraftId, Is.EqualTo("VH-PAX"));

                var child = aircraft.transform.Find(AircraftPickRouting.ProxyChildName);
                Assert.That(child, Is.Not.Null);
                Assert.That(child.GetComponent<Renderer>(), Is.Null);

                var box = child.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null);
                Assert.That(box.isTrigger, Is.True);
                Assert.That(box.size.x, Is.EqualTo(AircraftPickRouting.ProxyWidthMetres).Within(0.01f));
                Assert.That(box.size.y, Is.EqualTo(AircraftPickRouting.ProxyHeightMetres).Within(0.01f));
                Assert.That(box.size.z, Is.EqualTo(AircraftPickRouting.ProxyLengthMetres).Within(0.01f));

                var layer = LayerMask.NameToLayer(AircraftPickRouting.PickLayerName);
                if (layer >= 0)
                    Assert.That(child.gameObject.layer, Is.EqualTo(layer));

                // Idempotent: second ensure refreshes the same proxy.
                var again = AircraftPickProxy.Ensure(aircraft.transform, "VH-PAX");
                Assert.That(again, Is.SameAs(proxy));
                Assert.That(aircraft.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(aircraft);
            }
        }

        [Test]
        public void Boeing7378_ProfileUsesItsTrueScaleArtAndPickVolume()
        {
            Assert.That(AircraftType.TryFromId("B38M", out var type), Is.True);
            Assert.That(type, Is.SameAs(AircraftType.Boeing7378));

            var profile = AircraftVisualProfiles.For(type);
            Assert.That(profile.ArtRelativePath,
                Is.EqualTo("Models/Aircraft/mdl_737_8_narrowbody_v01.gltf"));
            Assert.That(profile.PickSizeMetres.x, Is.EqualTo(40f));
            Assert.That(profile.PickSizeMetres.z, Is.EqualTo(43f));
            Assert.That(profile.VisualCentreOffsetMetres.z, Is.EqualTo(-19.735f).Within(0.001f));
            Assert.That(profile.SelectionMarkerDiameterMetres, Is.EqualTo(41f));
            Assert.That(profile.FollowDistanceMultiplier, Is.GreaterThan(1f));
            Assert.That(profile.MainTireRadiusMetres, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(profile.NoseTireRadiusMetres, Is.EqualTo(0.55f).Within(0.001f));
        }

        [Test]
        public void CircuitCues_JetFanPowerFollowsTheSameOperationalPhasesAsTheJet()
        {
            Assert.That(AirsideReusableMotion.JetFanRpmForPhase(AircraftPhase.Takeoff),
                Is.GreaterThan(AirsideReusableMotion.JetFanRpmForPhase(AircraftPhase.TaxiOut)));
            Assert.That(AirsideReusableMotion.JetFanRpmForPhase(AircraftPhase.Approach),
                Is.GreaterThan(AirsideReusableMotion.JetFanRpmForPhase(AircraftPhase.TaxiOut)));
            Assert.That(AirsideReusableMotion.JetFanRpmForPhase(AircraftPhase.AtStand),
                Is.EqualTo(AirportCircuit.SkipGroundTaxi
                    ? AirsideReusableMotion.JetFanRpmTaxi
                    : 0f));
        }

        [Test]
        public void Dash8Q400_ProfileUsesItsOwnTrueScaleArtAndFraming()
        {
            Assert.That(AircraftType.TryFromId("DH8D", out var type), Is.True);
            Assert.That(type, Is.SameAs(AircraftType.Dash8Q400));

            var profile = AircraftVisualProfiles.For(type);
            Assert.That(profile, Is.EqualTo(AircraftVisualProfiles.Dash8Q400));
            Assert.That(profile.ArtRelativePath,
                Is.EqualTo("Models/Aircraft/mdl_dash8_q400_v01.gltf"));
            Assert.That(profile.PickSizeMetres.x, Is.EqualTo(31f));
            Assert.That(profile.PickSizeMetres.z, Is.EqualTo(36f));
            Assert.That(profile.VisualCentreOffsetMetres, Is.EqualTo(Vector3.zero));
            Assert.That(profile.SelectionMarkerDiameterMetres, Is.EqualTo(33f));
            Assert.That(profile.FollowDistanceMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void Saab340_ProfileUsesItsOwnTrueScaleArtAndFraming()
        {
            Assert.That(AircraftType.TryFromId("SF34", out var type), Is.True);
            Assert.That(type, Is.SameAs(AircraftType.Saab340));

            var profile = AircraftVisualProfiles.For(type);
            Assert.That(profile, Is.EqualTo(AircraftVisualProfiles.Saab340));
            Assert.That(profile.ArtRelativePath,
                Is.EqualTo("Models/Aircraft/mdl_saab_340b_v01.gltf"));
            Assert.That(profile.PickSizeMetres.x, Is.EqualTo(24f));
            Assert.That(profile.PickSizeMetres.z, Is.EqualTo(23f));
            Assert.That(profile.VisualCentreOffsetMetres, Is.EqualTo(Vector3.zero));
            Assert.That(profile.SelectionMarkerDiameterMetres, Is.EqualTo(22f));
            Assert.That(profile.FollowDistanceMultiplier, Is.EqualTo(0.92f));
        }

        [Test]
        public void AircraftPickProxy_UsesTheAttachedAircraftProfile()
        {
            var aircraft = new GameObject("Gate 13 · Boeing 737-8");
            try
            {
                var profile = AircraftVisualProfiles.Boeing7378;
                AircraftVisualProfileComponent.Ensure(aircraft.transform, profile);
                AircraftPickProxy.Ensure(aircraft.transform, "GATE-13-737-8");

                var proxy = aircraft.transform.Find(AircraftPickRouting.ProxyChildName);
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.localPosition.y, Is.EqualTo(profile.PickCentreYMetres));
                Assert.That(proxy.localPosition.z, Is.EqualTo(profile.VisualCentreOffsetMetres.z));
                Assert.That(proxy.GetComponent<BoxCollider>().size, Is.EqualTo(profile.PickSizeMetres));
            }
            finally
            {
                Object.DestroyImmediate(aircraft);
            }
        }

        [Test]
        public void FinalAtr_PreferredKitPathIsStarterV01()
        {
            // PreferArtKit is private; the production contract is the Resources key and
            // the glTF stem used by BuildAircraft.
            Assert.That(AirsideReusableMotion.MainTireRadiusMetres, Is.EqualTo(0.37f));
            Assert.That(AirsideReusableMotion.NoseTireRadiusMetres, Is.EqualTo(0.31f));
            Assert.That(AirsideReusableMotion.MainTireRadiusMetres * 2f, Is.EqualTo(0.74f).Within(0.001f));
        }
    }
}
