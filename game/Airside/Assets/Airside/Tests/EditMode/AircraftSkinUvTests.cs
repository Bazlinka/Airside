using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The aircraft skin can only carry real frame, stringer and rivet spacing if every part
    /// of every airframe is unwrapped at the same texel density. The generic kit unwrap
    /// normalised each part's own bounding box to 0..1, so a 39 m fuselage and a 0.3 m window
    /// differed by over a hundred times and no authored skin could read as panels.
    /// </summary>
    public sealed class AircraftSkinUvTests
    {
        // Real parts, in metres, taken from the shipped kits.
        private const float FuselageLength = 39.47f;   // 737-8 class
        private const float FuselageDiameter = 3.76f;
        private const float WindowHeight = 0.36f;
        private const float WindowWidth = 0.26f;

        [Test]
        public void TheLongestAxisIsTheOneUnwrappedAlong()
        {
            // A fuselage is unwrapped along its length, not across it. The old planar unwrap
            // chose the two *smallest* axes, collapsing the whole 39 m onto one UV.
            Assert.That(AircraftSkinUv.LongestAxis(FuselageDiameter, FuselageDiameter, FuselageLength),
                Is.EqualTo(2));
            Assert.That(AircraftSkinUv.LongestAxis(35.92f, 0.52f, 7.60f), Is.EqualTo(0), "a wing runs along X");
            Assert.That(AircraftSkinUv.LongestAxis(0.4f, 2.30f, 0.4f), Is.EqualTo(1), "a gear leg stands in Y");
        }

        [Test]
        public void VRunsInMetresAlongThePart()
        {
            var radius = AircraftSkinUv.ArcRadiusMetres(FuselageDiameter, FuselageDiameter);
            AircraftSkinUv.Unwrap(-19.735f, FuselageDiameter / 2f, 0f, radius, out _, out var nose);
            AircraftSkinUv.Unwrap(19.735f, FuselageDiameter / 2f, 0f, radius, out _, out var tail);

            Assert.That(tail - nose, Is.EqualTo(FuselageLength).Within(0.01f),
                "nose to tail must span the fuselage's real length in UV metres");
        }

        [Test]
        public void UIsArcLengthInMetresAroundThePart()
        {
            var radius = AircraftSkinUv.ArcRadiusMetres(FuselageDiameter, FuselageDiameter);
            // Half a turn around the tube is half its circumference.
            AircraftSkinUv.Unwrap(0f, radius, 0f, radius, out var atZero, out _);
            AircraftSkinUv.Unwrap(0f, 0f, radius, radius, out var atQuarter, out _);

            var quarterTurn = Math.Abs(atQuarter - atZero);
            Assert.That(quarterTurn, Is.EqualTo(Math.PI / 2.0 * radius).Within(0.01),
                "a quarter turn must measure a quarter of the circumference, in metres");
        }

        /// <summary>
        /// The contract that makes one tiling skin work across a whole airframe: a metre of
        /// surface is a metre of UV, whatever part it belongs to.
        /// </summary>
        [Test]
        public void TexelDensityMatchesBetweenTheFuselageAndACabinWindow()
        {
            var fuselageRadius = AircraftSkinUv.ArcRadiusMetres(FuselageDiameter, FuselageDiameter);
            AircraftSkinUv.Unwrap(0f, FuselageDiameter / 2f, 0f, fuselageRadius, out _, out var fuseA);
            AircraftSkinUv.Unwrap(1f, FuselageDiameter / 2f, 0f, fuselageRadius, out _, out var fuseB);
            var fuselageUvPerMetre = Math.Abs(fuseB - fuseA);

            var windowRadius = AircraftSkinUv.ArcRadiusMetres(WindowWidth, 0.02f);
            AircraftSkinUv.Unwrap(0f, WindowWidth / 2f, 0f, windowRadius, out _, out var winA);
            AircraftSkinUv.Unwrap(1f, WindowWidth / 2f, 0f, windowRadius, out _, out var winB);
            var windowUvPerMetre = Math.Abs(winB - winA);

            Assert.That(windowUvPerMetre, Is.EqualTo(fuselageUvPerMetre).Within(1e-4f),
                "one metre of window skin and one metre of fuselage skin must be one UV unit each");
            Assert.That(fuselageUvPerMetre, Is.EqualTo(1f).Within(1e-4f), "V is metres");

            // The defect this replaced, stated as the ratio it produced.
            var oldFuselage = 1f / FuselageLength;   // bounding box normalised to 0..1
            var oldWindow = 1f / WindowHeight;
            Assert.That(oldWindow / oldFuselage, Is.GreaterThan(100f),
                "the bounding-box unwrap really did differ by over a hundred times");
        }

        [Test]
        public void AFlatPartStillGetsUsableWidth()
        {
            // A paint strip or door skin has no depth; U must not collapse to zero.
            var radius = AircraftSkinUv.ArcRadiusMetres(0f, 0f);
            Assert.That(radius, Is.GreaterThan(0f));
            AircraftSkinUv.Unwrap(0f, 0f, radius, radius, out var u, out _);
            Assert.That(Math.Abs(u), Is.GreaterThan(0f));
        }
    }
}
