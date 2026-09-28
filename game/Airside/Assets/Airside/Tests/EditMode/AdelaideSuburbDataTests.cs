using System;
using System.IO;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0159: the baked suburbs read back whole, clear of the airport and at house scale.</summary>
    public sealed class AdelaideSuburbDataTests
    {
        private static AdelaideSuburbData Load() => AdelaideSuburbData.Parse(Find(AdelaideSuburbData.ArtPath));

        private static byte[] Find(string artPath)
        {
            var relative = Path.Combine("Assets", "Airside", "Art", artPath);
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
                            return File.ReadAllBytes(candidate);
                    }
                }
            }

            Assert.Fail(artPath + " not found");
            return null;
        }

        [Test]
        public void Trees_StandWhereTheSatelliteSeesCanopy()
        {
            var trees = AdelaideTreeData.Parse(Find(AdelaideTreeData.ArtPath));
            Assert.That(trees, Is.Not.Null);
            Assert.That(trees.Trees.Count, Is.InRange(3_000, 30_000));
            foreach (var t in trees.Trees)
            {
                Assert.That(t.Height, Is.InRange(5f, 16f));
                Assert.That(t.CrownRadius, Is.InRange(2f, 5f));
                Assert.That(t.Colour, Is.LessThan(AdelaideTreeData.ColourCount));
                var inPrecinct = t.X > 750f && t.X < 1750f && t.Z > 430f && t.Z < 920f;
                Assert.That(inPrecinct, Is.False, $"tree at {t.X:0},{t.Z:0} in the landside precinct");
            }

            // No tree on the runway strip (the airside is excluded, and its dry grass is not canopy).
            Assert.That(trees.Trees.Count(t => Math.Abs(t.X) < 1550f && Math.Abs(t.Z) < 150f), Is.EqualTo(0));
            Assert.That(AdelaideTreeData.Parse(new byte[] { (byte)'A', (byte)'T', (byte)'R', (byte)'E', 1, 0, 0, 0, 2, 0, 0, 0 }),
                Is.Null, "two trees promised, none present");
        }

        [Test]
        public void File_ParsesToASuburbOfMappedAndStreetFrontBuildings()
        {
            var data = Load();
            Assert.That(data, Is.Not.Null);
            var all = data.Buildings;
            Assert.That(all.Count, Is.InRange(8_000, 25_000));
            Assert.That(all.Count(b => b.Kind == AdelaideSuburbData.Kind.FlatPrism), Is.GreaterThan(500));
            Assert.That(all.Count(b => b.Kind == AdelaideSuburbData.Kind.OsmHouse), Is.GreaterThan(2_000));
            Assert.That(all.Count(b => b.Kind == AdelaideSuburbData.Kind.FillerHouse), Is.GreaterThan(2_000));
        }

        [Test]
        public void Buildings_AreHouseScaleAndUsePaletteColours()
        {
            foreach (var b in Load().Buildings)
            {
                Assert.That(b.WallHeight, Is.InRange(2f, 80f));
                Assert.That(b.Wall, Is.LessThan(AirsideSuburbPalette.WallCount));
                Assert.That(b.Roof, Is.LessThan(AirsideSuburbPalette.RoofCount));
                if (b.IsHipped)
                {
                    Assert.That(b.HalfLength, Is.InRange(1f, 30f));
                    Assert.That(b.HalfWidth, Is.InRange(1f, b.HalfLength + 0.01f));
                    Assert.That(b.RoofRise, Is.InRange(0.5f, 4f));
                }
                else
                {
                    Assert.That(b.Footprint.Length / 2, Is.InRange(3, 12));
                    Assert.That(b.RoofTriangles.Length / 3, Is.EqualTo(b.Footprint.Length / 2 - 2),
                        "a simple polygon's roof is n - 2 triangles");
                }
            }
        }

        [Test]
        public void Buildings_KeepOffTheAirportAndStayNearIt()
        {
            foreach (var b in Load().Buildings)
            {
                // The landside precinct (AdelaideLandside) belongs to the terminal and car parks.
                var inPrecinct = b.CentreX > 750f && b.CentreX < 1750f && b.CentreZ > 430f && b.CentreZ < 920f;
                Assert.That(inPrecinct, Is.False, $"building at {b.CentreX:0},{b.CentreZ:0} in the landside precinct");
                var outside = Math.Sqrt(Math.Pow(Math.Max(0f, Math.Abs(b.CentreX) - 1950f), 2)
                                        + Math.Pow(Math.Max(0f, Math.Abs(b.CentreZ) - 1400f), 2));
                Assert.That(outside, Is.LessThan(1850.0), "the suburb tapers out by 1.8 km");
            }
        }

        [Test]
        public void Parse_RejectsTruncatedOrForeignFiles()
        {
            Assert.That(AdelaideSuburbData.Parse(null), Is.Null);
            Assert.That(AdelaideSuburbData.Parse(new byte[] { (byte)'A', (byte)'S', (byte)'U', (byte)'B', 1, 0, 0, 0, 5, 0, 0, 0 }),
                Is.Null, "five buildings promised, none present");
        }
    }
}
