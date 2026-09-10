using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideGroundTests
    {
        [Test]
        public void AdelaideGround_MatchesBareFieldExtents()
        {
            Assert.That(AirsideAdelaideGround.SizeX, Is.EqualTo(AirsideBareField.GroundLengthMetres));
            Assert.That(AirsideAdelaideGround.SizeZ, Is.EqualTo(AirsideBareField.GroundWidthMetres));
            Assert.That(AirsideAdelaideGround.OriginX, Is.EqualTo(-1700f));
            Assert.That(AirsideAdelaideGround.OriginZ, Is.EqualTo(-1154.5f));
        }

        [Test]
        public void AdelaideGround_RunwayFootprintIsDeadLevel()
        {
            float[] xs = { -1550f, -1250f, 0f, 700f, 1550f };
            float[] zs = { -22f, 0f, 22f };
            foreach (var x in xs)
            {
                foreach (var z in zs)
                {
                    Assert.That(AirsideAdelaideGround.IsOperationallyFlat(x, z), Is.True,
                        $"runway sample ({x},{z}) must stay level");
                    Assert.That(AirsideAdelaideGround.WorldHeight(x, z),
                        Is.EqualTo(AirsideAdelaideGround.PavementWorldY).Within(0.001f));
                }
            }
        }

        [Test]
        public void AdelaideGround_HasDirtShoulderBesideTheRunway()
        {
            var w = new float[AirsideAdelaideGround.LayerCount];
            AirsideAdelaideGround.LayerWeights(0f, 26f, w);
            Assert.That(w[AirsideAdelaideGround.LayerWornDirt], Is.GreaterThan(0.25f),
                "worn dirt should hug the runway edge");
            AirsideAdelaideGround.LayerWeights(0f, 220f, w);
            Assert.That(w[AirsideAdelaideGround.LayerDryGrass], Is.GreaterThan(0.45f),
                "open field away from the strip stays mostly dry grass");
        }

        [Test]
        public void AdelaideGround_CoverageIsMostlyDryGrassWithRestrainedAccents()
        {
            var c = AirsideAdelaideGround.Coverage();
            Assert.That(c[AirsideAdelaideGround.LayerDryGrass], Is.GreaterThan(0.55f));
            Assert.That(c[AirsideAdelaideGround.LayerGreenGrass], Is.GreaterThan(0.04f));
            Assert.That(c[AirsideAdelaideGround.LayerGreenGrass], Is.LessThan(0.35f));
            Assert.That(c[AirsideAdelaideGround.LayerWornDirt], Is.GreaterThan(0.04f));
            Assert.That(c[AirsideAdelaideGround.LayerWornDirt], Is.LessThan(0.35f));
            var sum = c[0] + c[1] + c[2];
            Assert.That(sum, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AdelaideGround_TileSizesAreNonHarmonic()
        {
            var dry = AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerDryGrass);
            var green = AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerGreenGrass);
            var dirt = AirsideAdelaideGround.TileSize(AirsideAdelaideGround.LayerWornDirt);
            Assert.That(dry, Is.Not.EqualTo(16f), "must not revive the old 16 m grid");
            Assert.That(dry % green, Is.Not.EqualTo(0f));
            Assert.That(dry % dirt, Is.Not.EqualTo(0f));
            Assert.That(green % dirt, Is.Not.EqualTo(0f));
        }

        [Test]
        public void AdelaideGround_BoundaryDropsAwayFromTheBoardEdge()
        {
            var centre = AirsideAdelaideGround.WorldHeight(0f, 400f);
            var edge = AirsideAdelaideGround.WorldHeight(0f, AirsideAdelaideGround.OriginZ + 8f);
            Assert.That(edge, Is.LessThan(centre - 0.5f));
        }
    }
}
