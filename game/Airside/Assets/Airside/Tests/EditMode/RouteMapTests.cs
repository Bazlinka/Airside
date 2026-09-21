using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RouteMapTests
    {
        [Test]
        public void Progress_IsSubSecondAndClamped()
        {
            Assert.That(RouteMap.Progress(100, 200, 150.5), Is.EqualTo(0.505).Within(1e-9));
            Assert.That(RouteMap.Progress(100, 200, 50), Is.EqualTo(0));
            Assert.That(RouteMap.Progress(100, 200, 250), Is.EqualTo(1));
            Assert.That(RouteMap.Progress(100, 100, 100), Is.EqualTo(1));
        }

        [Test]
        public void GreatCirclePoint_HitsEndpointsAndBowsTowardThePole()
        {
            // Adelaide to Perth.
            RouteMap.GreatCirclePoint(-34.945, 138.531, -31.940, 115.967, 0, out var lat0, out var lon0);
            RouteMap.GreatCirclePoint(-34.945, 138.531, -31.940, 115.967, 1, out var lat1, out var lon1);
            RouteMap.GreatCirclePoint(-34.945, 138.531, -31.940, 115.967, 0.5, out var latMid, out var lonMid);

            Assert.That(lat0, Is.EqualTo(-34.945).Within(1e-6));
            Assert.That(lon0, Is.EqualTo(138.531).Within(1e-6));
            Assert.That(lat1, Is.EqualTo(-31.940).Within(1e-6));
            Assert.That(lon1, Is.EqualTo(115.967).Within(1e-6));
            // Southern hemisphere great circles bulge south of the straight lat/lon line.
            Assert.That(latMid, Is.LessThan((-34.945 + -31.940) / 2));
            Assert.That(lonMid, Is.InRange(115.967, 138.531));
        }

        [Test]
        public void DestinationPoint_ProducesRequestedGeodesicDistance()
        {
            var home = DestinationCatalogue.Adelaide;
            foreach (var distance in new[] { 500.0, 1000.0, 2000.0 })
            {
                RouteMap.DestinationPoint(home.Latitude, home.Longitude, distance, 73.0,
                    out var latitude, out var longitude);
                var point = new Destination("RING", "Range point", string.Empty, latitude, longitude);
                Assert.That(home.DistanceKmTo(point), Is.EqualTo(distance).Within(0.01));
            }
        }

        [Test]
        public void ClipSegment_TrimsToRectAndRejectsOutside()
        {
            float x0 = -10f, y0 = 50f, x1 = 110f, y1 = 50f;
            Assert.That(RouteMap.ClipSegment(ref x0, ref y0, ref x1, ref y1, 0f, 0f, 100f, 100f), Is.True);
            Assert.That(x0, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(x1, Is.EqualTo(100f).Within(1e-4f));

            float a0 = -10f, b0 = -10f, a1 = -5f, b1 = 200f;
            Assert.That(RouteMap.ClipSegment(ref a0, ref b0, ref a1, ref b1, 0f, 0f, 100f, 100f), Is.False);
        }
    }
}
