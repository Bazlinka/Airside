using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>The hollow behind an aircraft door must sit on the skin the door was cut from, not float off it.</summary>
    public sealed class AircraftDoorwayGeometryTests
    {
        private const int Outline = 16;
        private const float HalfLength = 0.46f, HalfArc = 0.9f;

        // A fuselage that is not a circle (an ellipse), so a layer built assuming a round section would miss the skin.
        private const float RadiusX = 1.9f, RadiusY = 2.1f, AxisY = 3.0f;

        private static float[] SkinPoint(float z, float angle, float offset)
        {
            var x = -(RadiusX + offset) * (float)Math.Cos(angle);
            var y = AxisY + (RadiusY + offset) * (float)Math.Sin(angle);
            return new[] { x, y, z };
        }

        /// <summary>A door-shaped shell the way skin_patch lays it out: four outline rings, the centre, then the back face.</summary>
        private static float[] Shell()
        {
            var front = new System.Collections.Generic.List<float>();
            var back = new System.Collections.Generic.List<float>();
            for (var ring = 0; ring <= AircraftDoorwayGeometry.DoorRings; ring++)
            {
                var scale = 1f - ring / (AircraftDoorwayGeometry.DoorRings + 1f);
                for (var i = 0; i < Outline; i++)
                {
                    var theta = 2.0 * Math.PI * i / Outline;
                    var z = HalfLength * scale * (float)Math.Cos(theta);
                    var angle = HalfArc * scale * (float)Math.Sin(theta) / RadiusY;
                    front.AddRange(SkinPoint(z, angle, 0.010f));
                    back.AddRange(SkinPoint(z, angle, -0.004f));
                }
            }

            front.AddRange(SkinPoint(0f, 0f, 0.010f));
            back.AddRange(SkinPoint(0f, 0f, -0.004f));
            front.AddRange(back);
            return front.ToArray();
        }

        private static float Distance(float[] a, int ia, float[] b, int ib) =>
            (float)Math.Sqrt(Math.Pow(a[ia * 3] - b[ib * 3], 2) + Math.Pow(a[ia * 3 + 1] - b[ib * 3 + 1], 2)
                + Math.Pow(a[ia * 3 + 2] - b[ib * 3 + 2], 2));

        [Test]
        public void FullSizeLayer_IsTheLeafsOwnOutlineAndHalfwayRing()
        {
            var shell = Shell();
            Assert.That(AircraftDoorwayGeometry.TryBuild(shell, 1f, 0f, out var v, out var t), Is.True);
            Assert.That(v.Length, Is.EqualTo((2 * Outline + 1) * 3));
            for (var i = 0; i < Outline; i++)
                Assert.That(Distance(v, i, shell, i), Is.LessThan(0.0005f), "outer ring vertex " + i);
            Assert.That(t.Length % 3, Is.EqualTo(0));
            Assert.That(t, Is.All.InRange(0, v.Length / 3 - 1));
        }

        [Test]
        public void Lift_RaisesTheLayerByThatMuchAlongTheOutwardDirection()
        {
            var shell = Shell();
            Assert.That(AircraftDoorwayGeometry.TryBuild(shell, 1f, 0f, out var flat, out _), Is.True);
            Assert.That(AircraftDoorwayGeometry.TryBuild(shell, 1f, 0.008f, out var lifted, out _), Is.True);
            for (var i = 0; i < flat.Length / 3; i++)
                Assert.That(Distance(flat, i, lifted, i), Is.EqualTo(0.008f).Within(0.0005f));
        }

        [Test]
        public void ShrunkLayer_StaysOnTheSkinOfANonCircularFuselage()
        {
            var shell = Shell();
            Assert.That(AircraftDoorwayGeometry.TryBuild(shell, 0.66f, 0f, out var v, out _), Is.True);
            for (var i = 0; i < v.Length / 3; i++)
            {
                // Each point is within a couple of millimetres of the true skin 10 mm proud (the leaf's face).
                var x = v[i * 3] / (RadiusX + 0.010f);
                var y = (v[i * 3 + 1] - AxisY) / (RadiusY + 0.010f);
                Assert.That(Math.Sqrt(x * x + y * y), Is.EqualTo(1.0).Within(0.002), "vertex " + i);
                Assert.That(Math.Abs(v[i * 3 + 2]), Is.LessThanOrEqualTo(HalfLength * 0.66f + 0.001f));
            }
        }

        [Test]
        public void EveryTriangleFacesOutward()
        {
            var shell = Shell();
            foreach (var scale in new[] { 1f, 0.66f })
            {
                Assert.That(AircraftDoorwayGeometry.TryBuild(shell, scale, 0f, out var v, out var t), Is.True);
                for (var k = 0; k < t.Length; k += 3)
                {
                    float[] P(int i) => new[] { v[t[i] * 3], v[t[i] * 3 + 1], v[t[i] * 3 + 2] };
                    var a = P(k); var b = P(k + 1); var c = P(k + 2);
                    var nx = (b[1] - a[1]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[1] - a[1]);
                    var ny = (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]);
                    // The skin faces -x here, so a triangle facing out has a negative x normal component.
                    if (Math.Abs(nx) + Math.Abs(ny) < 1e-9f) continue;
                    Assert.That(nx, Is.LessThan(0f), "triangle " + k / 3 + " at scale " + scale);
                }
            }
        }

        [Test]
        public void ShellAtTheGeneratorsMinimumThickness_IsStillADoor()
        {
            // The shipped Saab/ATR/737/Dash 8 shells have vertex pairs 0.00399995 m apart in float32: the generators'
            // 4 mm minimum, a hair short after rounding. They must still get a doorway rather than bare hull.
            Assert.That(AircraftDoorwayGeometry.TryBuild(Shell(), 1f, 0f, out _, out _), Is.True, "control: normal door");
            Assert.That(AircraftDoorwayGeometry.TryBuild(NearMinimum(0.00399995f), 1f, 0f, out _, out _), Is.True,
                "rounded 4 mm shell");
            Assert.That(AircraftDoorwayGeometry.TryBuild(NearMinimum(0.0030f), 1f, 0f, out _, out _), Is.False,
                "a genuinely thin plate is still not a door");
        }

        /// <summary>The door shell with its back face moved to sit exactly <paramref name="thickness"/> behind the front.</summary>
        private static float[] NearMinimum(float thickness)
        {
            var shell = Shell();
            var half = shell.Length / 6;
            for (var i = 0; i < half; i++)
            {
                // Pull the back vertex along the (front - back) direction until the pair is `thickness` apart.
                float dx = shell[i * 3] - shell[(i + half) * 3], dy = shell[i * 3 + 1] - shell[(i + half) * 3 + 1],
                    dz = shell[i * 3 + 2] - shell[(i + half) * 3 + 2];
                var length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                var back = (i + half) * 3;
                shell[back] = shell[i * 3] - dx / length * thickness;
                shell[back + 1] = shell[i * 3 + 1] - dy / length * thickness;
                shell[back + 2] = shell[i * 3 + 2] - dz / length * thickness;
            }
            return shell;
        }

        [Test]
        public void NotADoorShell_IsRefused()
        {
            Assert.That(AircraftDoorwayGeometry.TryBuild(null, 1f, 0f, out _, out _), Is.False);
            Assert.That(AircraftDoorwayGeometry.TryBuild(new float[9], 1f, 0f, out _, out _), Is.False);
            Assert.That(AircraftDoorwayGeometry.TryBuild(Shell(), 0f, 0f, out _, out _), Is.False);
            // Front and back identical: no thickness to read an outward direction from.
            var shell = Shell();
            Array.Copy(shell, 0, shell, shell.Length / 2, shell.Length / 2);
            Assert.That(AircraftDoorwayGeometry.TryBuild(shell, 1f, 0f, out _, out _), Is.False);
        }
    }
}
