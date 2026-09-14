using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Realistic ATR ground speeds on the real Adelaide routes (Bailey 2026-09-14).</summary>
    public sealed class GroundMotionTests
    {
        private const float Kt = 0.514444f;

        [Test]
        public void Speeds_StayWithinAtrGroundLimits()
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                var push = new GroundPath(bay.Pushback, GroundSpeedLimits.Pushback);
                var taxi = new GroundPath(bay.TaxiOut, GroundSpeedLimits.Taxi);
                Assert.That(push.TopSpeed / Kt, Is.LessThanOrEqualTo(2.01f), "pushback is a walking-pace tug");
                Assert.That(taxi.TopSpeed / Kt, Is.LessThanOrEqualTo(15.01f), "taxi is capped at 15 kt");
                Assert.That(taxi.TopSpeed / Kt, Is.GreaterThan(14f), "and reaches it on the straights");
            }

            Assert.That(new GroundPath(AdelaideLayout.Lineup, GroundSpeedLimits.Lineup).TopSpeed / Kt, Is.LessThanOrEqualTo(8.01f));
        }

        [Test]
        public void Turns_AreTakenSlowerThanStraights()
        {
            // Sample the taxi-out and record the slowest moving speed away from the ends.
            var leg = new GroundPath(AdelaideLayout.Bays[0].TaxiOut, GroundSpeedLimits.Taxi);
            var slowest = float.MaxValue;
            for (var t = 60.0; t < leg.Seconds - 60.0; t += 1.0)
                slowest = Math.Min(slowest, leg.SampleAt(t).Speed);
            Assert.That(slowest / Kt, Is.LessThan(10f), "the junction turns slow the aircraft well below 15 kt");
            Assert.That(slowest, Is.GreaterThan(0.5f), "but it does not stop mid-route");
        }

        [Test]
        public void Durations_AreRealisticForAdelaide()
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                var stand = new StableId(bay.Id);
                Assert.That(AirlineOperations.TaxiOutSecondsFrom(stand) / 60.0, Is.InRange(6.0, 13.0), $"{bay.Reference} taxi-out");
                Assert.That(AirlineOperations.TaxiInSecondsTo(stand) / 60.0, Is.InRange(2.5, 7.0), $"{bay.Reference} taxi-in");
            }

            Assert.That(AirlineOperations.VacateSeconds, Is.InRange(60, 180));
            Assert.That(AirlineOperations.LineupSeconds, Is.InRange(25, 90));
        }

        [Test]
        public void Motion_IsContinuousAndMatchesItsSpeed()
        {
            var leg = AdelaideGround.TaxiOut(new StableId("BAY-2"));
            var previous = leg.PoseAt(0);
            var travelled = 0.0;
            for (var t = 0.5; t <= leg.Seconds; t += 0.5)
            {
                var pose = leg.PoseAt(t);
                var step = Math.Sqrt(Math.Pow(pose.X - previous.X, 2) + Math.Pow(pose.Z - previous.Z, 2));
                Assert.That(step, Is.LessThanOrEqualTo(Math.Max(pose.Speed, previous.Speed) * 0.5 + 0.2), $"jump at {t:0.0}s");
                travelled += step;
                previous = pose;
            }

            var push = new GroundPath(AdelaideLayout.Bays[1].Pushback, GroundSpeedLimits.Pushback);
            var taxi = new GroundPath(AdelaideLayout.Bays[1].TaxiOut, GroundSpeedLimits.Taxi);
            Assert.That(travelled, Is.EqualTo(push.Length + taxi.Length).Within(15.0));
        }

        [Test]
        public void Pushback_IsTailFirstThenNoseFirst()
        {
            var leg = AdelaideGround.TaxiOut(new StableId("BAY-1"));
            var pushing = leg.PoseAt(20);
            var later = leg.PoseAt(20.5);
            var travel = (x: later.X - pushing.X, z: later.Z - pushing.Z);
            Assert.That(pushing.TailFirst, Is.True);
            Assert.That(travel.x * pushing.NoseX + travel.z * pushing.NoseZ, Is.LessThan(0f), "moving backwards during the pushback");

            var taxiing = leg.PoseAt(leg.Seconds * 0.5);
            var ahead = leg.PoseAt(leg.Seconds * 0.5 + 0.5);
            Assert.That(taxiing.TailFirst, Is.False);
            Assert.That((ahead.X - taxiing.X) * taxiing.NoseX + (ahead.Z - taxiing.Z) * taxiing.NoseZ, Is.GreaterThan(0f), "nose first once taxiing");

            var bay = AdelaideLayout.Bays[0];
            var parked = leg.PoseAt(0);
            var heading = bay.HeadingDegrees * Math.PI / 180.0;
            Assert.That(parked.NoseX * Math.Sin(heading) + parked.NoseZ * Math.Cos(heading), Is.GreaterThan(0.95), "starts facing the stand heading");
        }
    }
}
