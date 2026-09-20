using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class TaxiRunwayNightTests
    {
        [Test]
        public void Pushback_TurnsOntoTheTaxiHeadingInsteadOfSnapping180()
        {
            var stand = new StableId("BAY-1");
            var parked = AdelaideGround.StandPose(stand);
            var leg = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway05);
            var push = leg.Parts[0];
            var start = leg.PoseAt(0);
            var early = leg.PoseAt(push.Path.Seconds * 0.2);
            var pushed = leg.PoseAt(push.Path.Seconds);
            var taxi = leg.PoseAt(push.Seconds + AdelaideGround.TugDisconnectSeconds + 1.0);

            Assert.That(HeadingDegrees(start.NoseX, start.NoseZ, parked.NoseX, parked.NoseZ),
                Is.LessThan(8f), "pushback starts facing the stand");
            Assert.That(HeadingDegrees(early.NoseX, early.NoseZ, parked.NoseX, parked.NoseZ),
                Is.LessThan(20f), "the tug has not flipped the nose in the first metres");
            Assert.That(HeadingDegrees(pushed.NoseX, pushed.NoseZ, taxi.NoseX, taxi.NoseZ),
                Is.LessThan(25f), "tug disconnect is a turnout, not a 180 snap");

            var previous = start;
            for (var t = 2.0; t <= push.Path.Seconds; t += 2.0)
            {
                var pose = leg.PoseAt(t);
                Assert.That(HeadingDegrees(previous.NoseX, previous.NoseZ, pose.NoseX, pose.NoseZ),
                    Is.LessThan(40f), $"heading jumped at t={t:0}");
                previous = pose;
            }
        }

        [Test]
        public void RegionalTaxiOut_ReachesThe12And30Holds()
        {
            var stand = new StableId("BAY-2");
            var to12 = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway12);
            var to30 = AdelaideGround.TaxiOut(stand, AircraftType.Atr42, RunwayDirection.Runway30);
            var end12 = to12.PoseAt(to12.Seconds);
            var end30 = to30.PoseAt(to30.Seconds);
            Assert.That(Distance(end12.X, end12.Z, AdelaideCrossRoutes.Hold12[0], AdelaideCrossRoutes.Hold12[1]),
                Is.LessThan(8f), "taxi-out 12 ends at the 12 hold");
            Assert.That(Distance(end30.X, end30.Z, AdelaideCrossRoutes.Hold30[0], AdelaideCrossRoutes.Hold30[1]),
                Is.LessThan(8f), "taxi-out 30 ends at the 30 hold");
        }

        [Test]
        public void LineupAndVacate_JoinTheCrossStripToE2()
        {
            var lineup12 = AdelaideGround.LineupFor(RunwayDirection.Runway12);
            var lineup30 = AdelaideGround.LineupFor(RunwayDirection.Runway30);
            var start12 = lineup12.PoseAt(0);
            var start30 = lineup30.PoseAt(0);
            Assert.That(Distance(start12.X, start12.Z, AdelaideCrossRoutes.Hold12[0], AdelaideCrossRoutes.Hold12[1]),
                Is.LessThan(4f));
            Assert.That(Distance(start30.X, start30.Z, AdelaideCrossRoutes.Hold30[0], AdelaideCrossRoutes.Hold30[1]),
                Is.LessThan(4f));

            var vacate12 = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway12);
            var vacate30 = AdelaideGround.VacateFor(AircraftType.Atr42, RunwayDirection.Runway30);
            var e2 = AdelaideLayout.E2Hold;
            var end12 = vacate12.PoseAt(vacate12.Seconds);
            var end30 = vacate30.PoseAt(vacate30.Seconds);
            Assert.That(Distance(end12.X, end12.Z, e2[0], e2[1]), Is.LessThan(6f));
            Assert.That(Distance(end30.X, end30.Z, e2[0], e2[1]), Is.LessThan(6f));
        }

        [Test]
        public void RunwayFrame_Flips23AndRotatesTheCrossStrip()
        {
            RunwayFrame.ToWorld(RunwayDirection.Runway05, 100f, 1f, 8f, out var x05, out var y05, out var z05);
            Assert.That(x05, Is.EqualTo(100f));
            Assert.That(y05, Is.EqualTo(1f));
            Assert.That(z05, Is.EqualTo(8f));

            RunwayFrame.ToWorld(RunwayDirection.Runway23, 100f, 1f, 8f, out var x23, out _, out var z23);
            Assert.That(x23, Is.EqualTo(-100f));
            Assert.That(z23, Is.EqualTo(-8f));

            AdelaideCrossRoutes.LocalToWorld(-AdelaideCrossRoutes.HalfLength, 0f,
                out var threshold12X, out var threshold12Z);
            RunwayFrame.ToWorld(RunwayDirection.Runway12, CircuitProfile.WestThresholdX, 0f, 0f,
                out var x12, out _, out var z12);
            Assert.That(Distance(x12, z12, threshold12X, threshold12Z), Is.LessThan(0.5f),
                "05 west threshold maps to the 12 threshold, not a squeezed mid-strip");

            RunwayFrame.ToWorld(RunwayDirection.Runway12, CircuitProfile.WestThresholdX, 0f, 0f,
                out var t12x, out _, out var t12z);
            RunwayFrame.ToWorld(RunwayDirection.Runway30, CircuitProfile.WestThresholdX, 0f, 0f,
                out var t30x, out _, out var t30z);
            Assert.That(Distance(t12x, t12z, t30x, t30z), Is.GreaterThan(1400f),
                "12 and 30 thresholds sit at opposite ends");

            RunwayFrame.Forward(RunwayDirection.Runway05, out var f05x, out var f05z);
            RunwayFrame.Forward(RunwayDirection.Runway23, out var f23x, out var f23z);
            Assert.That(f05x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(f05z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(f23x, Is.EqualTo(-1f).Within(0.01f));
            Assert.That(f23z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void DesignationNumerals_ArePaintedOnBothStrips()
        {
            var main = AirsideRunwayMarkings.DesignationNumerals();
            Assert.That(main.Length, Is.GreaterThan(8), "05 and 23 are more than a pair of bars");
            foreach (var mark in main)
                Assert.That(mark.OnPavement, Is.True, $"05/23 number at {mark.CenterX},{mark.CenterZ}");

            var cross = AirsideStripMarkings.DesignationNumerals(
                AirsideAdelaidePavement.CrossLengthMetres, "12", "30");
            Assert.That(cross.Length, Is.GreaterThan(8));
            var halfL = AirsideAdelaidePavement.CrossHalfLength;
            var halfW = AirsideAdelaidePavement.CrossHalfWidth;
            foreach (var mark in cross)
            {
                Assert.That(Math.Abs(mark.MinX), Is.LessThanOrEqualTo(halfL + 0.01f));
                Assert.That(Math.Abs(mark.MaxX), Is.LessThanOrEqualTo(halfL + 0.01f));
                Assert.That(Math.Abs(mark.MinZ), Is.LessThanOrEqualTo(halfW + 0.01f));
                Assert.That(Math.Abs(mark.MaxZ), Is.LessThanOrEqualTo(halfW + 0.01f));
            }
        }

        [Test]
        public void TaxiCorners_AreFilletedLikeAHumanTurn()
        {
            Assert.That(TaxiVisualPath.MaxCornerFillet, Is.GreaterThanOrEqualTo(16f));
            Assert.That(GroundPathSmoothing.DefaultFilletMetres, Is.GreaterThanOrEqualTo(18f));
        }

        private static float HeadingDegrees(float ax, float az, float bx, float bz)
        {
            var a = Math.Atan2(ax, az);
            var b = Math.Atan2(bx, bz);
            var d = Math.Abs(a - b);
            if (d > Math.PI)
                d = 2 * Math.PI - d;
            return (float)(d * 180.0 / Math.PI);
        }

        private static float Distance(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
