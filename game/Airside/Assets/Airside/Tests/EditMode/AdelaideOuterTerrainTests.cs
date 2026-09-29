using System;
using System.IO;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0185: the far DEM and the outer terrain mesh behind the zoomed-out camera.</summary>
    public sealed class AdelaideOuterTerrainTests
    {
        private static AdelaideTerrainHeights LoadFar()
        {
            var relative = Path.Combine("Assets", "Airside", "Art", AdelaideTerrainHeights.FarArtPath);
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, relative), Path.Combine(dir.FullName, "game", "Airside", relative) })
                        if (File.Exists(candidate))
                            return AdelaideTerrainHeights.Parse(File.ReadAllBytes(candidate));
            Assert.Fail("far DEM not found");
            return null;
        }

        private static AdelaideOuterTerrainGeometry.Result Build(AdelaideTerrainHeights t) =>
            AdelaideOuterTerrainGeometry.Build(t.Count, t.Spacing, t.Origin, (xi, zi) => t.Sample(xi, zi),
                AdelaideTerrainHeights.ReliefAbovePlain, 3f, -1f, new[] { 0.3f, 0.3f, 0.2f }, new[] { 0.2f, 0.2f, 0.1f },
                new[] { 0.01f, 0.05f, 0.1f });

        [Test]
        public void FarDem_CoversNinetySixKilometres_WithRealGround()
        {
            var t = LoadFar();
            Assert.That(t, Is.Not.Null);
            Assert.That(t.Spacing, Is.EqualTo(250f));
            Assert.That(t.HalfExtent, Is.EqualTo(96000f));
            float max = 0f, min = float.MaxValue;
            var sea = 0;
            var land = 0;
            for (var zi = 0; zi < t.Count; zi += 8)
            for (var xi = 0; xi < t.Count; xi += 8)
            {
                var h = t.Sample(xi, zi);
                max = Math.Max(max, h);
                min = Math.Min(min, h);
                if (h <= 0.01f)
                    sea++;
                else
                    land++;
            }

            Assert.That(max, Is.InRange(450f, 800f), "the Mount Lofty Ranges");
            Assert.That(sea, Is.GreaterThan(500), "Gulf St Vincent");
            Assert.That(land, Is.GreaterThan(500));
        }

        [Test]
        public void OuterMesh_StartsInsideTheFarRing_AndFacesUp()
        {
            var t = LoadFar();
            var r = Build(t);
            Assert.That(r.Triangles.Length / 3, Is.GreaterThan(100000));
            Assert.That(r.VertexCount, Is.LessThan(200000));
            var minCellRadius = float.MaxValue;
            var maxRadius = 0f;
            for (var i = 0; i < r.Triangles.Length; i += 3)
            {
                int a = r.Triangles[i], b = r.Triangles[i + 1], c = r.Triangles[i + 2];
                var gy = (r.Positions[b * 3 + 2] - r.Positions[a * 3 + 2]) * (r.Positions[c * 3] - r.Positions[a * 3])
                         - (r.Positions[b * 3] - r.Positions[a * 3]) * (r.Positions[c * 3 + 2] - r.Positions[a * 3 + 2]);
                Assert.That(gy, Is.GreaterThan(0f), "up-facing");
                foreach (var v in new[] { a, b, c })
                {
                    var rad = (float)Math.Sqrt(r.Positions[v * 3] * r.Positions[v * 3] + r.Positions[v * 3 + 2] * r.Positions[v * 3 + 2]);
                    minCellRadius = Math.Min(minCellRadius, rad);
                    maxRadius = Math.Max(maxRadius, rad);
                }
            }

            Assert.That(minCellRadius, Is.InRange(25000f, AdelaideOuterTerrainGeometry.InnerRadiusMetres + 1000f), "tucked in under the 30 km ring");
            Assert.That(maxRadius, Is.GreaterThan(90000f), "reaches the far edge");
        }

        [Test]
        public void Overlap_IsTuckedBelowTheFarRing()
        {
            var t = LoadFar();
            var r = Build(t);
            var found = 0;
            for (var i = 0; i < r.VertexCount && found < 50; i++)
            {
                var x = r.Positions[i * 3];
                var z = r.Positions[i * 3 + 2];
                var rad = Math.Sqrt(x * x + z * z);
                if (rad > 26000 && rad < 31000 && r.Colours[i * 4 + 3] < 0.5f)
                {
                    // a land vertex under the ring is at least the tuck below where it would be
                    Assert.That(r.Positions[i * 3 + 1], Is.LessThan(3f + 700f - AdelaideOuterTerrainGeometry.TuckMetres + 1f));
                    found++;
                }
            }

            Assert.That(found, Is.GreaterThan(10));
        }
    }
}
