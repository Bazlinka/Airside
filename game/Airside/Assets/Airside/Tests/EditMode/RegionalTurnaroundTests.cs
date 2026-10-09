using System;
using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Bug hunt finding 8: at Kingscote the aircraft stayed at the end of the landing roll for the whole turnaround. It now
    /// taxis to the apron, waits, and taxis back to where its departure roll starts, with no jump at either end.
    /// </summary>
    public sealed class RegionalTurnaroundTests
    {
        private static TurnaroundSpot ApronBeside(double stopX, double stopZ)
        {
            // An apron of a couple of hundred metres, 700 m across the field from the stop, facing a terminal to its north.
            var cx = stopX + 450.0;
            var cz = stopZ + 520.0;
            return RegionalTurnaround.Spot(
                new[] { cx - 90, cx + 90, cx + 90, cx - 90 }, new[] { cz - 40, cz - 40, cz + 40, cz + 40 },
                new[] { cx - 20, cx + 20 }, new[] { cz + 120, cz + 120 }, 0.0);
        }

        private static double Distance(double ax, double az, double bx, double bz) =>
            Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        private static double YawGap(double a, double b) => Math.Abs(((a - b + 540.0) % 360.0) - 180.0);

        [TestCase("KGC")]
        [TestCase("PLO")]
        [TestCase("CED")]
        public void TurnaroundBeginsAtTheLandingStop_AndEndsAtTheDepartureStart_WithNoJump(string code)
        {
            Assert.That(RegionalRunways.TryGet(code, out var runway), Is.True);
            var type = AircraftType.Saab340;
            RegionalFlightPath.Landing(runway, 0, 0, 0, null, type, out var stopX, out _, out var stopZ);
            RegionalFlightPath.Departure(runway, 0, 0, type, out var startX, out _, out var startZ);
            RegionalTurnaround.RunwayYaws(runway, out var landingYaw, out var departureYaw);
            var spot = ApronBeside(stopX, stopZ);
            const double duration = 40 * 60;

            var first = RegionalTurnaround.At(stopX, stopZ, landingYaw, startX, startZ, departureYaw, spot, 0, duration);
            Assert.That(Distance(first.X, first.Z, stopX, stopZ), Is.LessThan(0.01), "starts where the landing roll stopped");
            Assert.That(YawGap(first.YawDegrees, landingYaw), Is.LessThan(0.1));
            Assert.That(first.Leg, Is.EqualTo(TurnaroundLeg.TaxiIn));

            var last = RegionalTurnaround.At(stopX, stopZ, landingYaw, startX, startZ, departureYaw, spot, duration, duration);
            Assert.That(Distance(last.X, last.Z, startX, startZ), Is.LessThan(0.01), "ends where the departure roll starts");
            Assert.That(YawGap(last.YawDegrees, departureYaw), Is.LessThan(0.1), "facing the way the departure rolls");
            Assert.That(last.Leg, Is.EqualTo(TurnaroundLeg.TaxiOut));

            var parked = 0;
            var maxStep = 0.0;
            var maxYawRate = 0.0;
            var previous = first;
            for (var t = 0.5; t <= duration; t += 0.5)
            {
                var p = RegionalTurnaround.At(stopX, stopZ, landingYaw, startX, startZ, departureYaw, spot, t, duration);
                maxStep = Math.Max(maxStep, Distance(p.X, p.Z, previous.X, previous.Z));
                maxYawRate = Math.Max(maxYawRate, YawGap(p.YawDegrees, previous.YawDegrees) / 0.5);
                if (p.Leg == TurnaroundLeg.Parked)
                {
                    parked++;
                    Assert.That(Distance(p.X, p.Z, spot.X, spot.Z), Is.LessThan(0.01));
                    Assert.That(p.SpeedMetresPerSecond, Is.EqualTo(0.0));
                }

                previous = p;
            }

            Assert.That(parked * 0.5, Is.GreaterThan(duration * 0.8), "most of the stay is spent parked on the apron");
            Assert.That(maxStep / 0.5, Is.LessThan(12.0), "taxi speed stays believable (m/s)");
            Assert.That(maxYawRate, Is.LessThan(25.0), "the nose never swings faster than a taxiing aircraft can turn (deg/s)");
        }

        [Test]
        public void TheTaxiPath_ReachesAnyPose_WithTheRequestedHeading_AndNoShorterThanAStraightLine()
        {
            var random = new Random(8);
            for (var i = 0; i < 200; i++)
            {
                var ax = random.NextDouble() * 400 - 200;
                var az = random.NextDouble() * 400 - 200;
                var bx = random.NextDouble() * 1200 - 600;
                var bz = random.NextDouble() * 1200 - 600;
                var ayaw = random.NextDouble() * 360;
                var byaw = random.NextDouble() * 360;
                var spot = new TurnaroundSpot(bx, bz, byaw);
                // A one-hour stay so both taxi legs are uncapped: sample the very ends of the first leg.
                var start = RegionalTurnaround.At(ax, az, ayaw, ax, az, ayaw, spot, 0, 3600);
                Assert.That(Distance(start.X, start.Z, ax, az), Is.LessThan(0.01), "pose " + i);
                var parkedAt = RegionalTurnaround.At(ax, az, ayaw, ax, az, ayaw, spot, 1800, 3600);
                Assert.That(Distance(parkedAt.X, parkedAt.Z, bx, bz), Is.LessThan(0.01), "pose " + i);
                Assert.That(YawGap(parkedAt.YawDegrees, byaw), Is.LessThan(0.1), "pose " + i);
                // The leg is complete a little before the end of its allowance: it must have arrived by then.
                var arrived = RegionalTurnaround.At(ax, az, ayaw, ax, az, ayaw, spot, 600, 3600);
                Assert.That(arrived.Leg, Is.EqualTo(TurnaroundLeg.Parked), "pose " + i);
            }
        }

        [Test]
        public void AShortStay_StillParksAndNeverProducesNonsense()
        {
            var spot = ApronBeside(0, 0);
            foreach (var duration in new[] { 1.0, 30.0, 120.0, 300.0 })
            for (var t = 0.0; t <= duration; t += Math.Max(0.25, duration / 40))
            {
                var p = RegionalTurnaround.At(0, 0, 30, 5, 5, 210, spot, t, duration);
                Assert.That(double.IsNaN(p.X) || double.IsNaN(p.Z) || double.IsNaN(p.YawDegrees), Is.False, duration + " " + t);
                Assert.That(Distance(p.X, p.Z, 0, 0), Is.LessThan(2000.0), duration + " " + t);
            }

            Assert.That(RegionalTurnaround.At(0, 0, 30, 5, 5, 210, spot, 150, 300).Leg, Is.EqualTo(TurnaroundLeg.Parked));
        }

        [Test]
        public void TheParkingSpot_IsTheApronCentre_NoseTowardTheNearestTerminalPoint()
        {
            var spot = RegionalTurnaround.Spot(new[] { 0.0, 100, 100, 0 }, new[] { 0.0, 0, 60, 60 },
                new[] { 40.0, 60 }, new[] { 200.0, 200 }, 77);
            Assert.That(spot.X, Is.EqualTo(50.0).Within(1e-9));
            Assert.That(spot.Z, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(YawGap(spot.YawDegrees, 0.0), Is.LessThan(10.0), "terminal is straight ahead (+Z)");
            var without = RegionalTurnaround.Spot(new[] { 0.0, 10, 10 }, new[] { 0.0, 0, 10 }, null, null, 77);
            Assert.That(without.YawDegrees, Is.EqualTo(77.0).Within(1e-9), "no terminal: parallel to the runway");
        }
    }
}
