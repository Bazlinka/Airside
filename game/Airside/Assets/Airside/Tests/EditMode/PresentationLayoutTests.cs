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
        [TestCase(1440, 900, 1f)]
        [TestCase(1280, 720, 0.8f)]
        public void HudScale_NormalWindowsUseReadableVirtualSize(int width, int height, float expected)
        {
            Assert.That(HudLayout.ScaleFor(width, height), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void HudScale_RetinaDisplayIsNotCappedAtTheOldTinyScale()
        {
            Assert.That(HudLayout.ScaleFor(3456, 2168), Is.EqualTo(2.25f).Within(0.001f));
        }

        [TestCase(1280f, 800f)]
        [TestCase(1536f, 964f)]
        public void HudLayout_DailyReportAndOfferHaveSeparateSpace(float width, float height)
        {
            var layout = HudLayout.Create(width, height, 330f, showDailyReport: true, showRouteOffer: true);

            Assert.That(layout.OperationsPanel.Overlaps(layout.DailyReportPanel), Is.False);
            Assert.That(layout.OperationsPanel.Overlaps(layout.RouteOfferPanel), Is.False);
            Assert.That(layout.DailyReportPanel.Overlaps(layout.RouteOfferPanel), Is.False);
            Assert.That(layout.RouteOfferPanel.yMax, Is.LessThanOrEqualTo(height - 22f));
            Assert.That(layout.LeftPanel.Overlaps(layout.OperationsPanel), Is.False);
        }

        [TestCase(1280, 720)]
        [TestCase(1440, 900)]
        [TestCase(3456, 2168)]
        public void HudLayout_PhysicalLaptopAndRetinaSizesRemainInsideViewport(int screenWidth, int screenHeight)
        {
            var scale = HudLayout.ScaleFor(screenWidth, screenHeight);
            var width = screenWidth / scale;
            var height = screenHeight / scale;
            var layout = HudLayout.Create(width, height, 354f, showDailyReport: true, showRouteOffer: true);

            Assert.That(layout.LeftPanel.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.LeftPanel.yMax, Is.LessThanOrEqualTo(height));
            Assert.That(layout.RouteOfferPanel.xMax, Is.LessThanOrEqualTo(width));
            Assert.That(layout.RouteOfferPanel.yMax, Is.LessThanOrEqualTo(height));
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
        public void GroundTrafficVisual_WhenYieldedDoesNotInterpolateThroughReleasedSpace()
        {
            var previous = new Vector3(20f, 0.7f, 9f);
            var resetBySimulation = new Vector3(8f, 0.7f, 9f);

            var visible = TaxiVisualPath.MoveGroundTraffic(previous, resetBySimulation, isHolding: true, maxDistanceDelta: 0.1f);

            Assert.That(visible, Is.EqualTo(resetBySimulation));
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

                var slab = new GameObject("Runway W");
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
        /// Metres per real second at 1x. Phase durations differ — takeoff lasts 15 s and
        /// the departure fly-out 6 s — so distance per unit of progress is not comparable
        /// across a seam, and comparing it flagged a matched handover as a 2.5x lurch.
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
        public void FlightPath_AirPhasesMoveAtAircraftSpeedNotTaxiSpeed()
        {
            Assert.That(SpeedAt(AircraftPhase.Approach, 0.5f, 0.502f), Is.GreaterThan(40f));
            Assert.That(SpeedAt(AircraftPhase.Departed, 0.5f, 0.502f), Is.GreaterThan(80f));
            Assert.That(SpeedAt(AircraftPhase.Landing, 0.4f, 0.402f), Is.GreaterThan(30f));
        }

        [Test]
        public void FlightPath_TakeoffDoesNotSlowDownAtRotation()
        {
            var r = AirsideFlightPath.RotateProgress;
            Assert.That(r, Is.InRange(AirsideFlightPath.LineupProgress, 0.95f));
            Assert.That(AirsideFlightPath.Takeoff(r).x, Is.EqualTo(AirsideFlightPath.RotateX).Within(0.2f));
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
            Assert.That(start.x, Is.EqualTo(AirsideFlightPath.RolloutEndX).Within(0.01f));

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

                Assert.That(max / min, Is.LessThan(1.35f), $"reverse={reverse} taxi speed is uneven");
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
        public void FocusMode_GroundClutterFollowsTheSingleAircraftOnlySwitch()
        {
            // The bare field parks every non-aircraft object behind the same switch
            // so the player sees only plane, runway, ground and sun lighting.
            Assert.That(AirsideFocusMode.ShowGroundVehicles, Is.False);
            Assert.That(AirsideFocusMode.ShowStandEquipment, Is.False);
            Assert.That(AirsideFocusMode.ShowPeople, Is.False);
            Assert.That(AirsideFocusMode.ShowGroundTrafficAircraft, Is.False);
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
        public void FlightPath_TouchdownSitsOnTheThreeHundredMetreTdz()
        {
            // Headless BareFieldTests cannot compile AirsideFlightPath. Keep this
            // equality in both Unity EditMode and work/flightcheck.
            Assert.That(AirsideFlightPath.TouchdownX,
                Is.EqualTo(AirsideRunwayMarkings.WestTouchdownZoneX(300f)).Within(0.01f));
            Assert.That(AirsideFlightPath.WestThresholdX,
                Is.EqualTo(AirsideRunwayMarkings.WestThresholdX).Within(0.01f));
            var half = AirsideRunwayMarkings.TouchdownZoneLength * 0.5f;
            Assert.That(AirsideFlightPath.TouchdownX,
                Is.InRange(
                    AirsideRunwayMarkings.WestTouchdownZoneX(300f) - half,
                    AirsideRunwayMarkings.WestTouchdownZoneX(300f) + half));
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
    }
}
