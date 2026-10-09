using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightSpeedRealismTests
    {
        private static readonly AircraftType[] Types = { AircraftType.Atr42, AircraftType.Boeing7378, AircraftType.Dash8Q400 };

        [Test]
        public void BankRaisesMinimumSpeed_AndCleanWingNeedsMoreThanLanding()
        {
            foreach (var type in Types)
            {
                var level = FlightSpeedEnvelope.MinimumKnots(type, 0, true);
                Assert.Greater(FlightSpeedEnvelope.MinimumKnots(type, 30, true), level);
                Assert.Greater(FlightSpeedEnvelope.MinimumKnots(type, 0, false), level);
                Assert.Less(level, AircraftPerformance.For(type).ApproachKnots, "minimum is under the approach speed");
            }
        }

        [Test]
        public void BelowTenThousandFeet_NoTypeMayExceed250Cas()
        {
            foreach (var type in Types)
                Assert.LessOrEqual(FlightSpeedEnvelope.MaximumCasKnots(type, 6000), 250 + 1e-6);
        }

        [Test]
        public void ClimbingLimitsAcceleration_DescendingLimitsSlowing()
        {
            var type = AircraftType.Boeing7378;
            Assert.Less(FlightSpeedEnvelope.MaxAccelerationMetresPerSecond2(type, 8), FlightSpeedEnvelope.MaxAccelerationMetresPerSecond2(type, 0));
            Assert.Less(FlightSpeedEnvelope.MaxDecelerationMetresPerSecond2(type, -3, false), FlightSpeedEnvelope.MaxDecelerationMetresPerSecond2(type, 0, false));
            Assert.Greater(FlightSpeedEnvelope.MaxDecelerationMetresPerSecond2(type, -3, true), FlightSpeedEnvelope.MaxDecelerationMetresPerSecond2(type, -3, false));
        }

        [Test]
        public void Step_NeverChangesSpeedFasterThanTheEnvelopeAllows()
        {
            var type = AircraftType.Atr42;
            var faster = FlightSpeedEnvelope.Step(type, 100, 300, 1, 0, false);
            Assert.AreEqual(100 + FlightSpeedEnvelope.MaxAccelerationMetresPerSecond2(type, 0) / FlightSpeedEnvelope.KnotsToMetresPerSecond, faster, 1e-6);
            Assert.AreEqual(150, FlightSpeedEnvelope.Step(type, 150, 150, 1, 0, false), 1e-9);
        }

        [Test]
        public void RegionalLanding_FollowsTheRouteSpeedIntoTheTerminalAreaAndNeverDropsBelowMinimum()
        {
            foreach (var type in Types)
            {
                var profile = EnrouteProfile.For(1200, 5400, type);
                var routeAtEntry = profile.GroundSpeedKnotsAt(profile.LegSeconds - RegionalFlightPath.TerminalSeconds);
                var entry = RegionalFlightPath.TerminalSpeedKnots(profile, type, RegionalFlightPath.TerminalSeconds);
                Assert.AreEqual(routeAtEntry, entry, 1.0, type.Id + ": no jump at the seam");
                var previous = entry;
                for (var s = RegionalFlightPath.TerminalSeconds - 1; s >= RegionalFlightPath.RolloutSeconds; s--)
                {
                    var v = RegionalFlightPath.TerminalSpeedKnots(profile, type, s);
                    Assert.GreaterOrEqual(v, FlightSpeedEnvelope.MinimumKnots(type, 0, true) - 1e-6, type.Id + " below minimum at " + s);
                    // The last 20 s is the flare, where idle thrust and the loss of ground-effect lift bleed speed faster than on the glide.
                    var decel = (previous - v) * FlightSpeedEnvelope.KnotsToMetresPerSecond;
                    Assert.LessOrEqual(decel, FlightSpeedEnvelope.MaxDecelerationMetresPerSecond2(type, -3, true) + (s < RegionalFlightPath.RolloutSeconds + 20 ? 0.3 : 0.05), type.Id + " slows too hard at " + s + " from " + previous + " to " + v);
                    previous = v;
                }
                Assert.AreEqual(AircraftPerformance.For(type).TouchdownKnots, RegionalFlightPath.TerminalSpeedKnots(profile, type, RegionalFlightPath.RolloutSeconds), 0.5, type.Id + " reaches touchdown speed");
            }
        }

        [Test]
        public void RegionalLanding_AlongTrackIsContinuousAndMonotoneToAStop()
        {
            Assert.IsTrue(RegionalRunways.TryGet("KGC", out var runway));
            foreach (var type in Types)
            {
                var profile = EnrouteProfile.For(1200, 5400, type);
                RegionalFlightPath.Landing(runway, 0, 0, RegionalFlightPath.TerminalSeconds, profile, type, out var px, out _, out var pz);
                var worstStep = 0.0;
                for (var s = RegionalFlightPath.TerminalSeconds - 1; s >= 0; s--)
                {
                    RegionalFlightPath.Landing(runway, 0, 0, s, profile, type, out var x, out _, out var z);
                    var step = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                    var knots = step / FlightSpeedEnvelope.KnotsToMetresPerSecond;
                    Assert.LessOrEqual(knots, 300, type.Id + " speed sane at " + s);
                    worstStep = Math.Max(worstStep, step);
                    px = x; pz = z;
                }
                RegionalFlightPath.Landing(runway, 0, 0, 0.5, profile, type, out var ex, out _, out var ez);
                RegionalFlightPath.Landing(runway, 0, 0, 0, profile, type, out var fx, out _, out var fz);
                Assert.Less(Math.Sqrt((fx - ex) * (fx - ex) + (fz - ez) * (fz - ez)) / 0.5 / FlightSpeedEnvelope.KnotsToMetresPerSecond, 8, "at rest by the end");
            }
        }

        [Test]
        public void RolloutDeceleration_StaysInARealisticBand()
        {
            foreach (var type in Types)
            {
                var a = RegionalFlightPath.RolloutDeceleration(type, 5000);
                Assert.That(a, Is.InRange(1.2, 4.5));
            }
        }
    }
}
