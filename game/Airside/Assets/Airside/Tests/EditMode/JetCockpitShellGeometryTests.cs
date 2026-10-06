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
                var geometry = JetCockpitShellGeometry.Build(profile);
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
        [Test] public void FamilyFramesShareShellBoundaryAndKeepPanelAndOverheadClear()
        {
            foreach (var profile in JetCockpitProfile.All)
            {
                var layout = profile.Shell;
                var geometry = JetCockpitShellGeometry.Build(profile);
                // Panel sides at 0.875 W cannot protrude through the lower tapered wall.
                Assert.That(layout.FrontSillRatio * profile.HalfWidth,
                    Is.GreaterThan(profile.HalfWidth * 0.875f + 0.02f), profile.TypeId);
                Assert.That(layout.ShoulderZ, Is.GreaterThan(0.55f), "Roof wedge starts beyond overhead");
                Assert.That(layout.FrontHeadY, Is.GreaterThan(0.60f));
                Assert.That(Math.Atan2(layout.FrontHeadY, layout.FrontHeadZ) * 180 / Math.PI, Is.GreaterThan(28));
                Assert.That(layout.FrontHeadRatio, Is.LessThan(layout.FrontSillRatio));
                var quarterlights = 0;
                foreach (var frame in geometry.Frames)
                {
                    var a = frame.Start; var b = frame.End;
                    Assert.That((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) +
                        (a.Z - b.Z) * (a.Z - b.Z), Is.GreaterThan(0.001f), frame.Name);
                    if (frame.Name == "Windscreen centre post")
                    {
                        Assert.That(a.X, Is.Zero); Assert.That(b.X, Is.Zero);
                        Assert.That(a.Z, Is.EqualTo(1.30f));
                        Assert.That(b.Z, Is.EqualTo(layout.FrontHeadZ));
                        continue;
                    }
                    if (frame.Name == "Side quarterlight pillar")
                    {
                        quarterlights++;
                        Assert.That(a.Z, Is.InRange(-0.61f + 0.05f, layout.ShoulderZ - 0.05f));
                        Assert.That(a.Y, Is.EqualTo(JetCockpitShellGeometry.SillY));
                        Assert.That(b.Y, Is.EqualTo(JetCockpitShellGeometry.SideTopY));
                        continue;
                    }
                    foreach (var end in new[] { a, b })
                    {
                        var found = false;
                        foreach (var id in geometry.WindowBoundary)
                        {
                            var vertex = geometry.Vertices[id];
                            if (vertex.X == end.X && vertex.Y == end.Y && vertex.Z == end.Z) found = true;
                        }
                        Assert.That(found, Is.True, profile.TypeId + ": frame end must be welded aperture vertex");
                    }
                }
                Assert.That(quarterlights, Is.EqualTo(layout.QuarterlightZ.HasValue ? 2 : 0));
            }
        }

        [Test] public void SharedFamilyShellsRetainCommonIdentity()
        {
            JetCockpitProfile.TryFor("B738", out var ng); JetCockpitProfile.TryFor("B38M", out var max);
            JetCockpitProfile.TryFor("A320", out var a320); JetCockpitProfile.TryFor("A21N", out var neo);
            JetCockpitProfile.TryFor("A339", out var a330);
            JetCockpitProfile.TryFor("B789", out var nine); JetCockpitProfile.TryFor("B78X", out var ten);
            Assert.That(ng.Shell, Is.SameAs(max.Shell));
            Assert.That(a320.Shell, Is.SameAs(neo.Shell));
            Assert.That(a330.Shell, Is.SameAs(a320.Shell));
            Assert.That(nine.Shell, Is.SameAs(ten.Shell));
            Assert.That(nine.Shell.QuarterlightZ, Is.Null);
            var layouts = new HashSet<JetCockpitShellGeometry.Layout>();
            foreach (var profile in JetCockpitProfile.All) layouts.Add(profile.Shell);
            Assert.That(layouts.Count, Is.EqualTo(6));
        }

        [Test] public void A350DisplayStationsHaveEqualFormatsClearEdgesAndInwardLateralNormals()
        {
            var stations = JetCockpitProfile.A350Displays;
            Assert.That(stations.Count, Is.EqualTo(6));
            Assert.That(JetCockpitProfile.TryFor("A359", out var profile), Is.True);
            var pilots = new HashSet<int>();
            for (var i = 0; i < stations.Count; i++)
            {
                var a = stations[i];
                Assert.That(a.Width, Is.EqualTo(stations[0].Width));
                Assert.That(a.Height, Is.EqualTo(stations[0].Height));
                Assert.That(Math.Abs(a.X) + a.Width * 0.5f, Is.LessThan(profile.HalfWidth * 0.875f));
                // Positive yaw points a screen's rear-facing normal left, toward the
                // pilots for the right lateral screen; the left screen mirrors it.
                if (a.Yaw != 0f) Assert.That(a.X * -Math.Sin(a.Yaw * Math.PI / 180.0), Is.LessThan(0));
                if (a.Pilot >= 0) Assert.That(pilots.Add(a.Pilot), Is.True);
                for (var j = i + 1; j < stations.Count; j++)
                {
                    var b = stations[j];
                    Assert.That(Math.Abs(a.X - b.X) > (a.Width + b.Width) * 0.5f ||
                        Math.Abs(a.Y - b.Y) > (a.Height + b.Height) * 0.5f, Is.True,
                        "Adjacent bezels must have clearance");
                }
            }
            Assert.That(pilots, Is.EquivalentTo(new[] { 0, 1 }));
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
