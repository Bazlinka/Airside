using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class BareFieldTests
    {
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
        public void BareField_CameraCanSeeTheWholeSite()
        {
            Assert.That(AirsideBareField.CameraFarClip,
                Is.GreaterThan(AirsideBareField.GroundLengthMetres));
            Assert.That(AirsideBareField.MaxOrbitDistance,
                Is.GreaterThan(AirsideBareField.RunwayLengthMetres * 0.6f));
            Assert.That(AirsideBareField.OverviewDistance,
                Is.GreaterThan(1000f));
        }
    }
}
