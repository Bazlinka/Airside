using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DepartureFlightTransitionTests
    {
        private static IEnumerable<AircraftType> Types()
        {
            foreach (var spec in AircraftCatalogue.All)
                if (!spec.Type.IsRotorcraft) yield return spec.Type;
        }

        [TestCaseSource(nameof(Types))]
        public void TakeoffToOutbound_MatchesHeightAndVerticalSpeed(AircraftType type)
        {
            var p = AircraftPerformance.For(type);
            var route = new EnrouteProfile(650, 4800, type);
            const double dt = .001;
            var height = DepartureFlightTransition.TakeoffHeight(p, 1);
            Assert.That(DepartureFlightTransition.OutboundHeight(p, route, 0), Is.EqualTo(height).Within(.0001));
            var before = DepartureFlightTransition.TakeoffHeight(p, 1-dt/p.TakeoffSeconds);
            var after = DepartureFlightTransition.OutboundHeight(p, route, dt);
            Assert.That((height-before)/dt, Is.EqualTo((after-height)/dt).Within(.005));
            Assert.That(height, Is.LessThan(route.StartFeet/EnrouteProfile.FeetPerMetre),
                "Regression: enroute starts at the later climb-out end, not takeoff end");
        }

        [TestCaseSource(nameof(Types))]
        public void ClimboutToRoute_MatchesHeightAndVerticalSpeedAndFinishesAtTimedProfile(AircraftType type)
        {
            var p = AircraftPerformance.For(type);
            foreach (var leg in new[] {(160.0, 1200.0), (650.0, 4800.0), (4000.0, 17000.0)})
            {
                var route = new EnrouteProfile(leg.Item1, leg.Item2, type);
                var d = p.DepartedSeconds;
                const double dt = .001;
                var at = DepartureFlightTransition.OutboundHeight(p, route, d);
                var before = DepartureFlightTransition.OutboundHeight(p, route, d-dt);
                var after = DepartureFlightTransition.OutboundHeight(p, route, d+dt);
                Assert.That(at, Is.EqualTo(p.DepartedEndHeight).Within(.0001));
                Assert.That((at-before)/dt, Is.EqualTo((after-at)/dt).Within(.005));
                var end = d + DepartureFlightTransition.JoinDuration(p, route);
                Assert.That(DepartureFlightTransition.OutboundHeight(p, route, end),
                    Is.EqualTo(route.AltitudeFeetAt(end)/EnrouteProfile.FeetPerMetre).Within(.0001));
                var endBefore = DepartureFlightTransition.OutboundHeight(p, route, end-dt);
                Assert.That((DepartureFlightTransition.OutboundHeight(p, route, end)-endBefore)/dt,
                    Is.EqualTo(route.VerticalSpeedFeetPerMinuteAt(end)/(60*EnrouteProfile.FeetPerMetre)).Within(.005));
                for (var t=0.0; t<=end; t+=.1)
                    Assert.That(DepartureFlightTransition.OutboundHeight(p, route, t+.01),
                        Is.GreaterThanOrEqualTo(DepartureFlightTransition.OutboundHeight(p, route, t)));
            }
        }

        [TestCaseSource(nameof(Types))]
        public void Liftoff_HasZeroSinkAtRotationAndMonotonicHeight(AircraftType type)
        {
            var p = AircraftPerformance.For(type);
            Assert.That(DepartureFlightTransition.TakeoffHeight(p, p.RotateProgress), Is.Zero);
            var dt = .001;
            Assert.That(DepartureFlightTransition.TakeoffHeight(p, p.RotateProgress+dt/p.TakeoffSeconds)/dt,
                Is.InRange(0, .01));
            for (var t=0.0; t<1; t+=.001)
                Assert.That(DepartureFlightTransition.TakeoffHeight(p,t+.001),
                    Is.GreaterThanOrEqualTo(DepartureFlightTransition.TakeoffHeight(p,t)));
        }

        [TestCase(2300, 35)]
        [TestCase(-4000, -160)]
        [TestCase(0, 85)]
        public void WorldAxisCorrection_MatchesPositionAndVelocityAndThenVanishes(double position, double velocity)
        {
            const double dt=.0001;
            var start = DepartureFlightTransition.RouteOffset(0,120,position,velocity);
            Assert.That(start, Is.EqualTo(position));
            Assert.That((DepartureFlightTransition.RouteOffset(dt,120,position,velocity)-start)/dt,
                Is.EqualTo(velocity).Within(.01));
            Assert.That(DepartureFlightTransition.RouteOffset(120,120,position,velocity), Is.Zero);
            Assert.That(DepartureFlightTransition.RouteOffset(180,120,position,velocity), Is.Zero);
            Assert.That(DepartureFlightTransition.RouteOffset(120-dt,120,position,velocity)/dt, Is.EqualTo(0).Within(.01));
        }
    }
}
