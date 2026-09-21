using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideTaxiRouterTests
    {
        [Test]
        public void TaxiOut_To12FollowsTaxiwaysInsteadOfAGrassChord()
        {
            var bay = Array.Find(AdelaideLayout.Bays, b => b.Id == "BAY-2");
            var path = AdelaideCrossRoutes.TaxiOutFrom(bay.TaxiOut[0], bay.TaxiOut[1],
                RunwayDirection.Runway12);
            Assert.That(Length(path), Is.GreaterThan(1800f),
                "the real T4–K–A–G1 route is over 2 km, not a 1 km chord");
            AssertStaysOnTaxiOrRunway(path, "taxi-out 12");
        }

        [Test]
        public void TaxiOut_To30CrossesOnPavementToTheD2Hold()
        {
            var bay = Array.Find(AdelaideLayout.Bays, b => b.Id == "BAY-2");
            var path = AdelaideCrossRoutes.TaxiOutFrom(bay.TaxiOut[0], bay.TaxiOut[1],
                RunwayDirection.Runway30);
            Assert.That(Length(path), Is.GreaterThan(1200f),
                "30 is reached via A6/D1 and a runway crossing, not a grass cut");
            AssertStaysOnTaxiOrRunway(path, "taxi-out 30");
        }

        [Test]
        public void Vacate_From12And30StaysOnPavementOntoTheBayTaxiIn()
        {
            foreach (var runway in new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 })
            {
                var path = AdelaideCrossRoutes.Vacate(runway);
                var endX = path[path.Length - 2];
                var endZ = path[path.Length - 1];
                // Ends on the E2 → bays corridor, not back at E2 itself: running on to E2 meant
                // driving down the taxi-in route the wrong way and turning 180° to come back.
                Assert.That(Distance(endX, endZ, AdelaideLayout.E2Hold[0], AdelaideLayout.E2Hold[1]),
                    Is.GreaterThan(50f), $"{runway} vacate must not double back to E2");
                foreach (var bay in AdelaideLayout.Bays)
                {
                    var taxiIn = AdelaideGround.TaxiIn(new StableId(bay.Id), AircraftType.Saab340, runway).PoseAt(0);
                    Assert.That(Distance(endX, endZ, taxiIn.X, taxiIn.Z), Is.LessThan(3f),
                        $"{runway} taxi-in to {bay.Reference} must start where the vacate ends");
                }
                AssertStaysOnTaxiOrRunway(path, $"vacate {runway}");
            }
        }

        [Test]
        public void RegionalTaxiOut_UsesTheRoutedPolylines()
        {
            var stand = new StableId("BAY-2");
            var to12 = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway12);
            var to30 = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway30);
            var mid12 = to12.PoseAt(to12.Seconds * 0.55);
            var mid30 = to30.PoseAt(to30.Seconds * 0.55);
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(mid12.X, mid12.Z), Is.LessThan(4f),
                "mid taxi-out 12 left the pavement");
            Assert.That(AirsideAdelaidePavement.DistanceToPavement(mid30.X, mid30.Z), Is.LessThan(4f),
                "mid taxi-out 30 left the pavement");
        }

        private static void AssertStaysOnTaxiOrRunway(float[] path, string name)
        {
            for (var i = 0; i + 1 < path.Length; i += 2)
            {
                var x = path[i];
                var z = path[i + 1];
                var onTaxi = AdelaideTaxiRouter.DistanceToCentreline(x, z) < 18f;
                var onRunway = AirsideAdelaidePavement.DistanceToRunwayPavement(x, z) < 8f;
                var onPavement = AirsideAdelaidePavement.DistanceToPavement(x, z) < 6f;
                Assert.That(onTaxi || onRunway || onPavement, Is.True,
                    $"{name} leaves the taxiways at ({x:0},{z:0})");
            }
        }

        private static float Length(float[] xz)
        {
            var total = 0f;
            for (var i = 0; i + 3 < xz.Length; i += 2)
                total += Distance(xz[i], xz[i + 1], xz[i + 2], xz[i + 3]);
            return total;
        }

        private static float Distance(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
