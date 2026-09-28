using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class EnrouteProfileTests
    {
        private static EnrouteProfile For(string code)
        {
            DestinationCatalogue.TryFind(code, out var destination);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(destination);
            return new EnrouteProfile(km, LegTiming.AirborneSeconds(km, AircraftType.Atr42));
        }

        [Test]
        public void Melbourne_CruisesInTheLowTwentiesAtAboutAtrCruiseSpeed()
        {
            var p = For("MEL");
            Assert.That(p.CruiseFeet, Is.InRange(20000, 25000));
            var cruiseKnots = p.GroundSpeedKnotsAt(p.ClimbSeconds + p.CruiseSeconds * 0.5);
            Assert.That(cruiseKnots, Is.InRange(270, 320), "ATR 42-600 cruises near 300 kt");
            Assert.That(p.GroundSpeedKnotsAt(10), Is.LessThan(cruiseKnots));
        }

        [Test]
        public void ShortHop_StaysLowAndStillLevelsOff()
        {
            var p = For("KGC");
            Assert.That(p.CruiseFeet, Is.LessThanOrEqualTo(10000));
            Assert.That(p.CruiseSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void Profile_StartsAndEndsAtTheFieldHandoffHeights()
        {
            var p = For("MEL");
            Assert.That(p.AltitudeFeetAt(0), Is.EqualTo(CircuitProfile.DepartedEndHeight * EnrouteProfile.FeetPerMetre).Within(1));
            Assert.That(p.AltitudeFeetAt(p.LegSeconds), Is.EqualTo(CircuitProfile.ApproachStartHeight * EnrouteProfile.FeetPerMetre).Within(1));
            Assert.That(p.DistanceFractionAt(0), Is.EqualTo(0));
            Assert.That(p.DistanceFractionAt(p.LegSeconds), Is.EqualTo(1).Within(1e-6));
            Assert.That(p.DistanceFractionAt(p.LegSeconds * 0.5), Is.InRange(0.3, 0.7));
        }

        [Test]
        public void ClimbRate_IsRealistic()
        {
            var p = For("MEL");
            var feetPerMinute = (p.AltitudeFeetAt(120) - p.AltitudeFeetAt(60));
            Assert.That(feetPerMinute, Is.EqualTo(EnrouteProfile.ClimbFeetPerMinute).Within(1));
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(p.LegSeconds - 60), Is.LessThan(0));
        }

        [Test]
        public void AltitudeText_UsesFlightLevelsAboveTransition()
        {
            Assert.That(EnrouteProfile.AltitudeText(22040), Is.EqualTo("FL220"));
            Assert.That(EnrouteProfile.AltitudeText(8960), Does.EndWith("ft"));
        }

        [Test]
        public void MapZoom_NotchIsModestAndFlingIsCapped()
        {
            Assert.That(System.Math.Exp(MapZoom.StepFor(-3f)), Is.InRange(1.08, 1.2));
            Assert.That(MapZoom.StepFor(-500f), Is.EqualTo(MapZoom.StepFor(-MapZoom.MaxScrollPerEvent)));
            Assert.That(MapZoom.EaseStep(0.5f, 1f / 60f), Is.InRange(0.05f, 0.15f));
        }

        [Test]
        public void GroundPath_EndZoneSlowsTheLeadInToAStand()
        {
            // Real layout paths are densely sampled; speeds live on the points.
            var xz = new float[42];
            for (var i = 0; i <= 20; i++)
                xz[i * 2] = i * 20f;
            var free = new GroundPath(xz, GroundSpeedLimits.Taxi);
            var zoned = new GroundPath(xz, GroundSpeedLimits.Taxi, 0f, 0f, null,
                new[] { new GroundSpeedZone(45f, CircuitProfile.Knots(5f)) });

            Assert.That(zoned.Seconds, Is.GreaterThan(free.Seconds));
            // 30 m out from the stop it is at walking pace, not braking from straight-taxi speed.
            var near = zoned.SampleAt(zoned.Seconds - 1.0);
            Assert.That(CircuitProfile.ToKnots(near.Speed), Is.LessThanOrEqualTo(5.01f));
            var t = 0.0;
            var mid = 0f;
            for (; t < zoned.Seconds; t += 0.5)
            {
                var s = zoned.SampleAt(t);
                if (s.X > 380f - 45f && s.X < 400f - 45f)
                    mid = System.Math.Max(mid, CircuitProfile.ToKnots(s.Speed));
            }
            Assert.That(mid, Is.LessThanOrEqualTo(GroundSpeedLimits.TurbopropStraightKnots + 0.01f));
        }

        /// <summary>
        /// ADR 0152. The opening arrival bank seeds aircraft already most of the way home — Air NZ
        /// thirty minutes from Auckland, Singapore forty from Changi — and a delivery flight is
        /// given a flat eight minutes. Handed that remainder as though it were the whole leg, the
        /// profile used to solve a cruise speed out of it: Melbourne in twelve minutes reported
        /// about 1 730 kt. No leg time may produce a speed the aeroplane cannot fly.
        /// </summary>
        [Test]
        public void ShortLegTime_NeverReportsASpeedTheAircraftCannotFly()
        {
            foreach (var code in new[] { "MEL", "SYD", "AKL", "SIN", "PLO" })
            {
                Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
                var legKm = DestinationCatalogue.Adelaide.DistanceKmTo(destination);
                foreach (var type in new[] { AircraftType.Atr42, AircraftCatalogue.Boeing737800.Type })
                {
                    var ceiling = EnrouteProfile.MaxCruiseMetresPerSecond(type)
                                  / CircuitProfile.KnotsToMetresPerSecond;
                    foreach (var seconds in new[] { 1.0, 8 * 60.0, 12 * 60.0, 40 * 60.0 })
                    {
                        var profile = new EnrouteProfile(legKm, seconds, type);
                        for (var t = 0.0; t <= seconds; t += Math.Max(1.0, seconds / 10.0))
                            Assert.That(profile.GroundSpeedKnotsAt(t), Is.LessThanOrEqualTo(ceiling + 0.01),
                                $"{type.Id} {code} over {seconds:0} s");
                    }
                }
            }
        }

        [Test]
        public void MaxCruise_IsThePublishedFigureWhereThereIsOne()
        {
            var spec = AircraftCatalogue.Boeing737800;
            var ceiling = EnrouteProfile.MaxCruiseMetresPerSecond(spec.Type) * 3.6;
            Assert.That(ceiling, Is.GreaterThanOrEqualTo(spec.PlanningCruiseKmh));
            if (spec.ManufacturerMaxCruiseKmh > 0)
                Assert.That(ceiling, Is.GreaterThanOrEqualTo(spec.ManufacturerMaxCruiseKmh - 0.01));
            // Not an open-ended allowance: a jet does not cruise at twice its planning speed.
            Assert.That(ceiling, Is.LessThan(spec.PlanningCruiseKmh * 1.35));
        }

        /// <summary>
        /// A leg flown in the time it is planned for still cruises at about the planning speed —
        /// the clamp must not have flattened the normal case.
        /// </summary>
        [Test]
        public void PlannedLegTime_StillCruisesNearThePlanningSpeed()
        {
            Assert.That(DestinationCatalogue.TryFind("SYD", out var sydney), Is.True);
            var legKm = DestinationCatalogue.Adelaide.DistanceKmTo(sydney);
            var type = AircraftCatalogue.Boeing737800.Type;
            var seconds = LegTiming.AirborneSeconds(legKm, type);
            var profile = new EnrouteProfile(legKm, seconds, type);
            var cruiseKnots = profile.GroundSpeedKnotsAt(seconds * 0.5);
            var planningKnots = type.CruiseKmh / 3.6 / CircuitProfile.KnotsToMetresPerSecond;
            Assert.That(cruiseKnots, Is.EqualTo(planningKnots).Within(planningKnots * 0.3));
        }
    }
}
