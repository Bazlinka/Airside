using System;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class PassengerCabinProfileTests
    {
        [Test]
        public void EverySupportedPassengerTypeHasExactlyOneProfile()
        {
            var expected = new[] { "SF34", "ATR42", "DH8D", "B738", "B38M", "A320", "A21N",
                "E190", "A223", "A359", "A339", "B789", "B78X" };
            Assert.That(PassengerCabinProfile.All.Select(p => p.TypeId), Is.EquivalentTo(expected));
            foreach (var profile in PassengerCabinProfile.All)
            {
                Assert.That(PassengerCabinProfile.TryFor(profile.TypeId, out var found), Is.True);
                Assert.That(found, Is.SameAs(profile));
            }
            Assert.That(PassengerCabinProfile.TryFor("B412", out var unsupported), Is.False);
            Assert.That(unsupported, Is.Null);
            Assert.That(PassengerCabinProfile.TryFor(null, out unsupported), Is.False);
        }

        [Test]
        public void DimensionsFitAnUnobstructedEyeAndSeparateWindowAndSeatRhythms()
        {
            foreach (var p in PassengerCabinProfile.All)
            {
                var dimensions = new[] { p.HalfWidth, p.WindowY, p.WindowZ, p.Pitch, p.WindowPitch,
                    p.WindowWidth, p.WindowHeight, p.RevealDepth, p.WindowSquareness,
                    p.FloorY, p.CeilingY, p.CabinLength, p.AisleWidth, p.SeatEyeInset };
                Assert.That(dimensions.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True, p.TypeId);
                Assert.That(p.WindowWidth, Is.GreaterThan(0f), p.TypeId);
                Assert.That(p.WindowHeight, Is.GreaterThan(p.WindowWidth), p.TypeId);
                // The rim adds .043 m on each side; leave actual wall between neighbouring rims.
                Assert.That(p.WindowWidth + .086f, Is.LessThan(p.WindowPitch), p.TypeId);
                Assert.That(Math.Abs(p.WindowPitch - p.Pitch), Is.GreaterThan(.001f), p.TypeId);
                Assert.That(p.WindowSquareness, Is.GreaterThanOrEqualTo(2f), p.TypeId);
                Assert.That(p.RevealDepth, Is.GreaterThan(0f), p.TypeId);
                Assert.That(p.SeatEyeInset, Is.GreaterThan(p.RevealDepth + .05f), p.TypeId);
                Assert.That(p.SeatEyeInset, Is.LessThan(p.HalfWidth), p.TypeId);
                Assert.That(p.HalfWidth-p.SeatEyeInset, Is.LessThan(p.LiningHalfWidth-.05f), p.TypeId);
                Assert.That(p.FloorY, Is.LessThan(-p.WindowHeight * .5f), p.TypeId);
                Assert.That(p.CeilingY, Is.GreaterThan(p.WindowHeight * .5f), p.TypeId);
                Assert.That(p.CabinLength, Is.GreaterThan(p.Pitch * 5f), p.TypeId);
                Assert.That(p.CabinLength, Is.InRange(12f, 20f), p.TypeId);
                Assert.That(p.AisleWidth, Is.InRange(.4f, .6f), p.TypeId);
                Assert.That(p.SeatGroups.All(n => n > 0), Is.True, p.TypeId);
                var seatWidth = (p.LiningHalfWidth * 2f - .20f - p.AisleWidth * (p.SeatGroups.Length - 1))
                    / p.SeatGroups.Sum();
                Assert.That(seatWidth, Is.InRange(.40f, .60f), p.TypeId);
            }
        }

        [TestCase("SF34", "1,2", .76f, 1.080f, 2.288f)]
        [TestCase("ATR42", "2,2", .76f, 1.340f, 2.168f)]
        [TestCase("DH8D", "2,2", .76f, 1.295f, 2.566f)]
        [TestCase("B738", "3,3", .79f, 1.795f, 4.833f)]
        [TestCase("B38M", "3,3", .79f, 1.795f, 4.802f)]
        [TestCase("A320", "3,3", .79f, 1.892f, 4.642f)]
        [TestCase("A21N", "3,3", .79f, 1.789f, 4.547f)]
        [TestCase("E190", "2,2", .79f, 1.430f, 3.947f)]
        [TestCase("A223", "2,3", .79f, 1.673f, 3.800f)]
        [TestCase("A359", "3,3,3", .79f, 2.836f, 7.133f)]
        [TestCase("A339", "2,4,2", .79f, 2.683f, 7.025f)]
        [TestCase("B789", "3,3,3", .79f, 2.745f, 7.121f)]
        [TestCase("B78X", "3,3,3", .79f, 2.745f, 7.121f)]
        public void ExistingLayoutsAndLateralEyeFitsAreRetained(string type, string groups,
            float pitch, float halfWidth, float eyeY)
        {
            PassengerCabinProfile.TryFor(type, out var p);
            Assert.That(string.Join(",", p.SeatGroups), Is.EqualTo(groups));
            Assert.That(p.Pitch, Is.EqualTo(pitch));
            Assert.That(p.HalfWidth, Is.EqualTo(halfWidth));
            Assert.That(p.WindowY, Is.EqualTo(eyeY));
            Assert.That(p.SeatEyeInset, Is.EqualTo(.43f));
        }

        // Measurements of one connected pane near the original merged-pair station in
        // the shipped glTF; these are kit geometry, not certified aircraft dimensions.
        [TestCase("ATR42", 1.794158f, .508045f, .240021f, .330165f)]
        [TestCase("A320", -14.295f, .670000f, .220000f, .329231f)]
        [TestCase("B789", -24.232627f, .477656f, .235067f, .344649f)]
        public void HeroOpeningsFitAnIndividualShippedPane(string type, float centreZ,
            float pitch, float width, float height)
        {
            PassengerCabinProfile.TryFor(type, out var p);
            Assert.That(p.WindowZ, Is.EqualTo(centreZ).Within(.0005f));
            Assert.That(p.WindowPitch, Is.EqualTo(pitch).Within(.0005f));
            Assert.That(p.WindowWidth, Is.EqualTo(width).Within(.0005f));
            Assert.That(p.WindowHeight, Is.EqualTo(height).Within(.0005f));
        }

        [Test]
        public void DreamlinersShareSectionAndDimmingButRetainAuthoredLongitudinalScaling()
        {
            PassengerCabinProfile.TryFor("B789", out var nine);
            PassengerCabinProfile.TryFor("B78X", out var ten);
            Assert.That(nine.SeatGroups, Is.EqualTo(ten.SeatGroups));
            Assert.That(nine.HalfWidth, Is.EqualTo(ten.HalfWidth));
            Assert.That(nine.WindowHeight, Is.EqualTo(ten.WindowHeight));
            Assert.That(nine.WindowSquareness, Is.EqualTo(ten.WindowSquareness));
            Assert.That(nine.RevealDepth, Is.EqualTo(ten.RevealDepth));
            Assert.That(nine.FloorY, Is.EqualTo(ten.FloorY));
            Assert.That(nine.CeilingY, Is.EqualTo(ten.CeilingY));
            Assert.That(nine.AisleWidth, Is.EqualTo(ten.AisleWidth));
            var authoredScale = 62.81f / 68.30f;
            Assert.That(nine.WindowPitch / ten.WindowPitch, Is.EqualTo(authoredScale).Within(.00002f));
            Assert.That(nine.WindowWidth / ten.WindowWidth, Is.EqualTo(authoredScale).Within(.00002f));
            Assert.That(nine.RevealDepth, Is.GreaterThan(PassengerCabinProfile.All.Single(p => p.TypeId == "A320").RevealDepth));
            Assert.That(PassengerCabinProfile.All.Where(p => p.HasElectronicDimming).Select(p => p.TypeId),
                Is.EquivalentTo(new[] { "B789", "B78X" }));
        }
    }
}
