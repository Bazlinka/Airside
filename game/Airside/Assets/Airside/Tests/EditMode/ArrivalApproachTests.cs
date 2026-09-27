using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0142 — arrivals drawn from 32 km, joining the final from their origin's side.</summary>
    public sealed class ArrivalApproachTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var d), Is.True, code);
            return d;
        }

        [Test]
        public void ArrivalsShowFurtherOut_AndInsideTheFarClip()
        {
            Assert.That(ArrivalApproach.ShowMetres, Is.EqualTo(32_000f));
            // The joining curve's corner is still inside the 30 km far clip from the airport centre.
            var far = Math.Sqrt(Math.Pow(ArrivalApproach.ShowMetres - 1_500f, 2) + Math.Pow(ArrivalApproach.JoinOffsetMetres, 2));
            Assert.That(far, Is.LessThan(AirsideBareField.CameraFarClip + 2_500f));
            Assert.That(AirsideBareField.CameraFarClip, Is.EqualTo(30_000f));
        }

        [Test]
        public void TheHeightIsTheGlideslope_CappedAt6000Feet()
        {
            Assert.That(ArrivalApproach.Height(300f), Is.EqualTo(300f));
            Assert.That(ArrivalApproach.Height(5_000f), Is.EqualTo(ArrivalApproach.CapMetres));
            Assert.That(ArrivalApproach.CapMetres / 0.3048f, Is.EqualTo(6_000f).Within(10f));
            // At the show point the 3° path is under the cap: the aircraft is descending, not level.
            var tan3 = (float)Math.Tan(3.0 * Math.PI / 180.0);
            Assert.That(ArrivalApproach.ShowMetres * tan3, Is.LessThan(ArrivalApproach.CapMetres));
        }

        [Test]
        public void TheJoiningCurve_MeetsTheFinalWithoutAKink()
        {
            Assert.That(ArrivalApproach.LateralOffset(ArrivalApproach.StraightInMetres, 1f), Is.Zero);
            Assert.That(ArrivalApproach.LateralOffset(5_000f, 1f), Is.Zero);
            var justOut = ArrivalApproach.LateralOffset(ArrivalApproach.StraightInMetres + 100f, 1f);
            Assert.That(justOut, Is.LessThan(1f), "zero slope where it joins");
            Assert.That(ArrivalApproach.LateralOffset(ArrivalApproach.ShowMetres, -1f), Is.EqualTo(-ArrivalApproach.JoinOffsetMetres));
            // Continuous everywhere: no step bigger than the curve's slope allows.
            var previous = 0f;
            for (var m = 0f; m <= ArrivalApproach.ShowMetres; m += 50f)
            {
                var offset = ArrivalApproach.LateralOffset(m, 1f);
                Assert.That(Math.Abs(offset - previous), Is.LessThan(40f), $"at {m} m");
                previous = offset;
            }
        }

        [Test]
        public void AnArrival_JoinsFromItsOriginsSide()
        {
            var adl = DestinationCatalogue.Adelaide;
            // Runway 05 lands north-east (50°), so the final points south-west (230°): Kingscote (~222°)
            // is nearly straight behind it.
            var fromKingscote = ArrivalApproach.LateralFactor(ArrivalApproach.Bearing(adl, Code("KGC")), 50);
            Assert.That(Math.Abs(fromKingscote), Is.LessThan(0.3f), "nearly straight in from behind");
            // Sydney is ahead (east-north-east): it swings round from one side, fully.
            var fromSydney = ArrivalApproach.LateralFactor(ArrivalApproach.Bearing(adl, Code("SYD")), 50);
            Assert.That(Math.Abs(fromSydney), Is.EqualTo(1f));
            // Perth (west) and Melbourne (south-east) come from opposite sides of the 05 final.
            var fromPerth = ArrivalApproach.LateralFactor(ArrivalApproach.Bearing(adl, Code("PER")), 50);
            var fromMelbourne = ArrivalApproach.LateralFactor(ArrivalApproach.Bearing(adl, Code("MEL")), 50);
            var fromDarwin = ArrivalApproach.LateralFactor(ArrivalApproach.Bearing(adl, Code("DRW")), 50);
            Assert.That(Math.Sign(fromPerth), Is.Not.EqualTo(Math.Sign(fromMelbourne)));
            // +z is left of the landing direction: Darwin (north-west of a north-east landing) is left.
            Assert.That(fromDarwin, Is.GreaterThan(0f));
        }

        [Test]
        public void Bearing_IsTrueNorthBased()
        {
            var adl = DestinationCatalogue.Adelaide;
            Assert.That(ArrivalApproach.Bearing(adl, Code("DRW")), Is.InRange(335.0, 350.0));
            Assert.That(ArrivalApproach.Bearing(adl, Code("MEL")), Is.InRange(110.0, 130.0));
            Assert.That(ArrivalApproach.Bearing(adl, Code("PER")), Is.InRange(260.0, 280.0));
        }

        [Test]
        public void TheDistantLight_OnlyShowsFarOut()
        {
            Assert.That(ArrivalApproach.BeaconStrength(2_000f), Is.Zero);
            Assert.That(ArrivalApproach.BeaconStrength(ArrivalApproach.BeaconFromMetres), Is.Zero);
            Assert.That(ArrivalApproach.BeaconStrength(9_000f), Is.InRange(0.1f, 0.9f));
            Assert.That(ArrivalApproach.BeaconStrength(20_000f), Is.EqualTo(1f));
        }
    }
}
