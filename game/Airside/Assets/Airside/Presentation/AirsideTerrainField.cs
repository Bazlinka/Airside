using System;

namespace Airside.Presentation
{
    /// <summary>
    /// The authored Kingscote landform, as pure functions of world position.
    ///
    /// This deliberately holds no UnityEngine types. The Editor baker
    /// (<c>AirsideTerrainBaker</c>) samples it once to write a TerrainData asset, and
    /// the EditMode tests sample the same functions headlessly, so the shipped ground
    /// and the thing under test cannot drift apart. Nothing here runs in the packaged
    /// player: the player loads the baked asset.
    ///
    /// Two rules drive every constant below.
    ///
    /// The operational plateau is dead level. Runways, taxiways, stands and the taxi
    /// graph all sit on it, and the simulation owns those coordinates, so the terrain
    /// is not allowed to have an opinion about height anywhere an aircraft or vehicle
    /// can go. Relief lives strictly outside that footprint.
    ///
    /// Blending is broad, irregular and driven by domain-warped noise at low
    /// frequency. Layer weights use different, non-harmonic feature sizes so the four
    /// layers never line up into the checker pattern the flat slabs used to show.
    /// </summary>
    public static class AirsideTerrainField
    {
        // ---- Terrain extents -------------------------------------------------

        /// <summary>Terrain width in metres (world X).</summary>
        public const float SizeX = 256f;

        /// <summary>Terrain length in metres (world Z).</summary>
        public const float SizeZ = 220f;

        /// <summary>Full height range in metres. Only a fraction is used; see <see cref="PavementNormalized"/>.</summary>
        public const float SizeY = 8f;

        public const int HeightmapResolution = 257;
        public const int AlphamapResolution = 256;
        public const int BasemapResolution = 1024;
        public const float HeightmapPixelError = 8f;

        /// <summary>
        /// The terrain is centred where the retired flat base slab was centred, so the
        /// airfield keeps the same relationship to the coast and the distant hills.
        /// </summary>
        public const float CentreX = 0f;
        public const float CentreZ = 10f;

        public static float OriginX => CentreX - SizeX * 0.5f;
        public static float OriginZ => CentreZ - SizeZ * 0.5f;

        // ---- The operational plateau ----------------------------------------

        /// <summary>
        /// World Y of the level ground the pavement sits on.
        ///
        /// The lowest operational pad top in the scene is Runway blast W/E at -0.010,
        /// so -0.045 leaves it 3.5 cm proud — inside the 2–5 cm the packet asks for and
        /// clear of z-fighting. Pads whose tops are already higher (the apron at +0.060,
        /// Stand 3 at +0.095) stand further proud than 5 cm. That is deliberate: their Y
        /// is existing operational geometry and moving it would shift gameplay surfaces,
        /// which this change is not allowed to do.
        /// </summary>
        public const float PavementWorldY = -0.045f;

        /// <summary>
        /// Where the plateau sits in the 0..1 heightmap.
        ///
        /// 0.36 leaves 2.88 m of range below the airfield, which is what the deepest
        /// combination of boundary lip, drainage swale and downward undulation actually
        /// needs (about 2.58 m). At 0.25 the south-west corner ran past the clamp, and a
        /// clamped heightmap does not fail loudly — it silently flattens the corner into
        /// a plate. Above the plateau the worst case is about 2.16 m, well inside range.
        /// </summary>
        public const float PavementNormalized = 0.36f;

        public static float OriginY => PavementWorldY - PavementNormalized * SizeY;

        /// <summary>
        /// Flat footprint, generous around every operational pad. Parsed extents of all
        /// operational geometry are X [-54.5, 56.0], Z [-3.4, 52.0]; the taxi graph and
        /// stands (Z up to 34) sit inside that. The plateau adds margin on every side so
        /// no aircraft, vehicle or pad can ever end up below ground.
        /// </summary>
        public const float PlateauMinX = -64f;
        public const float PlateauMaxX = 66f;
        public const float PlateauMinZ = -12f;
        public const float PlateauMaxZ = 60f;

        /// <summary>Metres over which the plateau eases into the surrounding landform.</summary>
        public const float PlateauFalloff = 26f;

        // ---- Layers ----------------------------------------------------------

        public const int LayerDryGrass = 0;
        public const int LayerGreenGrass = 1;
        public const int LayerWornDirt = 2;
        public const int LayerCoastSand = 3;
        public const int LayerCount = 4;

        /// <summary>
        /// Metres per texture repeat, per layer. Deliberately different and
        /// non-harmonic: equal or doubled tile sizes make the layers agree about where
        /// their seams are, which is what reads as a checker pattern.
        /// </summary>
        public static float TileSize(int layer) => layer switch
        {
            LayerDryGrass => 16f,
            LayerGreenGrass => 13f,
            LayerWornDirt => 11f,
            LayerCoastSand => 18f,
            _ => 16f
        };

