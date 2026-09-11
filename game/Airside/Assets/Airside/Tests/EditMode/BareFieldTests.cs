using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class BareFieldTests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.RestoreCircuit();

        [Test]
        public void BareField_IsEnabled()
        {
            Assert.That(AirsideBareField.Enabled, Is.True);
        }

        [Test]
        public void BareField_RunwayIsAdelaideFiveTwoThreeInRealMetres()
        {
            Assert.That(AirsideBareField.RunwayLengthMetres, Is.EqualTo(3100f));
            Assert.That(AirsideBareField.RunwayWidthMetres, Is.EqualTo(45f));
            Assert.That(AirsideBareField.RunwayObjectName, Is.EqualTo("Runway W"));
        }

        [Test]
        public void BareField_GroundMatchesPublishedAdelaideHectares()
        {
            Assert.That(AirsideBareField.GroundLengthMetres, Is.EqualTo(3400f));
            Assert.That(AirsideBareField.GroundWidthMetres, Is.EqualTo(2309f));
            Assert.That(AirsideBareField.GroundAreaSquareMetres, Is.EqualTo(3400f * 2309f));
            Assert.That(AirsideBareField.GroundHectares,
                Is.EqualTo(AirsideBareField.AdelaideAirportHectares).Within(0.1f));
            Assert.That(AirsideBareField.GroundObjectName, Is.EqualTo("Airport ground"));
        }

        [Test]
        public void BareField_GroundSurroundsTheRunway()
        {
            Assert.That(AirsideBareField.GroundLengthMetres,
                Is.GreaterThan(AirsideBareField.RunwayLengthMetres));
            Assert.That(AirsideBareField.GroundWidthMetres,
                Is.GreaterThan(AirsideBareField.RunwayWidthMetres));
            Assert.That(AirsideBareField.ContainsGround(0f, 0f), Is.True);
            Assert.That(AirsideBareField.ContainsRunway(0f, 0f), Is.True);
            Assert.That(AirsideBareField.ContainsRunway(1550f, 0f), Is.True);
            Assert.That(AirsideBareField.ContainsRunway(1551f, 0f), Is.False);
            Assert.That(AirsideBareField.ContainsRunway(0f, 22.5f), Is.True);
            Assert.That(AirsideBareField.ContainsRunway(0f, 22.6f), Is.False);
            Assert.That(AirsideBareField.ContainsGround(1700f, 1154f), Is.True);
            Assert.That(AirsideBareField.ContainsGround(1701f, 0f), Is.False);
        }

        [Test]
        public void Circuit_SkipsTaxiAndUsesRealRunwayDurations()
        {
            Assert.That(AirportCircuit.SkipGroundTaxi, Is.True);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.TaxiIn), Is.True);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.AtStand), Is.True);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.Pushback), Is.True);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.TaxiOut), Is.True);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.Landing), Is.False);
            Assert.That(AirportCircuit.IsSkippedGroundPhase(AircraftPhase.Takeoff), Is.False);
            Assert.That(AirportCircuit.DurationSeconds(AircraftPhase.Approach),
                Is.EqualTo(AirportCircuit.ApproachSeconds));
            Assert.That(AirportCircuit.DurationSeconds(AircraftPhase.Landing),
                Is.EqualTo(AirportCircuit.LandingSeconds));
            Assert.That(AirportCircuit.DurationSeconds(AircraftPhase.Takeoff),
                Is.EqualTo(AirportCircuit.TakeoffSeconds));
            Assert.That(AirportCircuit.DurationSeconds(AircraftPhase.TaxiIn),
                Is.EqualTo(AirportCircuit.TaxiSkipSeconds));
        }

        [Test]
        public void BareField_CameraCanSeeTheWholeSite()
        {
            Assert.That(AirsideBareField.CameraFarClip,
                Is.GreaterThan(AirsideBareField.GroundLengthMetres));
            Assert.That(AirsideBareField.MaxOrbitDistance,
                Is.GreaterThan(AirsideBareField.RunwayLengthMetres * 0.6f));
            Assert.That(AirsideBareField.OverviewDistance,
                Is.GreaterThan(1000f));
        }

        [Test]
        public void RunwayMarkings_ThresholdsSitOnTheRealMetreEnds()
        {
            Assert.That(AirsideRunwayMarkings.WestThresholdX,
                Is.EqualTo(-AirsideBareField.RunwayHalfLength));
            Assert.That(AirsideRunwayMarkings.EastThresholdX,
                Is.EqualTo(AirsideBareField.RunwayHalfLength));
            Assert.That(AirsideRunwayMarkings.WestThresholdX, Is.EqualTo(-1550f));
            Assert.That(AirsideRunwayMarkings.ThresholdStripeCount, Is.EqualTo(12));
            Assert.That(AirsideRunwayMarkings.ThresholdStripes().Length,
                Is.EqualTo(AirsideRunwayMarkings.ThresholdStripeCount * 2));
        }

        [Test]
        public void RunwayMarkings_AimingPointBeginsFourHundredMetresPastTheThreshold()
        {
            Assert.That(AirsideRunwayMarkings.AimingPointFromThreshold, Is.EqualTo(400f));
            Assert.That(AirsideRunwayMarkings.WestAimingStartX,
                Is.EqualTo(-AirsideBareField.RunwayHalfLength + 400f));
            Assert.That(AirsideRunwayMarkings.WestAimingStartX, Is.EqualTo(-1150f));
            Assert.That(AirsideRunwayMarkings.EastAimingStartX,
                Is.EqualTo(AirsideBareField.RunwayHalfLength - 400f));
            Assert.That(AirsideRunwayMarkings.HasTouchdownZoneAt(400f), Is.False,
                "400 m is the aiming point; do not double it as a TDZ pair");
        }

        [Test]
        public void RunwayMarkings_ThreeHundredMetreTdzSitsUnderTheCircuitTouchdown()
        {
            Assert.That(AirsideRunwayMarkings.HasTouchdownZoneAt(300f), Is.True);
            Assert.That(AirsideRunwayMarkings.WestTouchdownZoneX(300f),
                Is.EqualTo(-AirsideBareField.RunwayHalfLength + 300f));
            Assert.That(AirsideRunwayMarkings.WestTouchdownZoneX(300f), Is.EqualTo(-1250f));
            Assert.That(AirsideRunwayMarkings.EastTouchdownZoneX(300f),
                Is.EqualTo(AirsideBareField.RunwayHalfLength - 300f));
        }

        [Test]
        public void RunwayMarkings_CentrelineIsDashedAndEdgesAreWideEnoughToRead()
        {
            var dashes = AirsideRunwayMarkings.CentrelineDashes();
            Assert.That(dashes.Length, Is.GreaterThan(20), "one 3 km bar is not a centreline");
            Assert.That(dashes[0].LengthX, Is.EqualTo(AirsideRunwayMarkings.CentrelineDashLength));
            Assert.That(dashes[0].LengthX, Is.EqualTo(30f));
            Assert.That(dashes[0].WidthZ, Is.EqualTo(0.90f));
            Assert.That(AirsideRunwayMarkings.EdgeWidth, Is.EqualTo(0.90f));
            Assert.That(AirsideRunwayMarkings.EdgeWidth, Is.Not.EqualTo(0.35f));
            Assert.That(AirsideRunwayMarkings.EdgeOuterZ,
                Is.LessThan(AirsideBareField.RunwayHalfWidth));
            Assert.That(AirsideRunwayMarkings.EdgeOuterZ,
                Is.GreaterThan(AirsideBareField.RunwayHalfWidth - 1.01f));
        }

        [Test]
        public void RunwayMarkings_EveryMarkStaysOnThePavement()
        {
            var marks = AirsideRunwayMarkings.All();
            Assert.That(marks.Length, Is.GreaterThan(50));
            for (var i = 0; i < marks.Length; i++)
            {
                Assert.That(marks[i].OnPavement, Is.True,
                    $"mark {i} at x={marks[i].CenterX} z={marks[i].CenterZ} leaves the 45 m pavement");
            }
        }

        [Test]
        public void BareField_HidesEconomyHudChrome()
        {
            Assert.That(AirsideFocusMode.BareWorld, Is.True);
            Assert.That(AirsideFocusMode.ShowEconomyHud, Is.False);
        }
    }
}
