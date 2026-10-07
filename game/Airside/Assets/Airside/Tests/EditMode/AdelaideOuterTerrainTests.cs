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

        private static AdelaideFarLandCover LoadCover()
        {
            var relative = Path.Combine("Assets", "Airside", "Art", AdelaideFarLandCover.ArtPath);
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, relative), Path.Combine(dir.FullName, "game", "Airside", relative) })
                        if (File.Exists(candidate))
                            return AdelaideFarLandCover.Parse(File.ReadAllBytes(candidate));
            Assert.Fail("far land cover not found");
            return null;
        }

        [Test]
        public void LandCover_CoversTheFarGridWithRealVariety()
        {
            var cover = LoadCover();
            var dem = LoadFar();
            Assert.That(cover, Is.Not.Null);
            Assert.That(cover.Count, Is.EqualTo(dem.Count), "the same grid as the far DEM");
            Assert.That(cover.Spacing, Is.EqualTo(dem.Spacing));
            var counts = new int[AdelaideFarLandCover.ClassCount];
            for (var zi = 0; zi < cover.Count; zi++)
            for (var xi = 0; xi < cover.Count; xi++)
                counts[cover.ClassAt(xi, zi)]++;
            var total = (double)cover.Count * cover.Count;
            Assert.That(counts[AdelaideFarLandCover.Water] / total, Is.InRange(0.2, 0.6), "Gulf St Vincent and Spencer Gulf");
            Assert.That(counts[AdelaideFarLandCover.Built] / total, Is.InRange(0.005, 0.05), "greater Adelaide and the towns");
            Assert.That(counts[AdelaideFarLandCover.Crop] / total, Is.GreaterThan(0.1), "the Mid North and Yorke Peninsula");
            Assert.That(counts[AdelaideFarLandCover.Tree] / total, Is.GreaterThan(0.02), "the Hills");
            Assert.That(counts[AdelaideFarLandCover.Grass] / total, Is.GreaterThan(0.05));
        }

        [Test]
        public void LandCover_AgreesWithTheDem_OnTheSea()
        {
            var cover = LoadCover();
            var dem = LoadFar();
            var seaCells = 0;
            var seaAsWater = 0;
            for (var zi = 0; zi < cover.Count; zi += 4)
            for (var xi = 0; xi < cover.Count; xi += 4)
            {
                if (dem.Sample(xi, zi) > 0.01f)
                    continue;
                seaCells++;
                if (cover.ClassAt(xi, zi) == AdelaideFarLandCover.Water)
                    seaAsWater++;
            }

            Assert.That(seaCells, Is.GreaterThan(1000));
            Assert.That(seaAsWater / (double)seaCells, Is.GreaterThan(0.9), "two independent sources agree where the coast is");
        }

        [Test]
        public void LandCoverColours_AreDistinctPerClass_AndPatchworkedInCropLand()
        {
            var colour = new float[3];
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var cls = 0; cls < AdelaideFarLandCover.ClassCount; cls++)
            {
                AdelaideFarLandCover.Colour(cls, 10, 10, 0f, colour);
                foreach (var c in colour)
                    Assert.That(c, Is.InRange(0f, 0.6f), $"class {cls} is a plain colour, never white");
                seen.Add($"{colour[0]:F2},{colour[1]:F2},{colour[2]:F2}");
            }

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(7));
            var paddocks = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < 40; i++)
            {
                AdelaideFarLandCover.Colour(AdelaideFarLandCover.Crop, i * 4, 8, 0f, colour);
                paddocks.Add($"{Math.Round((colour[0] + colour[1] + colour[2]) * 30)}");
            }

            Assert.That(paddocks.Count, Is.GreaterThan(2), "crop land is a patchwork, not one tone");
            var flat = new float[3];
            var steep = new float[3];
            AdelaideFarLandCover.Colour(AdelaideFarLandCover.Tree, 3, 3, 0f, flat);
            AdelaideFarLandCover.Colour(AdelaideFarLandCover.Tree, 3, 3, 1f, steep);
            Assert.That(steep[1], Is.LessThan(flat[1]), "hillsides are darker");
        }

        [Test]
        public void DrapeHandover_CrossfadesFromTheImageToLandCoverWithoutAHalo()
        {
            const float strength = 0.92f, plain = 0.27f, land = 0.08f;
            foreach (var image in new[] { 0.02f, 0.09f, 0.4f })
            foreach (var t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var vertex = AdelaideFarLandCover.DrapeHandover(plain, land, t, strength);
                var drape = strength * (1f - t);
                var rendered = drape * image + (1f - drape) * vertex;
                var crossfade = (1f - t) * (strength * image + (1f - strength) * plain) + t * land;
                Assert.That(rendered, Is.EqualTo(crossfade).Within(1e-5f), $"image {image}, t {t}");
            }

            Assert.That(AdelaideFarLandCover.Smoothstep(24000f, 29500f, 20000f), Is.EqualTo(0f));
            Assert.That(AdelaideFarLandCover.Smoothstep(24000f, 29500f, 30000f), Is.EqualTo(1f));
        }

        [Test]
        public void MeshWithLandCover_ColoursByClass_AndStaysTheSameShape()
        {
            var dem = LoadFar();
            var cover = LoadCover();
            var plain = Build(dem);
            var coloured = AdelaideOuterTerrainGeometry.Build(dem.Count, dem.Spacing, dem.Origin, (xi, zi) => dem.Sample(xi, zi),
                AdelaideTerrainHeights.ReliefAbovePlain, 3f, -1f, new[] { 0.3f, 0.3f, 0.2f }, new[] { 0.2f, 0.2f, 0.1f },
                new[] { 0.01f, 0.05f, 0.1f }, cover);
            Assert.That(coloured.VertexCount, Is.EqualTo(plain.VertexCount));
            Assert.That(coloured.Triangles.Length, Is.EqualTo(plain.Triangles.Length));
            var different = 0;
            for (var i = 0; i < coloured.Colours.Length; i++)
            {
                Assert.That(float.IsNaN(coloured.Colours[i]) || coloured.Colours[i] < 0f || coloured.Colours[i] > 1f, Is.False, $"colour {i}");
                if (Math.Abs(coloured.Colours[i] - plain.Colours[i]) > 0.02f)
                    different++;
            }

            Assert.That(different, Is.GreaterThan(coloured.Colours.Length / 10), "the land is no longer one blend of two colours");
        }
    }
}