        /// <summary>Terrain layers are dielectric ground; metallic stays at zero.</summary>
        public static float Metallic(int layer) => 0f;

        public static float Smoothness(int layer) => layer switch
        {
            LayerDryGrass => 0.12f,
            LayerGreenGrass => 0.18f,
            LayerWornDirt => 0.08f,
            LayerCoastSand => 0.05f,
            _ => 0.1f
        };

        public static string LayerName(int layer) => layer switch
        {
            LayerDryGrass => "drygrass",
            LayerGreenGrass => "greengrass",
            LayerWornDirt => "worndirt",
            LayerCoastSand => "coastsand",
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };

        // ---- Operational pads, for painting shoulders ------------------------

        // cx, cz, halfX, halfZ — the pads a dirt shoulder should hug. Taken from the
        // CreateBlock calls in AirsidePrototype; only load-bearing pavement is listed,
        // not paint strips, because a shoulder belongs to the slab and not to its
        // markings.
        private static readonly float[,] Pads =
        {
            { 0f, 0f, 48f, 3.4f },       // Runway W
            { -50.5f, 0f, 4f, 3.2f },    // Runway blast W
            { 50.5f, 0f, 4f, 3.2f },     // Runway blast E
            { 8f, 9f, 32f, 2.1f },       // Taxiway A
            { 20f, 18f, 14f, 8f },       // Apron
            { 40f, 18.6f, 9f, 11f },     // Stand 3 apron
            { 18f, 18.6f, 13f, 4f },     // Stand 3 taxi lead
        };

