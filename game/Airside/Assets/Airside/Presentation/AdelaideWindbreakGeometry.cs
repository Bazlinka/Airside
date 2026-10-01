using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Draws Tapleys Hill Road windbreak eucalypts into the road props sink (ADR 0216).
    /// Reuses <see cref="AdelaideTreeGeometry"/> lobes — no new mesh assets.
    /// </summary>
    public static class AdelaideWindbreakGeometry
    {
        /// <summary>Crown + trunk budget per tree.</summary>
        public const int MaxTreeTriangles = 48;

        private static readonly RoadColor Trunk = RoadColor.Srgb(0.42f, 0.37f, 0.31f, 1f);
        private static readonly RoadColor Leaf = RoadColor.Srgb(0.29f, 0.35f, 0.22f, 1f);
        private static readonly RoadColor LeafShade = RoadColor.Srgb(0.24f, 0.30f, 0.18f, 1f);

        /// <summary>Returns trees drawn.</summary>
        public static int Build(RoadMeshSink sink, RoadBuildOptions o)
        {
            var sites = AdelaideWindbreakPlacement.Sites();
            for (var i = 0; i < sites.Length; i++)
                DrawTree(sink, o, sites[i]);
            return sites.Length;
        }

        private static void DrawTree(RoadMeshSink sink, RoadBuildOptions o, AdelaideWindbreakPlacement.Site site)
        {
            var y0 = o.Height(site.X, site.Z) + o.YOffset;
            var h = site.HeightMetres;
            var crownR = site.CrownRadius;
            var lobes = AdelaideTreeGeometry.LobesForSeed(site.X, site.Z);
            var trunkTop = y0 + h * lobes[0].BottomHeightFrac;
            var trunkR = Math.Max(0.18f, crownR * 0.08f);
            var ux = (float)Math.Cos(site.YawRad);
            var uz = (float)Math.Sin(site.YawRad);
            sink.Box(site.X, y0, site.Z, ux, uz, trunkR, trunkR, trunkTop - y0 + 0.3f, Trunk);

            var spin = Frac(site.X * 0.137f + site.Z * 0.071f) * (float)(Math.PI * 2.0);
            for (var li = 0; li < lobes.Length; li++)
            {
                var lobe = lobes[li];
                var cx = site.X + lobe.OffsetXFrac * crownR;
                var cz = site.Z + lobe.OffsetZFrac * crownR;
                var radius = crownR * lobe.RadiusScale;
                var bottom = y0 + h * lobe.BottomHeightFrac;
                var ring = y0 + h * lobe.RingHeightFrac;
                var apexY = y0 + h * lobe.ApexHeightFrac;
                var sides = lobe.SideCount;
                var step = (float)(Math.PI * 2.0 / sides);
                var topColour = li == 0 ? Leaf : RoadColor.Srgb(
                    Leaf.R * (0.92f - li * 0.04f),
                    Leaf.G * (0.92f - li * 0.04f),
                    Leaf.B * (0.92f - li * 0.04f),
                    1f);
                for (var k = 0; k < sides; k++)
                {
                    var a0 = spin + k * step;
                    var a1 = spin + (k + 1) * step;
                    var r0 = radius * (k % 2 == 0 ? 1f : 0.82f);
                    var r1 = radius * ((k + 1) % 2 == 0 ? 1f : 0.82f);
                    var px = cx + (float)Math.Cos(a0) * r0;
                    var pz = cz + (float)Math.Sin(a0) * r0;
                    var qx = cx + (float)Math.Cos(a1) * r1;
                    var qz = cz + (float)Math.Sin(a1) * r1;
                    sink.Tri(px, ring, pz, qx, ring, qz, cx, apexY, cz, topColour);
                    sink.Tri(px, ring, pz, qx, ring, qz, cx, bottom, cz, LeafShade);
                }
            }
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
