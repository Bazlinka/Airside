using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Checks the authored Kingscote landform.
    ///
    /// <see cref="AirsideTerrainField"/> deliberately holds no UnityEngine types, so
    /// these run both under Unity and in the headless dotnet harness. That matters:
    /// the terrain is baked in the Editor, and without a headless check the only way
    /// to know the bake is sane would be to open Unity.
    /// </summary>
    public sealed class TerrainFieldTests
    {
        // ---- Extents and resolution -----------------------------------------

        [Test]
        public void Extents_MatchTheApprovedTerrainSize()
        {
            Assert.That(AirsideTerrainField.SizeX, Is.EqualTo(256f));
            Assert.That(AirsideTerrainField.SizeZ, Is.EqualTo(220f));
            Assert.That(AirsideTerrainField.SizeY, Is.EqualTo(8f));
            Assert.That(AirsideTerrainField.HeightmapResolution, Is.EqualTo(257));
            Assert.That(AirsideTerrainField.AlphamapResolution, Is.EqualTo(256));
        }

        [Test]
        public void Extents_ContainEveryOperationalPadWithMargin()
        {
            // Parsed extents of the operational pads in AirsidePrototype.
            const float padMinX = -54.5f;
            const float padMaxX = 56f;
            const float padMinZ = -3.4f;
            const float padMaxZ = 52f;

            Assert.That(AirsideTerrainField.OriginX, Is.LessThan(padMinX - 20f));
            Assert.That(AirsideTerrainField.OriginX + AirsideTerrainField.SizeX, Is.GreaterThan(padMaxX + 20f));
            Assert.That(AirsideTerrainField.OriginZ, Is.LessThan(padMinZ - 20f));
            Assert.That(AirsideTerrainField.OriginZ + AirsideTerrainField.SizeZ, Is.GreaterThan(padMaxZ + 20f));

            // And the level plateau, not merely the terrain, has to contain them.
            Assert.That(AirsideTerrainField.PlateauMinX, Is.LessThan(padMinX));
            Assert.That(AirsideTerrainField.PlateauMaxX, Is.GreaterThan(padMaxX));
            Assert.That(AirsideTerrainField.PlateauMinZ, Is.LessThan(padMinZ));
            Assert.That(AirsideTerrainField.PlateauMaxZ, Is.GreaterThan(padMaxZ));
        }

        // ---- Height ----------------------------------------------------------

        [Test]
        public void Height_IsDeadLevelAcrossTheOperationalPlateau()
        {
            // Aircraft, vehicles and the taxi graph all live on this footprint and the
            // simulation owns their coordinates, so the terrain gets no opinion here.
            for (var x = AirsideTerrainField.PlateauMinX; x <= AirsideTerrainField.PlateauMaxX; x += 1f)
            {
                for (var z = AirsideTerrainField.PlateauMinZ; z <= AirsideTerrainField.PlateauMaxZ; z += 1f)
                {
                    Assert.That(AirsideTerrainField.WorldHeight(x, z),
                        Is.EqualTo(AirsideTerrainField.PavementWorldY).Within(1e-4f),
                        $"terrain is not level at ({x}, {z})");
                }
            }
        }

        [Test]
        public void Height_LeavesTheLowestPavementTwoToFiveCentimetresProud()
        {
            var clearance = AirsideTerrainField.LowestPavementTopY - AirsideTerrainField.PavementWorldY;
            Assert.That(clearance, Is.GreaterThanOrEqualTo(0.02f).And.LessThanOrEqualTo(0.05f),
                "pavement must sit 2-5 cm above the terrain: closer z-fights, further looks kerbed");
        }

        [Test]
        public void Height_NeverReachesTheHeightmapClamp()
        {
            // A clamped heightmap does not fail loudly, it silently flattens whole
            // regions into a plate, so leave real margin at both ends rather than just
            // staying inside 0..1.
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var zi = 0; zi < AirsideTerrainField.HeightmapResolution; zi++)
            {
                var z = AirsideTerrainField.OriginZ
                        + AirsideTerrainField.SizeZ * zi / (AirsideTerrainField.HeightmapResolution - 1f);
                for (var xi = 0; xi < AirsideTerrainField.HeightmapResolution; xi++)
                {
                    var x = AirsideTerrainField.OriginX
                            + AirsideTerrainField.SizeX * xi / (AirsideTerrainField.HeightmapResolution - 1f);
                    var h = AirsideTerrainField.NormalizedHeight(x, z);
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            Assert.That(min, Is.GreaterThan(0.02f), "heightmap bottoms out");
            Assert.That(max, Is.LessThan(0.98f), "heightmap tops out");
        }

        [Test]
        public void Height_SculptsOnlyGentleReliefOutsideThePlateau()
        {
            var heights = AirsideTerrainField.BuildHeights();
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var h in heights)
            {
                if (h < min) min = h;
                if (h > max) max = h;
            }

            var relief = (max - min) * AirsideTerrainField.SizeY;
            Assert.That(relief, Is.GreaterThan(1.5f), "the ground should not be a flat plate");
            Assert.That(relief, Is.LessThan(6f), "drainage and distant undulation only, not hills");
        }

        [Test]
        public void Height_IsContinuousSoNoTerraceStepsAppear()
        {
            var heights = AirsideTerrainField.BuildHeights();
            var res = AirsideTerrainField.HeightmapResolution;
            var stepX = AirsideTerrainField.SizeX / (res - 1f);
            var worst = 0f;

            for (var zi = 0; zi < res; zi++)
            {
                for (var xi = 1; xi < res; xi++)
                {
                    var slope = Math.Abs(heights[zi, xi] - heights[zi, xi - 1]) * AirsideTerrainField.SizeY / stepX;
                    if (slope > worst) worst = slope;
                }
            }

            Assert.That(worst, Is.LessThan(1f),
                "no sample-to-sample slope should exceed 45 degrees; a spike there reads as a terrace");
        }

        [Test]
        public void Height_IsDeterministic()
        {
            Assert.That(AirsideTerrainField.NormalizedHeight(-91.5f, -63.25f),
                Is.EqualTo(AirsideTerrainField.NormalizedHeight(-91.5f, -63.25f)));
            var a = AirsideTerrainField.BuildHeights();
            var b = AirsideTerrainField.BuildHeights();
            Assert.That(a[13, 200], Is.EqualTo(b[13, 200]));
            Assert.That(a[240, 7], Is.EqualTo(b[240, 7]));
        }

        // ---- Layer weights ---------------------------------------------------

        [Test]
        public void Weights_AreNonNegativeAndSumToOne()
        {
            var w = new float[AirsideTerrainField.LayerCount];
            for (var zi = 0; zi < AirsideTerrainField.AlphamapResolution; zi += 3)
            {
                var z = AirsideTerrainField.OriginZ
                        + AirsideTerrainField.SizeZ * (zi + 0.5f) / AirsideTerrainField.AlphamapResolution;
                for (var xi = 0; xi < AirsideTerrainField.AlphamapResolution; xi += 3)
                {
                    var x = AirsideTerrainField.OriginX
                            + AirsideTerrainField.SizeX * (xi + 0.5f) / AirsideTerrainField.AlphamapResolution;
                    AirsideTerrainField.LayerWeights(x, z, w);

                    var sum = 0f;
                    for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
                    {
                        Assert.That(w[l], Is.GreaterThanOrEqualTo(0f), $"negative weight at ({x}, {z})");
                        sum += w[l];
                    }

                    // An unnormalized splatmap shows as white or grey holes in the ground.
                    Assert.That(sum, Is.EqualTo(1f).Within(1e-3f), $"weights do not sum to 1 at ({x}, {z})");
                }
            }
        }

        [Test]
        public void Weights_KeepDryGrassDominantAcrossTheOverviewCore()
        {
            var coverage = CoreCoverage();

            Assert.That(coverage[AirsideTerrainField.LayerDryGrass], Is.GreaterThanOrEqualTo(0.60f)
                .And.LessThanOrEqualTo(0.75f), "dry grass should read as the dominant ground");
            Assert.That(coverage[AirsideTerrainField.LayerGreenGrass], Is.GreaterThan(0.05f)
                .And.LessThan(0.22f), "green is an accent near drainage and landscaping, not a second field");
            Assert.That(coverage[AirsideTerrainField.LayerWornDirt], Is.GreaterThan(0.05f),
                "shoulders and service wear should be visible from the overview camera");
        }

        [Test]
        public void Weights_UseAllFourLayersSomewhere()
        {
            var whole = AirsideTerrainField.Coverage();
            for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
            {
                Assert.That(whole[l], Is.GreaterThan(0.01f),
                    $"{AirsideTerrainField.LayerName(l)} is painted nowhere, so its TerrainLayer is dead weight");
            }
        }

        [Test]
        public void Weights_PaintATwoToFiveMetreDirtShoulderBesidePavement()
        {
            // Probe southward from the runway's south edge, away from Taxiway A and the
            // apron: inside another pad the distance-to-pavement is zero and the
            // shoulder reading would be meaningless.
            const float runwaySouthEdge = -3.4f;
            var w = new float[AirsideTerrainField.LayerCount];
            var min = float.MaxValue;
            var max = float.MinValue;

            for (var x = -40f; x <= 40f; x += 2f)
            {
                var width = 0f;
                for (var d = 0.25f; d <= 9f; d += 0.25f)
                {
                    AirsideTerrainField.LayerWeights(x, runwaySouthEdge - d, w);
                    if (w[AirsideTerrainField.LayerWornDirt] < 0.5f)
                        break;
                    width = d;
                }
                if (width < min) min = width;
                if (width > max) max = width;
            }

            Assert.That(min, Is.GreaterThanOrEqualTo(2f), "shoulder is too narrow somewhere along the runway");
            Assert.That(max, Is.LessThanOrEqualTo(5f), "shoulder has grown into a dirt field");
        }

        [Test]
        public void Weights_HandGrassOverToSandOnlyNearTheShore()
        {
            var w = new float[AirsideTerrainField.LayerCount];

            // On the airfield there should be no beach.
            for (var x = -60f; x <= 60f; x += 5f)
            {
                AirsideTerrainField.LayerWeights(x, 20f, w);
                Assert.That(w[AirsideTerrainField.LayerCoastSand], Is.LessThan(0.02f),
                    $"sand is bleeding onto the airfield at x {x}");
            }

            // Along the south edge it should dominate.
            var sand = 0f;
            var n = 0;
            for (var x = -110f; x <= 110f; x += 5f)
            {
                AirsideTerrainField.LayerWeights(x, -92f, w);
                sand += w[AirsideTerrainField.LayerCoastSand];
                n++;
            }
            Assert.That(sand / n, Is.GreaterThan(0.7f), "the coastline should be sand");
        }

        [Test]
        public void Weights_AreDeterministic()
        {
            var a = new float[AirsideTerrainField.LayerCount];
            var b = new float[AirsideTerrainField.LayerCount];
            AirsideTerrainField.LayerWeights(17.5f, -41.25f, a);
            AirsideTerrainField.LayerWeights(17.5f, -41.25f, b);
            for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
                Assert.That(a[l], Is.EqualTo(b[l]));
        }

        // ---- Repetition ------------------------------------------------------

        [Test]
        public void Layers_UseNonHarmonicTileSizes()
        {
            // Equal or doubled tile sizes make the layers agree about where their seams
            // are, and that agreement is what reads as a checker pattern.
            for (var a = 0; a < AirsideTerrainField.LayerCount; a++)
            {
                for (var b = a + 1; b < AirsideTerrainField.LayerCount; b++)
                {
                    var ta = AirsideTerrainField.TileSize(a);
                    var tb = AirsideTerrainField.TileSize(b);
                    Assert.That(ta, Is.Not.EqualTo(tb));

                    var ratio = Math.Max(ta, tb) / Math.Min(ta, tb);
                    Assert.That(Math.Abs(ratio - (float)Math.Round(ratio)), Is.GreaterThan(0.08f),
                        $"tile sizes {ta} and {tb} are near-integer multiples");
                }
            }
        }

        [Test]
        public void Splat_HasNoRepeatPeriodAtAnyLayerTileSize()
        {
            // A smooth field is naturally correlated at short lags, so an absolute
            // threshold would prove nothing. What a repeating pattern looks like is
            // correlation that falls and then climbs again at the repeat distance, so
            // the check is that decay is monotonic across every layer's tile size.
            var lags = new[] { 11f, 13f, 16f, 18f, 26f, 32f, 48f };
            var previous = 1f;

            foreach (var lag in lags)
            {
                var c = Correlation(lag);
                Assert.That(c, Is.LessThan(previous),
                    $"correlation climbs back at a {lag} m lag, which is a visible repeat");
                previous = c;
            }
        }

        // ---- Bake output shape -----------------------------------------------

        [Test]
        public void BuildHeights_MatchesTheHeightmapResolution()
        {
            var heights = AirsideTerrainField.BuildHeights();
            Assert.That(heights.GetLength(0), Is.EqualTo(AirsideTerrainField.HeightmapResolution));
            Assert.That(heights.GetLength(1), Is.EqualTo(AirsideTerrainField.HeightmapResolution));
        }

        [Test]
        public void BuildAlphamaps_MatchesTheAlphamapResolutionAndLayerCount()
        {
            var maps = AirsideTerrainField.BuildAlphamaps();
            Assert.That(maps.GetLength(0), Is.EqualTo(AirsideTerrainField.AlphamapResolution));
            Assert.That(maps.GetLength(1), Is.EqualTo(AirsideTerrainField.AlphamapResolution));
            Assert.That(maps.GetLength(2), Is.EqualTo(AirsideTerrainField.LayerCount));
        }

        [Test]
        public void LayerNames_AreDistinctAndFileSafe()
        {
            var seen = new string[AirsideTerrainField.LayerCount];
            for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
            {
                var name = AirsideTerrainField.LayerName(l);
                Assert.That(name, Is.Not.Null.And.Not.Empty);
                Assert.That(name, Is.EqualTo(name.ToLowerInvariant()));
                Assert.That(Array.IndexOf(seen, name), Is.LessThan(0), $"duplicate layer name {name}");
                seen[l] = name;
            }
        }

        // ---- Helpers ---------------------------------------------------------

        /// <summary>
        /// Mean coverage over what the overview camera actually frames, rather than the
        /// whole 220 m terrain. A quarter of the terrain is beach, which would drag the
        /// dry-grass share below its target even though no player ever sees the ground
        /// that way.
        /// </summary>
        private static float[] CoreCoverage()
        {
            var totals = new float[AirsideTerrainField.LayerCount];
            var w = new float[AirsideTerrainField.LayerCount];
            var n = 0;

            for (var z = -30f; z <= 70f; z += 1f)
            {
                for (var x = -80f; x <= 80f; x += 1f)
                {
                    AirsideTerrainField.LayerWeights(x, z, w);
                    for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
                        totals[l] += w[l];
                    n++;
                }
            }

            for (var l = 0; l < AirsideTerrainField.LayerCount; l++)
                totals[l] /= Math.Max(1, n);
            return totals;
        }

        /// <summary>
        /// Pearson correlation of the dry-grass weight against itself, shifted by
        /// <paramref name="lag"/> metres along both axes.
        /// </summary>
        private static float Correlation(float lag)
        {
            var w = new float[AirsideTerrainField.LayerCount];
            double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0;
            var n = 0;

            for (var z = -60f; z <= 80f; z += 2f)
            {
                for (var x = -100f; x <= 100f; x += 2f)
                {
                    AirsideTerrainField.LayerWeights(x, z, w);
                    var a = w[AirsideTerrainField.LayerDryGrass];
                    AirsideTerrainField.LayerWeights(x + lag, z + lag, w);
                    var b = w[AirsideTerrainField.LayerDryGrass];

                    sa += a; sb += b;
                    saa += a * a; sbb += b * b; sab += a * b;
                    n++;
                }
            }

            var cov = sab / n - sa / n * (sb / n);
            var va = saa / n - sa / n * (sa / n);
            var vb = sbb / n - sb / n * (sb / n);
            if (va <= 0 || vb <= 0)
                return 0f;
            return (float)(cov / Math.Sqrt(va * vb));
        }
    }
}
