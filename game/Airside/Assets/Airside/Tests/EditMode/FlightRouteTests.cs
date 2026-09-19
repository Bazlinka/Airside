using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightRouteTests
    {
        [Test]
        public void TwoFlightsSameCityPair_DoNotShareOneTrack()
        {
            Assert.That(DestinationCatalogue.TryFind("ADL", out var adl), Is.True);
            Assert.That(DestinationCatalogue.TryFind("MEL", out var mel), Is.True);
            FlightRoute.Point(adl.Latitude, adl.Longitude, mel.Latitude, mel.Longitude, 0.5, "QF680",
                out var latA, out var lonA);
            FlightRoute.Point(adl.Latitude, adl.Longitude, mel.Latitude, mel.Longitude, 0.5, "VA232",
                out var latB, out var lonB);
            FlightRoute.GreatCircle(adl.Latitude, adl.Longitude, mel.Latitude, mel.Longitude, 0.5,
                out var latGc, out var lonGc);

            var gap = FlightRoute.DistanceKm(latA, lonA, latB, lonB);
            Assert.That(gap, Is.GreaterThan(1.0), "two Melbourne services must sit on different airways");
            Assert.That(FlightRoute.DistanceKm(latA, lonA, latGc, lonGc), Is.GreaterThan(1.0));
        }

        [Test]
        public void Endpoints_StayOnTheAirports()
        {
            Assert.That(DestinationCatalogue.TryFind("ADL", out var adl), Is.True);
            Assert.That(DestinationCatalogue.TryFind("PER", out var per), Is.True);
            FlightRoute.Point(adl.Latitude, adl.Longitude, per.Latitude, per.Longitude, 0, "QF641",
                out var lat0, out var lon0);
            FlightRoute.Point(adl.Latitude, adl.Longitude, per.Latitude, per.Longitude, 1, "QF641",
                out var lat1, out var lon1);
            Assert.That(lat0, Is.EqualTo(adl.Latitude).Within(1e-6));
            Assert.That(lon0, Is.EqualTo(adl.Longitude).Within(1e-6));
            Assert.That(lat1, Is.EqualTo(per.Latitude).Within(1e-6));
            Assert.That(lon1, Is.EqualTo(per.Longitude).Within(1e-6));
        }

        [Test]
        public void Offset_IsDeterministic()
        {
            Assert.That(FlightRoute.OffsetKm("QF680", 650), Is.EqualTo(FlightRoute.OffsetKm("QF680", 650)));
            Assert.That(FlightRoute.OffsetKm("QF680", 650), Is.Not.EqualTo(FlightRoute.OffsetKm("VA232", 650)));
        }
    }
}
