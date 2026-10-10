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
                "E190", "A223", "A359", "A339", "B789", "B78X", "B748", "A388" };
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
                Assert.That(p.CabinLength, Is.InRange(7f, 24f), p.TypeId);
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

        // Independently decoded POSITION/index connected-component bounds of the actual
        // shipped left panes, not values derived from the profile under test. The old
        // merged nodes span two windows; their midpoint is not an aperture centre.
        // X is the curved pane bounding-box centre, with an intentional .015 m inboard
        // lining datum. Central fit does not prove uniform end/curved-section fit.
        [TestCase("SF34", 1.095209f, 2.288343f, 1.936000f, 0.508000f, 0.240000f, 0.328812f)]
        [TestCase("ATR42", 1.354591f, 2.168136f, 1.794158f, 0.508045f, 0.240021f, 0.330165f)]
        [TestCase("DH8D", 1.309830f, 2.565833f, 3.396000f, 0.508000f, 0.240000f, 0.328921f)]
        [TestCase("B738", 1.810042f, 4.832900f, -14.263000f, 0.507999f, 0.250000f, 0.349914f)]
        [TestCase("B38M", 1.810042f, 4.801969f, -14.263000f, 0.507999f, 0.250000f, 0.347675f)]
        [TestCase("A320", 1.907410f, 4.642379f, -14.295000f, 0.670000f, 0.220000f, 0.329231f)]
        [TestCase("A21N", 1.803995f, 4.546792f, -16.657136f, 0.572866f, 0.281923f, 0.329199f)]
        [TestCase("E190", 1.445164f, 3.946727f, -13.103001f, 0.787000f, 0.230000f, 0.318742f)]
        [TestCase("A223", 1.688442f, 3.800141f, -15.107000f, 0.787001f, 0.250000f, 0.348898f)]
        [TestCase("A359", 2.851398f, 7.133434f, -25.772001f, 0.507999f, 0.250000f, 0.345256f)]
        [TestCase("A339", 2.698303f, 7.024654f, -25.044684f, 0.484122f, 0.238249f, 0.339992f)]
        [TestCase("B789", 2.760498f, 7.120883f, -24.232627f, 0.477657f, 0.235067f, 0.344649f)]
        [TestCase("B78X", 2.760498f, 7.120883f, -26.350715f, 0.519407f, 0.255613f, 0.344649f)]
        public void EveryOpeningFitsAnIndividualShippedPane(string type, float paneHalfWidth,
            float centreY, float centreZ, float pitch, float width, float height)
        {
            PassengerCabinProfile.TryFor(type, out var p);
            Assert.That(paneHalfWidth - p.HalfWidth, Is.InRange(.014f, .016f));
            Assert.That(p.WindowY, Is.EqualTo(centreY).Within(.0005f));
            Assert.That(p.WindowZ, Is.EqualTo(centreZ).Within(.0005f));
            Assert.That(p.WindowPitch, Is.EqualTo(pitch).Within(.0005f));
            Assert.That(p.WindowWidth, Is.EqualTo(width).Within(.0005f));
            Assert.That(p.WindowHeight, Is.EqualTo(height).Within(.0005f));
        }

        // Overall connected-pane belt bounds measured independently from the shipped kits.
        // Keeping the complete symmetric section inside the belt avoids extending into
        // the nose/tail cabin transitions. Local curved-wall clearance still needs review.
        [TestCase("SF34", -5.804000f, 5.612000f)]
        [TestCase("ATR42", -5.438480f, 5.470482f)]
        [TestCase("DH8D", -8.916000f, 10.120000f)]
        [TestCase("B738", -31.152000f, -6.010000f)]
        [TestCase("B38M", -31.152000f, -6.010000f)]
        [TestCase("A320", -30.485001f, -5.474999f)]
        [TestCase("A21N", -35.129856f, -6.777428f)]
        [TestCase("E190", -27.384001f, -5.905001f)]
        [TestCase("A223", -30.972000f, -6.325001f)]
        [TestCase("A359", -58.409004f, -8.375002f)]
        [TestCase("A339", -55.663429f, -7.981327f)]
        [TestCase("B789", -54.920204f, -7.874759f)]
        [TestCase("B78X", -59.720585f, -8.563064f)]
        public void VisualContinuationStaysWithinTheMeasuredPaneBelt(string type,
            float aftPaneEdgeZ, float forwardPaneEdgeZ)
        {
            PassengerCabinProfile.TryFor(type, out var p);
            Assert.That(p.WindowZ - p.CabinLength * .5f, Is.GreaterThanOrEqualTo(aftPaneEdgeZ));
            Assert.That(p.WindowZ + p.CabinLength * .5f, Is.LessThanOrEqualTo(forwardPaneEdgeZ));
        }

        [Test]
        public void StretchedVariantsHaveLongerBoundedVisualContinuation()
        {
            PassengerCabinProfile.TryFor("A320", out var a320);
            PassengerCabinProfile.TryFor("A21N", out var a321);
            PassengerCabinProfile.TryFor("B789", out var nine);
            PassengerCabinProfile.TryFor("B78X", out var ten);
            Assert.That(a321.CabinLength, Is.GreaterThan(a320.CabinLength));
            Assert.That(ten.CabinLength, Is.GreaterThan(nine.CabinLength));
            Assert.That(ten.CabinLength, Is.LessThanOrEqualTo(24f));
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
