using System;
using System.IO;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideCbdSkylineTests
    {
        private static AdelaideTerrainHeights LoadTerrain()
        {
            var relative = Path.Combine("Assets", "Airside", "Art", AdelaideTerrainHeights.ArtPath);
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    foreach (var candidate in new[]
                             {
                                 Path.Combine(dir.FullName, relative),
                                 Path.Combine(dir.FullName, "game", "Airside", relative)
                             })
                    {
                        if (File.Exists(candidate))
                            return AdelaideTerrainHeights.Parse(File.ReadAllBytes(candidate));
                    }
                }
            }

            Assert.Fail("dem_adelaide_runway_v01.bin not found");
            return null;
        }

        [Test]
        public void Towers_AreSourcedCbdCluster()
        {
            Assert.That(AdelaideCbdSkyline.Count, Is.InRange(30, 60));
            Assert.That(AdelaideCbdSkyline.Attribution, Does.Contain("OpenStreetMap"));

            var hasVictoria = false;
            var hasFestival = false;
            var hasSofitel = false;
            for (var i = 0; i < AdelaideCbdSkyline.Count; i++)
            {
                var t = AdelaideCbdSkyline.Get(i);
                Assert.That(t.HeightMetres, Is.InRange(30f, 200f));
                Assert.That(t.HalfExtentX, Is.GreaterThan(1f).And.LessThanOrEqualTo(80f));
                Assert.That(t.HalfExtentZ, Is.GreaterThan(1f).And.LessThanOrEqualTo(80f));
                Assert.That(t.CentreX, Is.InRange(5000f, 7800f));
                Assert.That(t.CentreZ, Is.InRange(-2800f, -600f));
                var dx = t.CentreX - AdelaideCbdSkyline.VictoriaSquareX;
                var dz = t.CentreZ - AdelaideCbdSkyline.VictoriaSquareZ;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.LessThanOrEqualTo(2500.0));
                if (t.OsmId == 1376925785L) hasVictoria = true;
                if (t.OsmId == 1285172442L) hasFestival = true;
                if (t.OsmId == 971999300L) hasSofitel = true;
            }

            Assert.That(hasVictoria && hasFestival && hasSofitel, Is.True,
                "Victoria Tower, Festival Tower and Sofitel must be in the silhouette");
        }

        [Test]
        public void Geometry_BuildsOneBoxPerTowerOnDemRelief()
        {
            var terrain = LoadTerrain();
            var plainY = 0f;
            var mesh = AdelaideCbdSkylineGeometry.Build(plainY, terrain.Height);
            Assert.That(mesh.VertexCount, Is.EqualTo(AdelaideCbdSkyline.Count * 8));
            Assert.That(mesh.TriangleCount, Is.EqualTo(AdelaideCbdSkyline.Count * 12));

            for (var i = 0; i < AdelaideCbdSkyline.Count; i++)
            {
                var t = AdelaideCbdSkyline.Get(i);
                var expected = AdelaideCbdSkylineGeometry.BaseY(plainY, terrain.Height(t.CentreX, t.CentreZ));
                var y = mesh.Positions[i * 8 * 3 + 1];
                Assert.That(y, Is.EqualTo(expected).Within(1f),
                    $"tower {t.OsmId} base Y {y} vs DEM relief {expected}");
            }
        }
    }
}