        /// <summary>
        /// Metres from the nearest operational pad edge; 0 when inside a pad.
        /// </summary>
        public static float DistanceToPavement(float worldX, float worldZ)
        {
            var best = float.MaxValue;
            for (var i = 0; i < Pads.GetLength(0); i++)
            {
                var dx = Math.Abs(worldX - Pads[i, 0]) - Pads[i, 2];
                var dz = Math.Abs(worldZ - Pads[i, 1]) - Pads[i, 3];
                if (dx < 0f) dx = 0f;
                if (dz < 0f) dz = 0f;
                var d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            return best;
        }

        // ---- Noise -----------------------------------------------------------

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

        /// <summary>
        /// Fractal noise. The frequency step is 2.03 rather than 2 so octaves do not
        /// share grid lines, which is what turns stacked value noise into visible
        /// axis-aligned blocks.
        /// </summary>
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

        /// <summary>Fractal noise sampled through a warped domain, so patch edges wander.</summary>
        private static float WarpedFbm(float x, float z, float featureMetres, int seed, int octaves = 4)
        {
            var wf = 1f / Math.Max(1f, featureMetres * 2.6f);
            var wx = x + 26f * (Fbm(x * wf, z * wf, seed + 7717, 2) - 0.5f);
            var wz = z + 26f * (Fbm(x * wf + 3.1f, z * wf - 1.7f, seed + 9931, 2) - 0.5f);
            var f = 1f / Math.Max(1f, featureMetres);
            return Fbm(wx * f, wz * f, seed, octaves);
        }

        // ---- Height ----------------------------------------------------------

        /// <summary>1 inside the level operational footprint, easing to 0 outside it.</summary>
        public static float PlateauMask(float worldX, float worldZ)
        {
            var dx = Math.Max(Math.Max(PlateauMinX - worldX, worldX - PlateauMaxX), 0f);
            var dz = Math.Max(Math.Max(PlateauMinZ - worldZ, worldZ - PlateauMaxZ), 0f);
            var d = (float)Math.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothStep(0f, PlateauFalloff, d);
        }

        /// <summary>
        /// The landform outside the plateau, in normalized heightmap units.
        /// Gentle only: a coastal descent south, a slow inland rise north, two shallow
        /// drainage lines, low undulation, and a lip that drops at the terrain boundary
        /// so the edge tucks under the existing context scenery instead of standing as a
        /// visible wall.
        /// </summary>
        private static float Landform(float worldX, float worldZ)
        {
            var metres = 0f;

            // Inland (north) rises slowly toward the distant hills.
            metres += 1.55f * SmoothStep(46f, 118f, worldZ);

            // Broad undulation. Large features only — this is landform, not surface noise.
            metres += (WarpedFbm(worldX, worldZ, 62f, 4211, 3) - 0.5f) * 0.9f;
            metres += (WarpedFbm(worldX, worldZ, 26f, 5323, 3) - 0.5f) * 0.32f;

            // Two shallow drainage lines carrying water off the airfield to the south.
            metres -= 0.42f * Swale(worldX, worldZ, -34f, 6f);
            metres -= 0.36f * Swale(worldX, worldZ, 52f, 5f);

            // The coastal fall and the boundary lip are both "the ground drops away
            // here", and along the south edge they coincide. Taking the deeper of the two
            // rather than adding them keeps the result inside the 0..1 heightmap; adding
            // them drove the south-west corner past the clamp, which flattens the very
            // corner into a plate.
            var coast = 1.35f * SmoothStep(-18f, -74f, worldZ);
            var edgeX = Math.Min(worldX - OriginX, OriginX + SizeX - worldX);
            var edgeZ = Math.Min(worldZ - OriginZ, OriginZ + SizeZ - worldZ);
            var edge = Math.Min(edgeX, edgeZ);
            var lip = 1.55f * (1f - SmoothStep(0f, 15f, edge));
            metres -= Math.Max(coast, lip);

            return PavementNormalized + metres / SizeY;
        }

        /// <summary>
        /// A soft channel running roughly north-south, wandering with noise so it does
        /// not read as a straight ditch.
        /// </summary>
        private static float Swale(float worldX, float worldZ, float centreX, float halfWidth)
        {
            var wander = (WarpedFbm(worldX, worldZ, 48f, 6421, 2) - 0.5f) * 26f;
            var d = Math.Abs(worldX - (centreX + wander));
            return 1f - SmoothStep(0f, halfWidth, d);
        }

        /// <summary>
        /// Normalized (0..1) heightmap value at a world position. Exactly
        /// <see cref="PavementNormalized"/> anywhere inside the operational plateau.
        /// </summary>
        public static float NormalizedHeight(float worldX, float worldZ)
        {
            var plateau = PlateauMask(worldX, worldZ);
            var h = Lerp(Landform(worldX, worldZ), PavementNormalized, plateau);
            return Clamp01(h);
        }

        /// <summary>World Y of the terrain surface at a world position.</summary>
        public static float WorldHeight(float worldX, float worldZ)
            => OriginY + NormalizedHeight(worldX, worldZ) * SizeY;

        // ---- Layer weights ---------------------------------------------------

        /// <summary>
        /// Layer weights at a world position, normalized to sum to 1. Order matches the
        /// Layer* constants.
        /// </summary>
        public static void LayerWeights(float worldX, float worldZ, float[] weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (weights.Length < LayerCount) throw new ArgumentException("need " + LayerCount + " weights", nameof(weights));

            // Dry grass is the ground everywhere until something else takes over.
            var dry = 1f;

            // --- Coastal sand: only toward the shoreline.
            // The dune edge wanders on two scales. One scale gives a wavy but still
            // recognisably straight line across the map; the long wavelength makes the
            // shore advance and retreat in bays, and the short one roughens each bay.
            var duneEdge = (WarpedFbm(worldX, worldZ, 96f, 1811, 3) - 0.5f) * 58f
                         + (WarpedFbm(worldX, worldZ, 31f, 1949, 3) - 0.5f) * 22f;
            var sand = SmoothStep(-34f + duneEdge, -68f + duneEdge, worldZ);
            // A few blow-outs where scrub thins, so the transition is not one clean band.
            sand += 0.5f * SmoothStep(0.74f, 0.94f, WarpedFbm(worldX, worldZ, 26f, 2903, 3))
                         * SmoothStep(0f, -40f, worldZ);
            sand = Clamp01(sand);

            // --- Worn dirt: shoulders beside pavement, plus broad service wear.
            var d = DistanceToPavement(worldX, worldZ);
            // 2-5 m, varying along the edge so the shoulder is not a constant-width stripe.
            var shoulder = 2.4f + 2.5f * WarpedFbm(worldX, worldZ, 17f, 3307, 3);
            var dirt = 1f - SmoothStep(shoulder * 0.55f, shoulder, d);

            // Wider irregular dirt under scrub and toward the dunes, away from the field.
            var wear = WarpedFbm(worldX, worldZ, 34f, 4409, 4);
            var outside = 1f - PlateauMask(worldX, worldZ);
            dirt = Math.Max(dirt, outside * SmoothStep(0.62f, 0.88f, wear));
            // Dirt also carries the grass-to-sand handover instead of butting them together.
            dirt = Math.Max(dirt, 0.85f * SmoothStep(0.12f, 0.5f, sand) * (1f - SmoothStep(0.5f, 0.9f, sand)));
            dirt = Clamp01(dirt);

            // --- Green grass: restrained accent where water collects or gets shade.
            // Feature sizes are large and octave counts low. An earlier pass used 23 m
            // with four octaves, which put most of the noise energy inside a few metres
            // and made the green read as speckle rather than as patches large enough to
            // recognise from the overview camera.
            var patch = WarpedFbm(worldX, worldZ, 44f, 5501, 2);

            // Drainage. The swales run north-south, so gating green on the swale alone
            // paints a full-length vertical ribbon down the map. A second coarse mask
            // along the channel breaks it into the few pockets where water actually
            // stands long enough to keep the grass green.
            var damp = Math.Max(Swale(worldX, worldZ, -34f, 15f), Swale(worldX, worldZ, 52f, 13f));
            var pockets = SmoothStep(0.46f, 0.64f, WarpedFbm(worldX, worldZ, 54f, 5717, 2));
            var green = 0.95f * damp * pockets;
            // Terminal landscaping, north of the apron.
            green = Math.Max(green, 0.9f * Blob(worldX, worldZ, 26f, 40f, 26f) * SmoothStep(0.30f, 0.54f, patch));
            green = Math.Max(green, 0.85f * Blob(worldX, worldZ, -6f, 46f, 23f) * SmoothStep(0.30f, 0.54f, patch));
            // Shaded scrub pockets inland.
            green = Math.Max(green, 0.8f * SmoothStep(0.58f, 0.78f, WarpedFbm(worldX, worldZ, 48f, 6607, 2))
                                        * SmoothStep(-10f, 26f, worldZ));
            // Grass of any kind gives way to sand near the shore.
            green *= 1f - Clamp01(sand);
            green = Clamp01(green);

            // Sand and dirt overwrite grass rather than averaging with it, so edges stay
            // legible instead of turning into uniform mud.
            var sandW = sand;
            var dirtW = dirt * (1f - sandW);
            var remaining = Math.Max(0f, 1f - sandW - dirtW);
            var greenW = green * remaining;
            var dryW = Math.Max(0f, remaining - greenW) * dry;

            var total = sandW + dirtW + greenW + dryW;
            if (total <= 1e-5f)
            {
                weights[LayerDryGrass] = 1f;
                weights[LayerGreenGrass] = 0f;
                weights[LayerWornDirt] = 0f;
                weights[LayerCoastSand] = 0f;
                return;
            }

            weights[LayerDryGrass] = dryW / total;
            weights[LayerGreenGrass] = greenW / total;
            weights[LayerWornDirt] = dirtW / total;
            weights[LayerCoastSand] = sandW / total;
        }

        /// <summary>A soft round patch, used for hand-placed landscaping.</summary>
        private static float Blob(float worldX, float worldZ, float cx, float cz, float radius)
        {
            var dx = worldX - cx;
            var dz = worldZ - cz;
            var d = (float)Math.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothStep(radius * 0.35f, radius, d);
        }

        // ---- Bake helpers ----------------------------------------------------

        /// <summary>
        /// Heights for <c>TerrainData.SetHeights</c>. Unity indexes this [z, x].
        /// </summary>
        public static float[,] BuildHeights()
        {
            var res = HeightmapResolution;
            var heights = new float[res, res];
            for (var zi = 0; zi < res; zi++)
            {
                var wz = OriginZ + SizeZ * zi / (res - 1f);
                for (var xi = 0; xi < res; xi++)
                {
                    var wx = OriginX + SizeX * xi / (res - 1f);
                    heights[zi, xi] = NormalizedHeight(wx, wz);
                }
            }
            return heights;
        }

        /// <summary>
        /// Splat weights for <c>TerrainData.SetAlphamaps</c>. Unity indexes this
        /// [z, x, layer]. Samples at cell centres, which is where the terrain shader
        /// reads them.
        /// </summary>
        public static float[,,] BuildAlphamaps()
        {
            var res = AlphamapResolution;
            var maps = new float[res, res, LayerCount];
            var w = new float[LayerCount];
            for (var zi = 0; zi < res; zi++)
            {
                var wz = OriginZ + SizeZ * (zi + 0.5f) / res;
                for (var xi = 0; xi < res; xi++)
                {
                    var wx = OriginX + SizeX * (xi + 0.5f) / res;
                    LayerWeights(wx, wz, w);
                    for (var l = 0; l < LayerCount; l++)
                        maps[zi, xi, l] = w[l];
                }
            }
            return maps;
        }

        /// <summary>
        /// Mean coverage per layer across the whole terrain, for tests and for the bake
        /// report. Index with the Layer* constants.
        /// </summary>
        public static float[] Coverage(int step = 2)
        {
            if (step < 1) step = 1;
            var totals = new float[LayerCount];
            var w = new float[LayerCount];
            var n = 0;
            for (var zi = 0; zi < AlphamapResolution; zi += step)
            {
                var wz = OriginZ + SizeZ * (zi + 0.5f) / AlphamapResolution;
                for (var xi = 0; xi < AlphamapResolution; xi += step)
                {
                    var wx = OriginX + SizeX * (xi + 0.5f) / AlphamapResolution;
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
    }
}
