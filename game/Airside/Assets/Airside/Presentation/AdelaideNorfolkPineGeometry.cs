using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Tiered conical Norfolk Island pine silhouette (ADR 0219).
    /// Three stacked rings + apex — distinct from eucalypt lobes. ≤ 28 tris.
    /// </summary>
    public static class AdelaideNorfolkPineGeometry
    {
        public const int MaxTreeTriangles = 28;
        public const int RingCount = 3;
        public const int Sides = 5;

        private static readonly RoadColor Trunk = RoadColor.Srgb(0.45f, 0.40f, 0.34f, 1f);
        private static readonly RoadColor Needle = RoadColor.Srgb(0.22f, 0.38f, 0.26f, 1f);
        private static readonly RoadColor NeedleShade = RoadColor.Srgb(0.18f, 0.32f, 0.22f, 1f);

        public static int Build(RoadMeshSink sink, RoadBuildOptions o)
        {
            var sites = AdelaideNorfolkPinePlacement.Sites();
            for (var i = 0; i < sites.Length; i++)
                Draw(sink, o, sites[i]);
            return sites.Length;
        }

        private static void Draw(RoadMeshSink sink, RoadBuildOptions o, AdelaideNorfolkPinePlacement.Site site)
        {
            var y0 = o.Height(site.X, site.Z) + o.YOffset;
            var h = site.HeightMetres;
            var trunkH = h * 0.22f;
            var trunkR = Math.Max(0.16f, h * 0.025f);
            var ux = (float)Math.Cos(site.YawRad);
            var uz = (float)Math.Sin(site.YawRad);
            sink.Box(site.X, y0, site.Z, ux, uz, trunkR, trunkR, trunkH, Trunk);

            var spin = Frac(site.X * 0.07f + site.Z * 0.11f) * (float)(Math.PI * 2.0);
            // Three tapering tiers: widest low, spike at top.
            for (var tier = 0; tier < RingCount; tier++)
            {
                var t0 = (tier + 0.15f) / (RingCount + 0.4f);
                var t1 = (tier + 1.05f) / (RingCount + 0.4f);
                var yLow = y0 + h * (0.18f + t0 * 0.78f);
                var yHigh = y0 + h * (0.18f + t1 * 0.78f);
                var rLow = h * 0.18f * (1f - t0 * 0.85f);
                var rHigh = h * 0.18f * (1f - t1 * 0.85f);
                var colour = tier % 2 == 0 ? Needle : NeedleShade;
                var step = (float)(Math.PI * 2.0 / Sides);
                for (var k = 0; k < Sides; k++)
                {
                    var a0 = spin + k * step;
                    var a1 = spin + (k + 1) * step;
                    var p0x = site.X + (float)Math.Cos(a0) * rLow;
                    var p0z = site.Z + (float)Math.Sin(a0) * rLow;
                    var p1x = site.X + (float)Math.Cos(a1) * rLow;
                    var p1z = site.Z + (float)Math.Sin(a1) * rLow;
                    var q0x = site.X + (float)Math.Cos(a0) * rHigh;
                    var q0z = site.Z + (float)Math.Sin(a0) * rHigh;
                    var q1x = site.X + (float)Math.Cos(a1) * rHigh;
                    var q1z = site.Z + (float)Math.Sin(a1) * rHigh;
                    // Two tris per facet (quad split) — 10 tris/tier × 3 = 30, over budget.
                    // Use a single upper triangle fan from apex of each tier instead.
                    sink.Tri(p0x, yLow, p0z, p1x, yLow, p1z, site.X, yHigh, site.Z, colour);
                }
            }
        }

        private static float Frac(float v)
        {
            v -= (float)Math.Floor(v);
            return v < 0f ? v + 1f : v;
        }
    }
}
