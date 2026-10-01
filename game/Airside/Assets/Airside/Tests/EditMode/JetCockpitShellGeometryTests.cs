using System;
using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class JetCockpitShellGeometryTests
    {
        [Test] public void EveryJetHasNoOpenSeamsOutsideItsWindowBoundary()
        {
            foreach (var profile in JetCockpitProfile.All)
            {
                var geometry = JetCockpitShellGeometry.Build(profile.HalfWidth);
                var edges = new Dictionary<(int, int), int>();
                var winding = new Dictionary<(int, int), int>();
                for (var i = 0; i < geometry.Triangles.Count; i += 3)
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var a = geometry.Triangles[i + corner];
                        var b = geometry.Triangles[i + (corner + 1) % 3];
                        Assert.That(a, Is.Not.EqualTo(b), "Degenerate edge: " + profile.TypeId);
                        var edge = a < b ? (a, b) : (b, a);
                        edges.TryGetValue(edge, out var count); edges[edge] = count + 1;
                        winding.TryGetValue(edge, out var balance); winding[edge] = balance + (a < b ? 1 : -1);
                    }
                var boundaryNeighbours = new Dictionary<int, List<int>>();
                foreach (var pair in edges)
                {
                    Assert.That(pair.Value, Is.InRange(1, 2), "Non-manifold shell: " + profile.TypeId);
                    if (pair.Value == 2)
                    { Assert.That(winding[pair.Key], Is.Zero, "Inconsistent shell normals: " + profile.TypeId); continue; }
                    var (a, b) = pair.Key;
                    Assert.That(geometry.WindowBoundary.Contains(a) && geometry.WindowBoundary.Contains(b), Is.True,
                        "Unintended wall/floor/roof gap: " + profile.TypeId);
                    foreach (var ends in new[] { (a, b), (b, a) })
                    {
                        if (!boundaryNeighbours.TryGetValue(ends.Item1, out var neighbours))
                            boundaryNeighbours.Add(ends.Item1, neighbours = new List<int>());
                        neighbours.Add(ends.Item2);
                    }
                }
                Assert.That(boundaryNeighbours.Keys, Is.EquivalentTo(geometry.WindowBoundary));
                foreach (var neighbours in boundaryNeighbours.Values) Assert.That(neighbours.Count, Is.EqualTo(2));
                var seen = new HashSet<int>(); var queue = new Queue<int>();
                foreach (var vertex in geometry.WindowBoundary) { queue.Enqueue(vertex); break; }
                while (queue.Count > 0)
                {
                    var vertex = queue.Dequeue(); if (!seen.Add(vertex)) continue;
                    foreach (var next in boundaryNeighbours[vertex]) queue.Enqueue(next);
                }
                Assert.That(seen, Is.EquivalentTo(geometry.WindowBoundary), "The window band must be one continuous loop");
            }
        }
        [Test] public void GlareshieldGivesARealisticOverTheNoseAngleAndWindowsReachUp()
        {
            Assert.That(JetCockpitShellGeometry.OverNoseDownDegrees, Is.InRange(15f, 20f));
            // Windscreen head at the brow is at least ~28 degrees above the eye.
            var up = Math.Atan2(JetCockpitShellGeometry.FrontTopY, 1.05) * 180.0 / Math.PI;
            Assert.That(up, Is.GreaterThan(28.0));
            Assert.That(JetCockpitShellGeometry.SillY, Is.LessThanOrEqualTo(JetCockpitShellGeometry.GlareTopY + 0.01f));
        }
        [TestCase("Flap L")] [TestCase("Aileron R")] [TestCase("Spoiler L")] [TestCase("Winglet L")]
        [TestCase("Wing fairing L")] [TestCase("Fan R")] [TestCase("Intake L")] [TestCase("Pylon L")]
        [TestCase("Exhaust R")] [TestCase("Nacelle fillet L")] [TestCase("PropBlade L2")] [TestCase("Spinner R")]
        [TestCase("Cowl flap L")] [TestCase("Nav light L")] [TestCase("Wing L")] [TestCase("Engine R")]
        public void WindowViewKeepsWingAndEngineParts(string name) =>
            Assert.That(CockpitExteriorVisibility.KeepsDuringCockpit(name), Is.True);
        [TestCase("Fuselage")] [TestCase("Radome")] [TestCase("Windscreen L")] [TestCase("Cabin window 3")]
        [TestCase("Belly fairing")] [TestCase("Door fwd")] [TestCase("Tailplane")] [TestCase("")]
        public void WindowViewHidesFuselageParts(string name) =>
            Assert.That(CockpitExteriorVisibility.KeepsDuringCockpit(name), Is.False);
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidWidthsFail(float width) => Assert.Throws<ArgumentOutOfRangeException>(() => JetCockpitShellGeometry.Build(width));
    }
}
