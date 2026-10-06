using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftAuditFanGeometryTests
    {
        [TestCase(0.777f)] [TestCase(0.780f)] [TestCase(1.420f)] [TestCase(1.417f)]
        public void DiscContainsAuthoredFanTipWithMargin(float radius)
        {
            var maximum = AircraftFanDiscGeometry.IncludeRadiusSquared(0f, 0.6f * radius, 0.8f * radius);
            Assert.That(AircraftFanDiscGeometry.Diameter(maximum) / 2f,
                Is.EqualTo(radius * 1.025f).Within(0.00001f));
        }

        [Test]
        public void CoverageUsesDistanceFromHubAndKeepsLargestTip()
        {
            var maximum = AircraftFanDiscGeometry.IncludeRadiusSquared(0f, 0f, 1.42f);
            maximum = AircraftFanDiscGeometry.IncludeRadiusSquared(maximum, 0.2f, 0.1f);
            Assert.That(AircraftFanDiscGeometry.Diameter(maximum), Is.GreaterThan(2.84f));
            Assert.That(AircraftFanDiscGeometry.Diameter(0f), Is.EqualTo(0.9225f).Within(0.00001f));
        }
    }
}
