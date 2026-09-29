using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class ArrivalMapTrackTests
    {
        private static Destination Melbourne()
        {
            DestinationCatalogue.TryFind("MEL", out var mel);
            return mel;
        }

        [Test]
        public void YpadFrame_LatLonRoundTrips()
        {
            YpadFrame.ToWorld(-34.9285, 138.6007, out var x, out var z);
            YpadFrame.ToLatLon(x, z, out var lat, out var lon);
            Assert.That(lat, Is.EqualTo(-34.9285).Within(1e-6));
            Assert.That(lon, Is.EqualTo(138.6007).Within(1e-6));
        }

        [TestCase("MEL")]
        [TestCase("KGC")]
        [TestCase("SIN")]
        public void DistanceOut_FallsSteadilyFromTheWholeLegToNothing(string code)
        {
            DestinationCatalogue.TryFind(code, out var origin);
            var type = AircraftType.Boeing7378;
            var km = origin.DistanceKmTo(DestinationCatalogue.Adelaide);
            var seconds = LegTiming.AirborneSeconds(km, type);
            var previous = km * 1000.0;
            for (var remaining = (double)seconds; remaining >= 0; remaining -= 5)
            {
                var d = ArrivalMapTrack.DistanceOutMetres(km, seconds, remaining, type);
                Assert.That(d, Is.LessThanOrEqualTo(previous + 1e-6), $"{code} went backwards at {remaining:0} s left");
                Assert.That(previous - d, Is.LessThan(1500.0), $"{code} jumped {previous - d:0} m in 5 s at {remaining:0} s left");
                previous = d;
            }

            Assert.That(ArrivalMapTrack.DistanceOutMetres(km, seconds, seconds, type), Is.EqualTo(km * 1000.0).Within(1.0));
            Assert.That(ArrivalMapTrack.DistanceOutMetres(km, seconds, 0, type), Is.Zero.Within(0.001));
        }

        [Test]
        public void LastThirtyTwoKilometres_AreFlownAtApproachSpeed_LikeTheField()
        {
            var type = AircraftType.Boeing7378;
            var km = Melbourne().DistanceKmTo(DestinationCatalogue.Adelaide);
            var seconds = LegTiming.AirborneSeconds(km, type);
            var speed = CircuitProfile.Knots(AircraftPerformance.For(type).ApproachKnots);
            foreach (var remaining in new[] { 60.0, 200.0, 400.0 })
                Assert.That(ArrivalMapTrack.DistanceOutMetres(km, seconds, remaining, type),
                    Is.EqualTo(speed * remaining).Within(1.0),
                    "the field draws it speed × time-to-clearance out; the map must match");
        }

        [Test]
        public void MapPosition_IsContinuousThroughTheBlendAndLandsOnTheField()
        {
            var type = AircraftType.Boeing7378;
            var mel = Melbourne();
            var home = DestinationCatalogue.Adelaide;
            var legM = mel.DistanceKmTo(home) * 1000.0;
            foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
            {
                var lateral = ArrivalApproach.LateralFactor(
                    ArrivalApproach.Bearing(home, mel), ArrivalApproach.LandingHeading(runway));
                double lastX = 0, lastZ = 0;
                var first = true;
                for (var d = 100_000.0; d >= 0; d -= 250)
                {
                    ArrivalMapTrack.LatLon(mel, home, "VH-TST", legM, d, runway, type, lateral, out var lat, out var lon);
                    YpadFrame.ToWorld(lat, lon, out var x, out var z);
                    if (!first)
                        Assert.That(System.Math.Sqrt((x - lastX) * (x - lastX) + (z - lastZ) * (z - lastZ)),
                            Is.LessThan(800.0), $"{runway} jumped near {d:0} m out");
                    lastX = x; lastZ = z; first = false;
                }

                // At the hold point the map is on the runway line, a short way up final.
                ArrivalMapTrack.LatLon(mel, home, "VH-TST", legM, 0, runway, type, lateral, out var la, out var lo);
                YpadFrame.ToWorld(la, lo, out var hx, out var hz);
                Assert.That(Mathf.Abs((float)hx), Is.LessThan(3000f));
                Assert.That(Mathf.Abs((float)hz), Is.LessThan(400f));
            }
        }

        [Test]
        public void FinalWorld_PutsTheTwoRunwayEndsOnOppositeSidesOfTheField()
        {
            ArrivalMapTrack.FinalWorldXZ(RunwayDirection.Runway05, AircraftType.Boeing7378, 10_000f, 0f, 0f, out var x05, out _);
            ArrivalMapTrack.FinalWorldXZ(RunwayDirection.Runway23, AircraftType.Boeing7378, 10_000f, 0f, 0f, out var x23, out _);
            Assert.That(x05, Is.LessThan(-8000f), "05 arrivals come in from the south-west");
            Assert.That(x23, Is.GreaterThan(8000f), "23 arrivals come in from the north-east");
        }

        [Test]
        public void MiniMapEdgePoint_PinsOffMapAircraftToTheEdgeTheyComeFrom()
        {
            var map = new Rect(100, 50, 200, 100);
            var inside = new Vector2(150, 90);
            Assert.That(FieldMiniMap.EdgePoint(map, inside, 5f), Is.EqualTo(inside));
            var far = FieldMiniMap.EdgePoint(map, new Vector2(5000, 100), 5f);
            Assert.That(far.x, Is.EqualTo(map.xMax - 5f).Within(0.01f));
            Assert.That(far.y, Is.EqualTo(map.center.y).Within(0.5f));
            var corner = FieldMiniMap.EdgePoint(map, new Vector2(-4000, -4000), 5f);
            Assert.That(map.Contains(corner));
            Assert.That(corner.x, Is.LessThan(map.center.x));
            Assert.That(corner.y, Is.LessThan(map.center.y));
        }
    }
}
