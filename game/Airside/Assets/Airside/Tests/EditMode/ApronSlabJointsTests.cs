using System;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ApronSlabJointsTests
    {
        [Test]
        public void Rectangle_GeneratesOneInsetJointInEachDirection()
        {
            var polygon = new[] { 0f, 0f, 40f, 0f, 40f, 40f, 0f, 40f };
            var joints = ApronSlabJoints.Generate(polygon, spacing: 20f, edgeInset: 0.5f);

            Assert.That(joints.Count, Is.EqualTo(2));
            Assert.That(joints.Count(j => Approximately(j.StartX, j.EndX)), Is.EqualTo(1));
            Assert.That(joints.Count(j => Approximately(j.StartZ, j.EndZ)), Is.EqualTo(1));
            Assert.That(joints.All(j => Approximately(j.Length, 39f)), Is.True);
        }

        [Test]
        public void ConcaveOutline_ClipsGridIntoSeparateInteriorSegments()
        {
            var polygon = new[]
            {
                0f, 0f, 50f, 0f, 50f, 50f, 30f, 50f,
                30f, 20f, 20f, 20f, 20f, 50f, 0f, 50f
            };

            var joints = ApronSlabJoints.Generate(polygon, spacing: 10f, edgeInset: 0.25f);
            var splitLine = joints.Where(j => Approximately(j.StartZ, 30f)).ToArray();

            Assert.That(splitLine.Length, Is.EqualTo(2), "the courtyard notch must remain unpainted");
            Assert.That(splitLine.All(j => j.Length < 20f), Is.True);
        }

        [Test]
        public void AdelaideAprons_ProduceFiniteRealScaleDetail()
        {
            var count = 0;
            foreach (var apron in AdelaideLayout.Aprons)
            foreach (var joint in ApronSlabJoints.Generate(apron.Xz))
            {
                Assert.That(float.IsNaN(joint.Length) || float.IsInfinity(joint.Length), Is.False, apron.Name);
                Assert.That(joint.Length, Is.GreaterThan(ApronSlabJoints.WidthMetres * 2f), apron.Name);
                count++;
            }

            Assert.That(count, Is.InRange(100, 2000), "detail should read without creating a mesh carpet");
        }

        private static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }
}
