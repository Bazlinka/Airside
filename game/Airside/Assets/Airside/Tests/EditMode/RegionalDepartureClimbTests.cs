using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RegionalDepartureClimbTests
    {
        private static double Height(RegionalRunway runway, AircraftType type, double legKm, double legSeconds, double elapsed)
        {
            var lag = RegionalFlightPath.ClimbLagSeconds(type);
            var profile = EnrouteProfile.For(legKm, legSeconds, type);
            double Route(double at) => RegionalFlightPath.ClimbAltitudeFeet(profile, at, lag) / EnrouteProfile.FeetPerMetre;
            double ExitHeight() => Route(RegionalFlightPath.DepartureSeconds);
            if (elapsed <= RegionalFlightPath.DepartureSeconds)
            {
                RegionalFlightPath.Departure(runway, elapsed, ExitHeight() + 0.0, type, out _, out var y, out _);
                return y;
            }
            return Route(elapsed);
        }

        [TestCase("B738")]
        [TestCase("A320")]
        [TestCase("DH8D")]
        [TestCase("ATR42")]
        public void ClimbOut_NeverExceedsARealisticRateAndHasNoHeightJump(string id)
        {
            var type = id == "ATR42" ? AircraftType.Atr42 : id == "DH8D" ? AircraftType.Dash8Q400
                : id == "A320" ? AircraftType.AirbusA320200 : AircraftType.Boeing7378;
            Assert.IsTrue(RegionalRunways.TryGet("KGC", out var runway));
            var maxRate = 0.0;
            var previous = Height(runway, type, 700, 4200, 0);
            for (var t = 1; t <= 400; t++)
            {
                var h = Height(runway, type, 700, 4200, t);
                var rate = (h - previous) * 196.85; // m/s -> ft/min
                maxRate = Math.Max(maxRate, rate);
                previous = h;
            }
            Assert.LessOrEqual(maxRate, 3000, id + " peak climb rate ft/min");
        }

        [Test]
        public void DepartureClimbHeight_StartsFlatThenClimbsAtOneSteadyRate()
        {
            Assert.AreEqual(0, RegionalFlightPath.DepartureClimbHeight(800, 0, 70), 1e-9);
            Assert.AreEqual(800, RegionalFlightPath.DepartureClimbHeight(800, 70, 70), 1e-6);
            var a = RegionalFlightPath.DepartureClimbHeight(800, 30, 70) - RegionalFlightPath.DepartureClimbHeight(800, 29, 70);
            var b = RegionalFlightPath.DepartureClimbHeight(800, 60, 70) - RegionalFlightPath.DepartureClimbHeight(800, 59, 70);
            Assert.AreEqual(a, b, 1e-9);
        }

        [Test]
        public void ClimbAltitude_LagsTheRouteClimbButConvergesToCruise()
        {
            var type = AircraftType.Boeing7378;
            var profile = EnrouteProfile.For(700, 4200, type);
            var lag = RegionalFlightPath.ClimbLagSeconds(type);
            Assert.Less(RegionalFlightPath.ClimbAltitudeFeet(profile, 100, lag), profile.AltitudeFeetAt(100));
            var after = profile.ClimbSeconds + lag + 1;
            Assert.AreEqual(profile.AltitudeFeetAt(after), RegionalFlightPath.ClimbAltitudeFeet(profile, after, lag), 1e-6);
            Assert.AreEqual(profile.AltitudeFeetAt(100), RegionalFlightPath.ClimbAltitudeFeet(profile, 100, 0), 1e-6);
        }
    }
}
