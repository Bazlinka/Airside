using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Low multi-lobe coastal scrub bushes for West Beach dunes (ADR 0218).
    /// Two 4-sided lobes → 16 tris; no tall trunk.
    /// </summary>
    public static class AdelaideDuneScrubGeometry
    {
        public const int MaxBushTriangles = 16;
        public const int LobeCount = 2;
        public const int SidesPerLobe = 4;

        private static readonly RoadColor Leaf = RoadColor.Srgb(0.48f, 0.52f, 0.36f, 1f);
        private static readonly RoadColor LeafShade = RoadColor.Srgb(0.40f, 0.44f, 0.30f, 1f);

        public static int Build(RoadMeshSink sink, RoadBuildOptions o)
        {
            var sites = AdelaideDuneScrubPlacement.Sites();
            for (var i = 0; i < sites.Length; i++)
                DrawBush(sink, o, sites[i]);
            return sites.Length;
        }

        private static void DrawBush(RoadMeshSink sink, RoadBuildOptions o, AdelaideDuneScrubPlacement.Site site)
        {
            var y0 = o.Height(site.X, site.Z) + o.YOffset;
            var h = site.HeightMetres;
            var r = site.RadiusMetres;
            // Two low lobes: primary + slightly offset side clump.
            DrawLobe(sink, site.X, y0, site.Z, r, h, site.YawRad, Leaf);
            var ox = (float)Math.Cos(site.YawRad + 1.1f) * r * 0.45f;
            var oz = (float)Math.Sin(site.YawRad + 1.1f) * r * 0.45f;
            DrawLobe(sink, site.X + ox, y0, site.Z + oz, r * 0.7f, h * 0.85f, site.YawRad + 0.6f, LeafShade);
        }

        private static void DrawLobe(RoadMeshSink sink, float cx, float y0, float cz,
            float radius, float height, float spin, RoadColor colour)
        {
            var ring = y0 + height * 0.55f;
            var apex = y0 + height;
            var low = y0 + height * 0.08f;
            var step = (float)(Math.PI * 2.0 / SidesPerLobe);
            for (var k = 0; k < SidesPerLobe; k++)
            {
                var a0 = spin + k * step;
                var a1 = spin + (k + 1) * step;
                var r0 = radius * (k % 2 == 0 ? 1f : 0.82f);
                var r1 = radius * ((k + 1) % 2 == 0 ? 1f : 0.82f);
                var px = cx + (float)Math.Cos(a0) * r0;
                var pz = cz + (float)Math.Sin(a0) * r0;
                var qx = cx + (float)Math.Cos(a1) * r1;
                var qz = cz + (float)Math.Sin(a1) * r1;
                // Upper facets only (low scrub silhouette) — 2 tris × 4 sides = 8 per lobe.
                sink.Tri(px, ring, pz, qx, ring, qz, cx, apex, cz, colour);
                sink.Tri(px, ring, pz, qx, ring, qz, cx, low, cz, colour);
            }
        }
    }
}
