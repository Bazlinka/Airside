using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Verified ground speeds on the real Adelaide routes.</summary>
    public sealed class GroundMotionTests
    {
        private const float Kt = CircuitProfile.KnotsToMetresPerSecond;

        [Test]
        public void Speeds_MatchVerifiedTurbopropAndJetBands()
        {
            Assert.That(GroundSpeedLimits.TurbopropStraightKnots, Is.EqualTo(25f));
            Assert.That(GroundSpeedLimits.JetStraightKnots, Is.EqualTo(25f));
            Assert.That(GroundSpeedLimits.JetNormalTaxiKnots, Is.EqualTo(20f));
            Assert.That(GroundSpeedLimits.ApronTurbopropKnots, Is.EqualTo(15f));
            Assert.That(GroundSpeedLimits.ApronJetKnots, Is.EqualTo(10f));
            Assert.That(GroundSpeedLimits.PushbackKnots, Is.EqualTo(3f));
            Assert.That(GroundSpeedLimits.LineupKnots, Is.EqualTo(10f));
            Assert.That(GroundSpeedLimits.TurnKnotsAtRadiusMetres(45f), Is.EqualTo(10f).Within(0.3f));

            foreach (var bay in AdelaideLayout.Bays)
            {
                var push = new GroundPath(bay.Pushback, GroundSpeedLimits.Pushback);
                var taxi = new GroundPath(bay.TaxiOut, GroundSpeedLimits.TaxiTurboprop);
                Assert.That(push.TopSpeed / Kt, Is.LessThanOrEqualTo(3.01f), "pushback stays a walking-pace tug");
                Assert.That(taxi.TopSpeed / Kt, Is.LessThanOrEqualTo(25.01f), "turboprop taxi capped at 25 kt");
                Assert.That(taxi.TopSpeed / Kt, Is.GreaterThan(20f), "and reaches a real straight-taxi pace");
            }

            foreach (var gate in AdelaideLayout.TerminalGates)
            {
                var taxi = new GroundPath(gate.TaxiOut, GroundSpeedLimits.TaxiJet);
                Assert.That(taxi.TopSpeed / Kt, Is.LessThanOrEqualTo(25.01f));
                // Gate routes are short and curved — they may not reach the full straight
                // band, but they must clear the old 15 kt crawl.
                Assert.That(taxi.TopSpeed / Kt, Is.GreaterThan(15f));
            }

            Assert.That(new GroundPath(AdelaideLayout.Lineup, GroundSpeedLimits.Lineup).TopSpeed / Kt,
                Is.LessThanOrEqualTo(10.01f));
        }

        [Test]
        public void Turns_AreTakenNearTenKnotsNotStraightTaxi()
        {
            var leg = new GroundPath(AdelaideLayout.Bays[0].TaxiOut, GroundSpeedLimits.TaxiTurboprop);
            var slowest = float.MaxValue;
            for (var t = 60.0; t < leg.Seconds - 60.0; t += 1.0)
                slowest = Math.Min(slowest, leg.SampleAt(t).Speed);
            Assert.That(slowest / Kt, Is.LessThan(12f), "junction turns sit near the 10 kt band");
            Assert.That(slowest / Kt, Is.GreaterThan(5f), "but not a crawl");
            Assert.That(slowest, Is.GreaterThan(0.5f), "and it does not stop mid-route");
        }

        [Test]
        public void ApronAndStandZones_AreSlowerThanTheStraight()
        {
            var bay = AdelaideLayout.Bays[0];
            var outPath = new GroundPath(bay.TaxiOut, GroundSpeedLimits.TaxiTurboprop, 0f, 0f,
                new[] { new GroundSpeedZone(GroundSpeedLimits.ApronMetres,
                    CircuitProfile.Knots(GroundSpeedLimits.ApronTurbopropKnots)) }, null);
            var early = outPath.SampleAt(Math.Min(20.0, outPath.Seconds * 0.1));
            Assert.That(CircuitProfile.ToKnots(early.Speed),
                Is.LessThanOrEqualTo(GroundSpeedLimits.ApronTurbopropKnots + 0.2f));

            var inPath = new GroundPath(bay.TaxiIn, GroundSpeedLimits.TaxiTurboprop, 0f, 0f, null,
                new[]
                {
                    new GroundSpeedZone(GroundSpeedLimits.ApronMetres,
                        CircuitProfile.Knots(GroundSpeedLimits.ApronTurbopropKnots)),
                    new GroundSpeedZone(GroundSpeedLimits.StandLeadInMetres,
                        CircuitProfile.Knots(GroundSpeedLimits.StandLeadInKnots))
                });
            var nearStand = inPath.SampleAt(Math.Max(0.0, inPath.Seconds - 2.0));
            Assert.That(CircuitProfile.ToKnots(nearStand.Speed),
                Is.LessThanOrEqualTo(GroundSpeedLimits.StandLeadInKnots + 0.2f));
        }

        [Test]
        public void Durations_AreRealisticForAdelaide()
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                var stand = new StableId(bay.Id);
                // Faster straight taxi shortens the old 6–13 min band; pushback + disconnect remain.
                Assert.That(AirlineOperations.TaxiOutSecondsFrom(stand) / 60.0, Is.InRange(4.0, 11.0), $"{bay.Reference} taxi-out");
                Assert.That(AirlineOperations.TaxiInSecondsTo(stand) / 60.0, Is.InRange(1.5, 6.0), $"{bay.Reference} taxi-in");
            }

            Assert.That(AirlineOperations.VacateSeconds, Is.InRange(45, 180));
            Assert.That(AirlineOperations.LineupSeconds, Is.InRange(20, 90));
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
            var taxi = new GroundPath(AdelaideLayout.Bays[1].TaxiOut, GroundSpeedLimits.TaxiTurboprop);
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

        [Test]
        public void StandClass_SelectsTheMatchingTaxiProfile()
        {
            Assert.That(GroundSpeedLimits.TaxiFor(StandClass.RegionalBay).MaxSpeed,
                Is.EqualTo(GroundSpeedLimits.TaxiTurboprop.MaxSpeed));
            Assert.That(GroundSpeedLimits.TaxiFor(StandClass.TerminalGate).MaxSpeed,
                Is.EqualTo(GroundSpeedLimits.TaxiJet.MaxSpeed));
            Assert.That(GroundSpeedLimits.ApronKnotsFor(StandClass.RegionalBay),
                Is.EqualTo(GroundSpeedLimits.ApronTurbopropKnots));
            Assert.That(GroundSpeedLimits.ApronKnotsFor(StandClass.TerminalGate),
                Is.EqualTo(GroundSpeedLimits.ApronJetKnots));
        }
    }
}
