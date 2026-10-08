using System;
using System.IO;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0158: the baked Copernicus DEM reads back as Adelaide.</summary>
    public sealed class AdelaideTerrainHeightsTests
    {
        private static AdelaideTerrainHeights Load()
        {
            // Unity runs tests from the project folder, the dotnet harness from its bin folder.
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
        public void Grid_CoversTheThirtyKilometreFarClip()
        {
            var terrain = Load();
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.HalfExtent, Is.GreaterThanOrEqualTo(30_000f));
            Assert.That(terrain.Spacing, Is.LessThanOrEqualTo(150f));
        }

        [Test]
        public void Heights_ReadAsAdelaide()
        {
            var terrain = Load();
            // Runway-frame positions of real places (generate-ypad-layout.py convention).
            Assert.That(terrain.Height(0f, 0f), Is.InRange(0f, 12f), "the airfield sits a few metres above the Gulf");
            Assert.That(terrain.Height(-9152f, 7572f), Is.EqualTo(0f), "Gulf St Vincent 10 km west is sea level");
            Assert.That(terrain.Height(6437f, -2327f), Is.InRange(30f, 70f), "the CBD stands ~50 m up the plain");
            Assert.That(terrain.Height(10446f, -12880f), Is.InRange(600f, 740f), "Mount Lofty, the Hills skyline");
        }

        [Test]
        public void Relief_LeavesTheAirfieldEdgeAndCoastFlat()
        {
            Assert.That(AdelaideTerrainHeights.ReliefAbovePlain(2f), Is.EqualTo(0f), "low coastal ground adds nothing");
            Assert.That(AdelaideTerrainHeights.ReliefAbovePlain(50f), Is.EqualTo(50f - AdelaideTerrainHeights.PlainAboveSeaMetres));
            Assert.That(AdelaideTerrainHeights.ReliefWeight(0f), Is.EqualTo(0f));
            Assert.That(AdelaideTerrainHeights.ReliefWeight(AdelaideTerrainHeights.ReliefStartMetres), Is.EqualTo(0f));
            Assert.That(AdelaideTerrainHeights.ReliefWeight(AdelaideTerrainHeights.ReliefFullMetres), Is.EqualTo(1f));
            var terrain = Load();
            Assert.That(terrain.Relief(0f, 0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Credits_NameTheElevationAndImagerySources()
        {
            var credits = string.Join(" ", new[] { FlightManual.CreditsPageId, "terrain-sound-credits" }
                .SelectMany(id => FlightManual.Pages[FlightManual.IndexOf(id)].Sections).Select(s => s.Body));
            Assert.That(credits, Does.Contain("Copernicus DEM").And.Contain("DLR e.V.").And.Contain("Airbus"));
            Assert.That(credits, Does.Contain(MapAttribution.Sentinel));
            Assert.That(credits, Does.Contain("OpenStreetMap"));
        }

        [Test]
        public void Parse_RejectsAnythingElse()
        {
            Assert.That(AdelaideTerrainHeights.Parse(null), Is.Null);
            Assert.That(AdelaideTerrainHeights.Parse(new byte[8]), Is.Null);
            var truncated = new byte[40];
            new byte[] { (byte)'A', (byte)'D', (byte)'E', (byte)'M' }.CopyTo(truncated, 0);
            BitConverter.GetBytes(1).CopyTo(truncated, 4);
            BitConverter.GetBytes(513).CopyTo(truncated, 8);
            BitConverter.GetBytes(125f).CopyTo(truncated, 12);
            BitConverter.GetBytes(-32000f).CopyTo(truncated, 16);
            BitConverter.GetBytes(0.05f).CopyTo(truncated, 20);
            Assert.That(AdelaideTerrainHeights.Parse(truncated), Is.Null, "too short for its samples");
        }
    }
}
