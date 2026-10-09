using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Low multi-lobe coastal scrub bushes for West Beach dunes (ADR 0218).
    /// Two to three irregular lobes, at most 30 tris; no tall trunk.
    /// </summary>
    public static class AdelaideDuneScrubGeometry
    {
        public const int MaxBushTriangles = 30;
        public const int MaxLobes = 3;
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
            var h = site.HeightMetres;
            var r = site.RadiusMetres;
            var seed = Frac(site.X*.137f+site.Z*.071f);
            var lobes = seed > .56f ? MaxLobes : 2;
            for (var lobe = 0; lobe < lobes; lobe++)
            {
                var shape = Frac(seed*7.31f+lobe*.317f);
                var angle = site.YawRad+lobe*2.3f+shape*.7f;
                var offset = lobe == 0 ? 0f : r*(.22f+shape*.28f);
                var x = site.X+(float)Math.Cos(angle)*offset;
                var z = site.Z+(float)Math.Sin(angle)*offset;
                var radius = r*(lobe == 0 ? 1f : .48f+shape*.24f);
                var height = h*(lobe == 0 ? .90f+shape*.10f : .53f+shape*.27f);
                var tint = .88f+shape*.16f;
                var baseColour = lobe == 0 ? Leaf : LeafShade;
                var colour = new RoadColor(baseColour.R*tint,baseColour.G*tint,baseColour.B*tint,1f);
                DrawLobe(sink,x,o.Height(x,z)+o.YOffset,z,radius,height,angle,colour,shape);
            }
        }

        private static void DrawLobe(RoadMeshSink sink, float cx, float y0, float cz,
            float radius, float height, float spin, RoadColor colour, float shape)
        {
            var ring = y0 + height * 0.55f;
            var apex = y0 + height;
            var low = y0 + height * 0.08f;
            var sides = shape > .5f ? 5 : SidesPerLobe;
            var step = (float)(Math.PI * 2.0 / sides);
            for (var k = 0; k < sides; k++)
            {
                var a0 = spin + k * step;
                var a1 = spin + (k + 1) * step;
                var r0 = radius * (.75f+Frac(shape*11.3f+k*.379f)*.25f);
                var r1 = radius * (.75f+Frac(shape*11.3f+((k+1)%sides)*.379f)*.25f);
                var px = cx + (float)Math.Cos(a0) * r0;
                var pz = cz + (float)Math.Sin(a0) * r0;
                var qx = cx + (float)Math.Cos(a1) * r1;
                var qz = cz + (float)Math.Sin(a1) * r1;
                // Closed low scrub lobe with true outward face normals.
                sink.SolidTriangle(qx, ring, qz, px, ring, pz, cx+radius*.12f, apex, cz, colour);
                sink.SolidTriangle(px, ring, pz, qx, ring, qz, cx, low, cz, colour);
            }
        }

        private static float Frac(float value) => value-(float)Math.Floor(value);
    }
}
