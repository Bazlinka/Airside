using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0124 — building detail stays on its building and the block bevel is a closed, outward mesh.</summary>
    public sealed class BuildingDetailTests
    {
        private static AdelaideBuilding Of(AdelaideBuildingKind kind) => AdelaideBuildings.All.First(b => b.Kind == kind);

        private static (float minX, float maxX, float minZ, float maxZ) Extent(float[] xz)
        {
            var xs = Enumerable.Range(0, xz.Length / 2).Select(i => xz[i * 2]).ToList();
            var zs = Enumerable.Range(0, xz.Length / 2).Select(i => xz[i * 2 + 1]).ToList();
            return (xs.Min(), xs.Max(), zs.Min(), zs.Max());
        }

        [Test]
        public void EveryBuilding_KeepsItsDetailOnOrAboveItsFootprint()
        {
            foreach (var building in AdelaideBuildings.All)
            {
                var set = BuildingDetail.For(building, 5f);
                var (minX, maxX, minZ, maxZ) = Extent(building.Xz);
                // Canopies aside, nothing reaches more than a door header's depth off the wall.
                const float slack = 8f;
                foreach (var box in set.Boxes)
                {
                    Assert.That(box.X, Is.InRange(minX - slack, maxX + slack), building.Id);
                    Assert.That(box.Z, Is.InRange(minZ - slack, maxZ + slack), building.Id);
                    Assert.That(box.Bottom, Is.GreaterThanOrEqualTo(5f - 0.01f), $"{building.Id} {box.Part} below ground");
                    Assert.That(box.Length, Is.GreaterThan(0f));
                    Assert.That(box.Height, Is.GreaterThan(0f));
                    Assert.That(box.Depth, Is.GreaterThan(0f));
                    Assert.That(Math.Abs(box.DirX * box.DirX + box.DirZ * box.DirZ - 1f), Is.LessThan(1e-3f));
                }
            }
        }

        [Test]
        public void FlatRoofs_GetAParapetOnEveryWall()
        {
            var shed = Of(AdelaideBuildingKind.Support);
            var set = BuildingDetail.For(shed, 0f);
            var parapets = set.Boxes.Where(b => b.Part == BuildingPart.Shell && b.Bottom >= shed.HeightMetres - 0.01f
                                                                             && b.Top <= shed.HeightMetres + 1f).ToList();
            Assert.That(parapets.Count, Is.EqualTo(shed.Xz.Length / 2));
            Assert.That(parapets.All(p => Math.Abs(p.Height - BuildingDetail.ParapetHeight(shed.Kind)) < 1e-4f), Is.True);
        }

        [Test]
        public void Offices_HaveWindowBandsWithSomePanesDarkAtNight()
        {
            var lit = 0;
            var dark = 0;
            foreach (var building in AdelaideBuildings.All.Where(b => b.Kind == AdelaideBuildingKind.Support))
            {
                var set = BuildingDetail.For(building, 0f);
                lit += set.Boxes.Count(b => b.Part == BuildingPart.WindowLit);
                dark += set.Boxes.Count(b => b.Part == BuildingPart.WindowDark);
                foreach (var pane in set.Boxes.Where(b => b.Part is BuildingPart.WindowLit or BuildingPart.WindowDark))
                    Assert.That(pane.Top, Is.LessThan(building.HeightMetres), building.Id);
            }

            Assert.That(lit, Is.GreaterThan(dark), "most offices are occupied");
            Assert.That(dark, Is.GreaterThan(0), "not one uniform strip");
        }

        [Test]
        public void Hangars_HaveADoorAndFireStationsHaveApplianceBays()
        {
            foreach (var hangar in AdelaideBuildings.All.Where(b => b.Kind == AdelaideBuildingKind.Hangar))
            {
                var doors = BuildingDetail.For(hangar, 0f).Boxes.Where(b => b.Part == BuildingPart.Door).ToList();
                Assert.That(doors.Count, Is.EqualTo(1), hangar.Id);
                Assert.That(doors[0].Top, Is.LessThan(hangar.HeightMetres), hangar.Id);
                Assert.That(doors[0].Length, Is.GreaterThan(6f), hangar.Id);
            }

            var station = BuildingDetail.For(Of(AdelaideBuildingKind.FireStation), 0f);
            Assert.That(station.Boxes.Count(b => b.Part == BuildingPart.Door), Is.InRange(1, 5));
        }

        [Test]
        public void Tower_HasAGlassCabAboveItsShaftAndALitMast()
        {
            var tower = Of(AdelaideBuildingKind.ControlTower);
            var set = BuildingDetail.For(tower, 2f);
            var shaft = set.Prisms.First();
            var glass = set.Prisms.Single(p => p.Part == BuildingPart.CabGlass);
            Assert.That(glass.BaseY, Is.GreaterThan(shaft.Top));
            Assert.That(glass.Height, Is.GreaterThan(3f));
            Assert.That(glass.Top, Is.LessThanOrEqualTo(2f + tower.HeightMetres));
            var light = set.Boxes.Single(b => b.Part == BuildingPart.ObstructionLight);
            Assert.That(light.Bottom, Is.GreaterThan(2f + tower.HeightMetres));
            // One mullion per cab corner.
            Assert.That(set.Boxes.Count(b => b.Part == BuildingPart.Trim && Math.Abs(b.Height - glass.Height) < 1e-4f),
                Is.EqualTo(tower.Xz.Length / 2));
        }

        [Test]
        public void RoofPlant_SitsInsideTheRoofAndIsTheSameEveryRun()
        {
            foreach (var building in AdelaideBuildings.All)
            {
                var a = BuildingDetail.For(building, 0f).Boxes;
                var b = BuildingDetail.For(building, 0f).Boxes;
                Assert.That(a.Count, Is.EqualTo(b.Count), building.Id);
                for (var i = 0; i < a.Count; i++)
                    Assert.That((a[i].X, a[i].Y, a[i].Z, a[i].Part), Is.EqualTo((b[i].X, b[i].Y, b[i].Z, b[i].Part)));
                foreach (var plant in a.Where(p => p.Part == BuildingPart.Plant))
                {
                    Assert.That(BuildingDetail.Contains(building.Xz, plant.X, plant.Z), Is.True, building.Id);
                    Assert.That(plant.Bottom, Is.EqualTo(building.HeightMetres).Within(1e-3f));
                }
            }

            Assert.That(AdelaideBuildings.All.Sum(b => BuildingDetail.For(b, 0f).Boxes.Count(p => p.Part == BuildingPart.Plant)),
                Is.GreaterThan(10));
        }

        [Test]
        public void Terminal_GetsALandsideCanopyAndNoAirsideDetail()
        {
            var terminal = AdelaideLayout.Terminals.First(t => t.Name == "Domestic & International Terminal");
            var set = BuildingDetail.ForTerminal(terminal.Name, terminal.Xz, 0f, AdelaideTerminalArchitecture.ShellHeightMetres, rfds: false);
            var canopy = set.Boxes.Where(b => b.Part == BuildingPart.Canopy).ToList();
            Assert.That(canopy, Is.Not.Empty);
            var airsideZ = Extent(terminal.Xz).minZ;
            // Canopies and windows stay off the airside curtain wall (its own 28-bay glazing).
            foreach (var box in set.Boxes.Where(b => b.Part is BuildingPart.Canopy or BuildingPart.WindowLit or BuildingPart.WindowDark))
                Assert.That(box.Z, Is.GreaterThan(airsideZ + 10f));
            // Rooftop plant keeps off the authored skylights and plant screens.
            foreach (var plant in set.Boxes.Where(b => b.Part == BuildingPart.Plant))
            foreach (var roof in AdelaideTerminalArchitecture.RoofDetails())
                Assert.That(Math.Abs(roof.X - plant.X) >= roof.Width * 0.5f + plant.Length * 0.5f
                            || Math.Abs(roof.Z - plant.Z) >= roof.Depth * 0.5f + plant.Length * 0.5f, Is.True, roof.Name);
        }

        [Test]
        public void BevelledCube_IsClosedOutwardAndUnitSized()
        {
            var g = BevelledBox.Build(0.05f, 0.2f, 0.1f);
            Assert.That(g.VertexCount, Is.EqualTo(96));
            Assert.That(g.Triangles.Count / 3, Is.EqualTo(44));
            for (var i = 0; i < g.VertexCount; i++)
                for (var k = 0; k < 3; k++)
                    Assert.That(Math.Abs(g.Positions[i * 3 + k]), Is.LessThanOrEqualTo(0.5f + 1e-5f));

            // Weld by position: every edge is shared by exactly two triangles, in opposite directions.
            string Key(int i) => $"{Math.Round(g.Positions[i * 3], 4)},{Math.Round(g.Positions[i * 3 + 1], 4)},{Math.Round(g.Positions[i * 3 + 2], 4)}";
            var edges = new Dictionary<(string, string), int>();
            for (var t = 0; t < g.Triangles.Count; t += 3)
            {
                for (var e = 0; e < 3; e++)
                {
                    var a = Key(g.Triangles[t + e]);
                    var b = Key(g.Triangles[t + (e + 1) % 3]);
                    edges[(a, b)] = edges.TryGetValue((a, b), out var n) ? n + 1 : 1;
                }

                // Each face points away from the centre, and matches its stored normal.
                var p = new float[3][];
                for (var v = 0; v < 3; v++)
                    p[v] = new[] { g.Positions[g.Triangles[t + v] * 3], g.Positions[g.Triangles[t + v] * 3 + 1], g.Positions[g.Triangles[t + v] * 3 + 2] };
                var e1 = new[] { p[1][0] - p[0][0], p[1][1] - p[0][1], p[1][2] - p[0][2] };
                var e2 = new[] { p[2][0] - p[0][0], p[2][1] - p[0][1], p[2][2] - p[0][2] };
                var cross = new[] { e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0] };
                var centre = new[] { (p[0][0] + p[1][0] + p[2][0]) / 3f, (p[0][1] + p[1][1] + p[2][1]) / 3f, (p[0][2] + p[1][2] + p[2][2]) / 3f };
                Assert.That(cross[0] * centre[0] + cross[1] * centre[1] + cross[2] * centre[2], Is.GreaterThan(0f));
                var n0 = g.Triangles[t] * 3;
                Assert.That(cross[0] * g.Normals[n0] + cross[1] * g.Normals[n0 + 1] + cross[2] * g.Normals[n0 + 2], Is.GreaterThan(0f));
            }

            foreach (var ((a, b), count) in edges)
            {
                Assert.That(count, Is.EqualTo(1), "an edge used twice in one direction");
                Assert.That(edges.ContainsKey((b, a)), Is.True, "an open edge");
            }
        }

        [Test]
        public void BevelledCube_IsTheSameWorldSizeOnEveryAxis()
        {
            var local = BevelledBox.LocalBevelFor(4f, 1f, 10f);
            Assert.That(local.HasValue, Is.True);
            var world = BevelledBox.MaxWorldBevel;
            Assert.That(local.Value.x * 4f, Is.EqualTo(world).Within(1e-5f));
            Assert.That(local.Value.y * 1f, Is.EqualTo(world).Within(1e-5f));
            Assert.That(local.Value.z * 10f, Is.EqualTo(world).Within(1e-5f));
            Assert.That(BevelledBox.LocalBevelFor(10f, 0.02f, 10f).HasValue, Is.False, "paint stays a plain cube");
            var thin = BevelledBox.LocalBevelFor(0.1f, 0.1f, 0.1f).Value;
            Assert.That(thin.x, Is.EqualTo(BevelledBox.WorldBevelFraction).Within(1e-5f));
        }
    }
}
