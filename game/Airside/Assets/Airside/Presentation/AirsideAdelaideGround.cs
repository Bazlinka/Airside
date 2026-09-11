using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Authored Adelaide bare-field landform as pure functions of world position.
    ///
    /// No UnityEngine types — the mesh builder and headless tests sample the same
    /// functions so the shipped ground and the thing under test cannot drift.
    /// Extents match <see cref="AirsideBareField"/>. This is not a stretch of the
    /// Kingscote Terrain prefab: footprint, plateau and layer scales are Adelaide's.
    /// </summary>
    public static class AirsideAdelaideGround
    {
        public static float SizeX => AirsideBareField.GroundLengthMetres;
        public static float SizeZ => AirsideBareField.GroundWidthMetres;

        /// <summary>Relief budget away from the runway. Kept shallow so overview stays flat.</summary>
        public const float SizeY = 6f;

        public const float CentreX = 0f;
        public const float CentreZ = 0f;

        public static float OriginX => CentreX - SizeX * 0.5f;
        public static float OriginZ => CentreZ - SizeZ * 0.5f;

        /// <summary>
        /// Top of the level operational strip. Matches the old cube top so the runway
        /// slab still sits a few centimetres proud without floating.
        /// </summary>
        public static float PavementWorldY =>
            AirsideBareField.GroundCenterY + AirsideBareField.GroundHeightMetres * 0.5f;

        public const float PavementNormalized = 0.42f;

        public static float OriginY => PavementWorldY - PavementNormalized * SizeY;

        /// <summary>
        /// Dead-level footprint covering 05/23, 12/30, Taxiway F and a margin for
        /// shoulders. Outside this box only, subtle relief is allowed.
        /// </summary>
        public static float PlateauMinX => -AirsideAdelaidePavement.PlateauHalfX;
        public static float PlateauMaxX => AirsideAdelaidePavement.PlateauHalfX;
        public static float PlateauMinZ => -AirsideAdelaidePavement.PlateauHalfZ;
        public static float PlateauMaxZ => AirsideAdelaidePavement.PlateauHalfZ;
        public const float PlateauFalloff = 120f;

        public const int LayerDryGrass = 0;
        public const int LayerGreenGrass = 1;
        public const int LayerWornDirt = 2;
        public const int LayerCount = 3;

        /// <summary>
        /// Metres per texture repeat. Non-harmonic so multi-scale layers never share seams.
        /// </summary>
        public static float TileSize(int layer) => layer switch
        {
            LayerDryGrass => 47f,
            LayerGreenGrass => 37f,
            LayerWornDirt => 29f,
            _ => 47f
        };

        public static string LayerName(int layer) => layer switch
        {
            LayerDryGrass => "drygrass",
            LayerGreenGrass => "greengrass",
            LayerWornDirt => "worndirt",
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };

        public static string LayerBasecolorPath(int layer) =>
            $"Textures/Terrain/tx_ground_{LayerName(layer)}_basecolor_v01.png";

        public static string LayerNormalPath(int layer) =>
            $"Textures/Terrain/tx_ground_{LayerName(layer)}_normal_v01.png";

        /// <summary>High mesh density; Medium may drop one step without losing the authored look.</summary>
        public const int HighResolutionX = 97;
        public const int HighResolutionZ = 65;
        public const int MediumResolutionX = 65;
        public const int MediumResolutionZ = 45;

        public static float DistanceToRunway(float worldX, float worldZ) =>
            AirsideAdelaidePavement.DistanceToPavement(worldX, worldZ);

        public static float PlateauMask(float worldX, float worldZ)
        {
            var dx = Math.Max(Math.Max(PlateauMinX - worldX, worldX - PlateauMaxX), 0f);
            var dz = Math.Max(Math.Max(PlateauMinZ - worldZ, worldZ - PlateauMaxZ), 0f);
            var d = (float)Math.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothStep(0f, PlateauFalloff, d);
        }

        public static float NormalizedHeight(float worldX, float worldZ)
        {
            var plateau = PlateauMask(worldX, worldZ);
            var h = Lerp(Landform(worldX, worldZ), PavementNormalized, plateau);
            return Clamp01(h);
        }

        public static float WorldHeight(float worldX, float worldZ) =>
            OriginY + NormalizedHeight(worldX, worldZ) * SizeY;

        /// <summary>
        /// True when the surface is dead level at pavement height — the operational strip.
        /// </summary>
        public static bool IsOperationallyFlat(float worldX, float worldZ) =>
            PlateauMask(worldX, worldZ) >= 0.999f
            && Math.Abs(WorldHeight(worldX, worldZ) - PavementWorldY) < 0.002f;

        public static void LayerWeights(float worldX, float worldZ, float[] weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (weights.Length < LayerCount)
                throw new ArgumentException("need " + LayerCount + " weights", nameof(weights));

            var d = DistanceToRunway(worldX, worldZ);
            // 4–12 m shoulder, irregular along the edge so it is not a constant stripe.
            var shoulder = 6.5f + 5.5f * WarpedFbm(worldX, worldZ, 55f, 3307, 3);
            var dirt = 1f - SmoothStep(0.8f, shoulder, d);

            // Irregular worn patches away from the strip — large and soft from overview.
            var wear = WarpedFbm(worldX, worldZ, 180f, 4409, 3);
            var outside = 1f - PlateauMask(worldX, worldZ);
            dirt = Math.Max(dirt, outside * SmoothStep(0.55f, 0.82f, wear) * 0.65f);
            dirt = Math.Max(dirt, SmoothStep(0.68f, 0.9f, WarpedFbm(worldX, worldZ, 95f, 2903, 2)) * 0.45f
                * SmoothStep(55f, 18f, d));
            dirt = Clamp01(dirt);

            var patch = WarpedFbm(worldX, worldZ, 210f, 5501, 2);
            // Restrained green accents — enough to read from overview, never dominant.
            var green = 0.45f * SmoothStep(0.55f, 0.78f, patch);
            green = Math.Max(green, 0.7f * Blob(worldX, worldZ, -620f, 420f, 280f)
                * SmoothStep(0.4f, 0.62f, patch));
            green = Math.Max(green, 0.65f * Blob(worldX, worldZ, 780f, -380f, 260f)
                * SmoothStep(0.38f, 0.6f, patch));
            green = Math.Max(green, 0.6f * Blob(worldX, worldZ, 180f, 620f, 220f)
                * SmoothStep(0.42f, 0.64f, patch));
            green *= SmoothStep(10f, 35f, d);
            green = Clamp01(green);

            var dirtW = dirt;
            var remaining = Math.Max(0f, 1f - dirtW);
            var greenW = green * remaining;
            var dryW = Math.Max(0f, remaining - greenW);

            var total = dirtW + greenW + dryW;
            if (total <= 1e-5f)
            {
                weights[LayerDryGrass] = 1f;
                weights[LayerGreenGrass] = 0f;
                weights[LayerWornDirt] = 0f;
                return;
            }

            weights[LayerDryGrass] = dryW / total;
            weights[LayerGreenGrass] = greenW / total;
            weights[LayerWornDirt] = dirtW / total;
        }

        public static float[] Coverage(int samplesX = 48, int samplesZ = 32)
        {
            var totals = new float[LayerCount];
            var w = new float[LayerCount];
            var n = 0;
            for (var zi = 0; zi < samplesZ; zi++)
            {
                var wz = OriginZ + SizeZ * (zi + 0.5f) / samplesZ;
                for (var xi = 0; xi < samplesX; xi++)
                {
                    var wx = OriginX + SizeX * (xi + 0.5f) / samplesX;
                    LayerWeights(wx, wz, w);
                    for (var l = 0; l < LayerCount; l++)
                        totals[l] += w[l];
                    n++;
                }
            }

            for (var l = 0; l < LayerCount; l++)
                totals[l] /= Math.Max(1, n);
            return totals;
        }

        private static float Landform(float worldX, float worldZ)
        {
            var metres = 0f;
            metres += (WarpedFbm(worldX, worldZ, 420f, 4211, 3) - 0.5f) * 1.8f;
            metres += (WarpedFbm(worldX, worldZ, 160f, 5323, 2) - 0.5f) * 0.55f;

            // Soft boundary lip so the 3400 × 2309 m rectangle does not read as a board.
            var edgeX = Math.Min(worldX - OriginX, OriginX + SizeX - worldX);
            var edgeZ = Math.Min(worldZ - OriginZ, OriginZ + SizeZ - worldZ);
            var edge = Math.Min(edgeX, edgeZ);
            metres -= 2.4f * (1f - SmoothStep(0f, 85f, edge));

            return PavementNormalized + metres / SizeY;
        }

        private static float Blob(float worldX, float worldZ, float cx, float cz, float radius)
        {
            var dx = worldX - cx;
            var dz = worldZ - cz;
            var d = (float)Math.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothStep(radius * 0.35f, radius, d);
        }

        private static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                var h = x * 374761393 + z * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }

        private static int FloorToInt(float v) => v >= 0f ? (int)v : (int)v - 1;

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float SmoothStep(float edge0, float edge1, float v)
        {
            if (Math.Abs(edge1 - edge0) < 1e-6f) return v < edge0 ? 0f : 1f;
            var t = Clamp01((v - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float ValueNoise(float x, float z, int seed)
        {
            var xi = FloorToInt(x);
            var zi = FloorToInt(z);
            var fx = x - xi;
            var fz = z - zi;
            var ux = fx * fx * (3f - 2f * fx);
            var uz = fz * fz * (3f - 2f * fz);
            var a = Hash(xi, zi, seed);
            var b = Hash(xi + 1, zi, seed);
            var c = Hash(xi, zi + 1, seed);
            var d = Hash(xi + 1, zi + 1, seed);
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uz);
        }

        private static float Fbm(float x, float z, int seed, int octaves)
        {
            var sum = 0f;
            var amp = 1f;
            var norm = 0f;
            var freq = 1f;
            for (var i = 0; i < octaves; i++)
            {
                sum += amp * ValueNoise(x * freq, z * freq, seed + i * 101);
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }

            return sum / norm;
        }

        private static float WarpedFbm(float x, float z, float featureMetres, int seed, int octaves = 4)
        {
            var wf = 1f / Math.Max(1f, featureMetres * 2.6f);
            var wx = x + 90f * (Fbm(x * wf, z * wf, seed + 7717, 2) - 0.5f);
            var wz = z + 90f * (Fbm(x * wf + 3.1f, z * wf - 1.7f, seed + 9931, 2) - 0.5f);
            var f = 1f / Math.Max(1f, featureMetres);
            return Fbm(wx * f, wz * f, seed, octaves);
        }
    }
}
