using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Bug hunt finding 13: on the return departure from an outstation the aircraft flew past the 250 kt calibrated cap
    /// below 10,000 ft (HUD: GS 320 kt / IAS 308 kt at 2,698 ft). The departure model leaves the aircraft several
    /// kilometres behind the route, and easing that gap away in a fixed two minutes lent it the closing speed on top
    /// of its route speed. The ease now lasts as long as the type's speed envelope needs.
    /// </summary>
    public sealed class RegionalDepartureHandoverTests
    {
        private static readonly string[] Strips = { "KGC", "PLO", "WYA", "MGB", "CED", "CPD", "BHQ", "MQL" };

        /// <summary>One return leg modelled as the route does: a straight track home, the real profile and departure.</summary>
        private sealed class Leg
        {
            public AircraftType Type;
            public EnrouteProfile Profile;
            public double Lag, OffsetX, OffsetZ, Gap;
            public double StartX, StartZ, UnitX, UnitZ;

            public (double X, double Z) Route(double t)
            {
                var metres = Profile.DistanceFractionAt(t) * Profile.LegMetres;
                return (StartX + UnitX * metres, StartZ + UnitZ * metres);
            }
        }

        private static Leg For(AircraftType type, string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True);
            Assert.That(RegionalRunways.TryGet(code, out var runway), Is.True);
            var km = destination.DistanceKmTo(DestinationCatalogue.Adelaide);
            var seconds = LegTiming.AirborneSeconds(km, type);
            var leg = new Leg { Type = type, Profile = EnrouteProfile.For(km, seconds, type), Lag = RegionalFlightPath.ClimbLagSeconds(type) };
            YpadFrame.ToWorld(destination.Latitude, destination.Longitude, out leg.StartX, out leg.StartZ);
            var length = Math.Sqrt(leg.StartX * leg.StartX + leg.StartZ * leg.StartZ);
            leg.UnitX = -leg.StartX / length;
            leg.UnitZ = -leg.StartZ / length;
            var exit = RegionalFlightPath.ClimbAltitudeFeet(leg.Profile, RegionalFlightPath.DepartureSeconds, leg.Lag) / EnrouteProfile.FeetPerMetre;
            RegionalFlightPath.Departure(runway, RegionalFlightPath.DepartureSeconds, exit, type, out var sx, out _, out var sz);
            var atExit = leg.Route(RegionalFlightPath.DepartureSeconds);
            leg.OffsetX = sx - atExit.X;
            leg.OffsetZ = sz - atExit.Z;
            leg.Gap = Math.Sqrt(leg.OffsetX * leg.OffsetX + leg.OffsetZ * leg.OffsetZ);
            return leg;
        }

        /// <summary>The most the calibrated airspeed shown by the HUD exceeds the cap at that moment, in knots.</summary>
        private static double Excess(Leg leg, Func<double, double> keep, double until)
        {
            var worst = double.MinValue;
            var before = leg.Route(RegionalFlightPath.DepartureSeconds);
            var k0 = keep(RegionalFlightPath.DepartureSeconds);
            var previous = (X: before.X + leg.OffsetX * k0, Z: before.Z + leg.OffsetZ * k0);
            for (var t = RegionalFlightPath.DepartureSeconds + 1; t <= until; t++)
            {
                var r = leg.Route(t);
                var k = keep(t);
                var position = (X: r.X + leg.OffsetX * k, Z: r.Z + leg.OffsetZ * k);
                var groundKnots = Math.Sqrt(Math.Pow(position.X - previous.X, 2) + Math.Pow(position.Z - previous.Z, 2))
                                  / CircuitProfile.KnotsToMetresPerSecond;
                previous = position;
                var altitude = FlightAtmosphere.AltitudeMetres(RegionalFlightPath.ClimbAltitudeFeet(leg.Profile, t, leg.Lag));
                var cap = FlightSpeedEnvelope.MaximumCasKnots(leg.Type, altitude * EnrouteProfile.FeetPerMetre);
                worst = Math.Max(worst, FlightAtmosphere.CalibratedKnots(groundKnots, altitude) - cap);
            }

            return worst;
        }

        /// <summary>The fixed two-minute smoothstep the hand-over used before the fix.</summary>
        private static double TwoMinutes(double t)
        {
            var b = Math.Max(0.0, Math.Min(1.0, (t - 120.0) / 120.0));
            return 1.0 - b * b * (3.0 - 2.0 * b);
        }

        [Test]
        public void TheOldTwoMinuteEase_PushedTheSaabPastTheCap_TheNewOneDoesNot()
        {
            var worstOld = double.MinValue;
            foreach (var code in Strips)
            {
                var leg = For(AircraftType.Saab340, code);
                var plan = RegionalDepartureHandover.PlanFor(leg.Type, leg.Profile, leg.Lag, leg.Gap);
                var until = plan.EndSeconds + 60;
                worstOld = Math.Max(worstOld, Excess(leg, TwoMinutes, 300));
                Assert.That(Excess(leg, plan.Keep, until), Is.LessThanOrEqualTo(0.5), code + " Saab 340 with the new ease");
            }

            Assert.That(worstOld, Is.GreaterThan(20.0), "the previous fixed ease took the Saab well past 250 kt CAS on the longer legs");
            Assert.That(Excess(For(AircraftType.Saab340, "KGC"), TwoMinutes, 300), Is.GreaterThan(5.0), "and past the cap on the Kingscote return itself");
        }

        [Test]
        public void EveryType_IsNoWorseThanItsOwnRoute_AndNeverWorseThanTheOldEase()
        {
            // Some jets already sit above the cap on the route itself (its speeds follow the unlagged climb); the hand-over
            // must not add to that, and must never be worse than the ease it replaces.
            var types = new[] { AircraftType.Saab340, AircraftType.Atr42, AircraftType.Dash8Q400, AircraftType.Boeing7378, AircraftType.AirbusA320200 };
            foreach (var type in types)
            foreach (var code in Strips)
            {
                DestinationCatalogue.TryFind(code, out var destination);
                if (!type.CanReach(destination.DistanceKmTo(DestinationCatalogue.Adelaide)) || !RouteAccess.Allows(type, destination))
                    continue;
                var leg = For(type, code);
                var plan = RegionalDepartureHandover.PlanFor(leg.Type, leg.Profile, leg.Lag, leg.Gap);
                var until = plan.EndSeconds + 60;
                var routeOnly = Excess(leg, _ => 0.0, until);
                var now = Excess(leg, plan.Keep, until);
                Assert.That(now, Is.LessThanOrEqualTo(Math.Max(routeOnly, 0.0) + 12.0), type.Id + " " + code + " against its own route");
                Assert.That(now, Is.LessThanOrEqualTo(Excess(leg, TwoMinutes, until) + 0.5), type.Id + " " + code + " against the old ease");
            }
        }

        [Test]
        public void ThePlan_StartsFullyCarried_EndsAtTheRoute_AndNeverJumps()
        {
            foreach (var type in new[] { AircraftType.Saab340, AircraftType.Atr42, AircraftType.Dash8Q400 })
            foreach (var code in Strips)
            {
                DestinationCatalogue.TryFind(code, out var destination);
                if (!type.CanReach(destination.DistanceKmTo(DestinationCatalogue.Adelaide)))
                    continue;
                var leg = For(type, code);
                var plan = RegionalDepartureHandover.PlanFor(leg.Type, leg.Profile, leg.Lag, leg.Gap);
                var label = type.Id + " " + code;
                Assert.That(plan.Keep(RegionalFlightPath.DepartureSeconds), Is.EqualTo(1.0).Within(1e-9), label);
                Assert.That(plan.Keep(plan.EndSeconds), Is.EqualTo(0.0).Within(1e-9), label);
                Assert.That(plan.Active(plan.EndSeconds - 1), Is.True, label);
                Assert.That(plan.Active(plan.EndSeconds), Is.False, label);
                Assert.That(plan.EndSeconds, Is.GreaterThanOrEqualTo(RegionalFlightPath.DepartureSeconds + RegionalDepartureHandover.MinimumBlendSeconds), label);
                Assert.That(plan.EndSeconds, Is.LessThan(leg.Profile.LegSeconds - RegionalFlightPath.TerminalSeconds), label + ": over before the landing blend");
                var previous = 1.0;
                for (var t = RegionalFlightPath.DepartureSeconds; t <= plan.EndSeconds + 30; t++)
                {
                    var keep = plan.Keep(t);
                    Assert.That(keep, Is.LessThanOrEqualTo(previous + 1e-9), label + " never grows back");
                    Assert.That(previous - keep, Is.LessThan(0.05), label + " no sudden step in the offset at " + t);
                    previous = keep;
                }
            }
        }

        [Test]
        public void ANegativeOrZeroGap_ProducesAHarmlessPlan()
        {
            var leg = For(AircraftType.Saab340, "KGC");
            var none = RegionalDepartureHandover.PlanFor(leg.Type, leg.Profile, leg.Lag, 0.0);
            Assert.That(none.Keep(200), Is.EqualTo(0.0).Within(1e-9));
            var ahead = RegionalDepartureHandover.PlanFor(leg.Type, leg.Profile, leg.Lag, -400.0);
            Assert.That(ahead.Keep(RegionalFlightPath.DepartureSeconds), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(ahead.Keep(ahead.EndSeconds), Is.EqualTo(0.0).Within(1e-9));
        }
    }
}
