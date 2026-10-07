using System;
using System.IO;
using System.IO.Compression;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0250 — the state-wide land-cover map that colours the streamed flight/overview terrain.</summary>
    public sealed class FlightWorldLandCoverTests
    {
        // 3 x 2 cells: row 0 (south) = 0 1 2, row 1 (north) = 3 4 5. West 130, south -36, step 0.5.
        private static byte[] Blob(byte[] cells, int width = 3, int height = 2, int version = 1, double step = 0.5, string magic = "SALC")
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(magic));
            writer.Write(version); writer.Write(width); writer.Write(height);
            writer.Write(130.0); writer.Write(-36.0); writer.Write(step);
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                deflate.Write(cells, 0, cells.Length);
            writer.Flush();
            return output.ToArray();
        }

        private static readonly byte[] Grid = { 0, 1, 2, 3, 4, 5 };

        [Test]
        public void Parse_ReadsCellsSouthRowFirst()
        {
            var cover = FlightWorldLandCover.Parse(Blob(Grid));
            Assert.That(cover, Is.Not.Null);
            Assert.That(cover.Width, Is.EqualTo(3));
            Assert.That(cover.Height, Is.EqualTo(2));
            // Cell [ix, iz] covers west + ix*step .. +step and south + iz*step .. +step.
            Assert.That(cover.TryClass(-35.9, 130.1, out var c) && c == 0, Is.True, "south-west");
            Assert.That(cover.TryClass(-35.9, 131.1, out c) && c == 2, Is.True, "south-east");
            Assert.That(cover.TryClass(-35.4, 130.1, out c) && c == 3, Is.True, "north-west");
            Assert.That(cover.TryClass(-35.4, 131.1, out c) && c == 5, Is.True, "north-east");
        }

        [Test]
        public void TryClass_IsFalseOutsideTheGridNeverAnEdgeClamp()
        {
            var cover = FlightWorldLandCover.Parse(Blob(Grid));
            Assert.That(cover.TryClass(-35.9, 130.0, out _), Is.True, "west edge is inside");
            Assert.That(cover.TryClass(-35.9, 129.99, out _), Is.False);
            Assert.That(cover.TryClass(-35.9, 131.5, out _), Is.False, "east edge is exclusive");
            Assert.That(cover.TryClass(-36.01, 130.1, out _), Is.False);
            Assert.That(cover.TryClass(-35.0, 130.1, out _), Is.False, "north edge is exclusive");
            Assert.That(cover.TryClass(double.NaN, 130.1, out _), Is.False);
            Assert.That(cover.TryClass(-35.5, double.PositiveInfinity, out _), Is.False);
            Assert.That(cover.TryCell(-35.4, 131.1, out var xi, out var zi) && xi == 2 && zi == 1, Is.True);
        }

        [Test]
        public void Parse_RejectsDataItCannotTrust()
        {
            Assert.That(FlightWorldLandCover.Parse(null), Is.Null);
            Assert.That(FlightWorldLandCover.Parse(new byte[10]), Is.Null);
            Assert.That(FlightWorldLandCover.Parse(Blob(Grid, magic: "XXXX")), Is.Null, "magic");
            Assert.That(FlightWorldLandCover.Parse(Blob(Grid, version: 2)), Is.Null, "version");
            Assert.That(FlightWorldLandCover.Parse(Blob(Grid, step: 0)), Is.Null, "step");
            Assert.That(FlightWorldLandCover.Parse(Blob(Grid, step: double.NaN)), Is.Null, "NaN step");
            Assert.That(FlightWorldLandCover.Parse(Blob(Grid, width: 5000, height: 1)), Is.Null, "absurd width");
            Assert.That(FlightWorldLandCover.Parse(Blob(new byte[] { 0, 1, 2, 3, 4 })), Is.Null, "stream shorter than the grid");
            Assert.That(FlightWorldLandCover.Parse(Blob(new byte[] { 0, 1, 2, 3, 4, 5, 0 })), Is.Null, "stream longer than the grid");
            Assert.That(FlightWorldLandCover.Parse(Blob(new byte[] { 0, 1, 2, 3, 4, 8 })), Is.Null, "class above 7");
            var truncated = Blob(Grid);
            Array.Resize(ref truncated, truncated.Length - 3);
            Assert.That(FlightWorldLandCover.Parse(truncated), Is.Null, "truncated deflate");
        }

        [Test]
        public void ShippedMapCoversTheWholeStateAndKnownPlacesReadRight()
        {
            var relative = Path.Combine("game", "Airside", "Assets", "Airside", "Art", FlightWorldLandCover.ArtPath);
            FlightWorldLandCover cover = null;
            for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, relative);
                if (File.Exists(path)) { cover = FlightWorldLandCover.Parse(File.ReadAllBytes(path)); break; }
            }

            Assert.That(cover, Is.Not.Null, "shipped landcover_south_australia_v01.bin parses");
            Assert.That(cover.Width, Is.EqualTo(1400));
            Assert.That(cover.Height, Is.EqualTo(1400));
            // Everything FlightWorldGrid.Covered allows must have data.
            foreach (var corner in new[] { (-38.99, 128.01), (-25.01, 128.01), (-38.99, 141.99), (-25.01, 141.99) })
            {
                Assert.That(FlightWorldGrid.Covered(corner.Item1, corner.Item2), Is.True);
                Assert.That(cover.TryClass(corner.Item1, corner.Item2, out _), Is.True, corner.ToString());
            }

            void Expect(string place, double lat, double lon, int expected)
            {
                Assert.That(cover.TryClass(lat, lon, out var cls), Is.True, place);
                Assert.That(cls, Is.EqualTo(expected), place);
            }

            const int water = AdelaideFarLandCover.Water;
            Expect("Spencer Gulf", -34.2, 137.2, water);
            Expect("Gulf St Vincent", -35.1, 138.2, water);
            Expect("Southern Ocean", -38.5, 134.0, water);
            Expect("Great Australian Bight", -34.5, 131.0, water);
            Expect("Lake Eyre", -28.4, 137.3, AdelaideFarLandCover.Bare);
            Expect("Lake Torrens", -31.1, 137.8, AdelaideFarLandCover.Bare);
            Expect("Yorke Peninsula cropping", -34.3, 137.6, AdelaideFarLandCover.Crop);
            Expect("Adelaide plains", -34.9, 138.55, AdelaideFarLandCover.Built);
        }

        [Test]
        public void LandCoverColour_ByClassMatchesTheAdelaideRingsAndFlagsInlandWater()
        {
            var sea = new[] { 0.02f, 0.04f, 0.08f };
            var into = new float[3];
            var viaMap = new float[3];
            // A class-based lookup must equal the classic one for every class, so the two landscapes meet in one palette.
            for (var cls = 1; cls < AdelaideFarLandCover.ClassCount; cls++)
            {
                var alpha = AdelaideOuterTerrainGeometry.LandCoverColour(cls, 7, 11, 0f, sea, into);
                AdelaideFarLandCover.Colour(cls, 7, 11, 0f, viaMap);
                Assert.That(alpha, Is.EqualTo(0f), "land is not water");
                Assert.That(into, Is.EqualTo(viaMap).AsCollection, $"class {cls}");
            }

            var water = AdelaideOuterTerrainGeometry.LandCoverColour(AdelaideFarLandCover.Water, 1, 1, 0f, sea, into);
            Assert.That(water, Is.EqualTo(0.7f), "inland water carries the water flag");
            Assert.That(into[2], Is.EqualTo(sea[2] * 1.3f).Within(1e-6f));
        }
    }
}
