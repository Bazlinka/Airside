using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Multi-lobe eucalypt crown layout for NDVI suburb trees (ADR 0212).
    /// Pure maths — <c>AirsideAdelaideSuburbs.AddTree</c> draws the triangles.
    /// Matches the VEG-001 multi-lobe read without new mesh assets.
    /// </summary>
    public static class AdelaideTreeGeometry
    {
        /// <summary>Hard cap on crown triangles per tree (excluding trunk).</summary>
        public const int MaxCrownTriangles = 48;

        /// <summary>A eucalypt crown needs at least two lobes (not one hex blob).</summary>
        public const int MinLobeCount = 2;

        /// <summary>One faceted canopy lobe: widest ring sits in the upper half of the tree.</summary>
        public readonly struct Lobe
        {
            /// <summary>Horizontal offset as a fraction of crown radius.</summary>
            public readonly float OffsetXFrac;

            /// <summary>Horizontal offset as a fraction of crown radius.</summary>
            public readonly float OffsetZFrac;

            /// <summary>Lobe radius as a fraction of crown radius.</summary>
            public readonly float RadiusScale;

            /// <summary>Crown underside height as a fraction of tree height.</summary>
            public readonly float BottomHeightFrac;

            /// <summary>Widest ring height as a fraction of tree height (must be &gt; 0.5).</summary>
            public readonly float RingHeightFrac;

            /// <summary>Apex height as a fraction of tree height.</summary>
            public readonly float ApexHeightFrac;

            /// <summary>Number of facets around the ring (each facet = 2 triangles).</summary>
            public readonly int SideCount;

            public Lobe(
                float offsetXFrac,
                float offsetZFrac,
                float radiusScale,
                float bottomHeightFrac,
                float ringHeightFrac,
                float apexHeightFrac,
                int sideCount)
            {
                OffsetXFrac = offsetXFrac;
                OffsetZFrac = offsetZFrac;
                RadiusScale = radiusScale;
                BottomHeightFrac = bottomHeightFrac;
                RingHeightFrac = ringHeightFrac;
                ApexHeightFrac = apexHeightFrac;
                SideCount = sideCount;
            }

            public int TriangleCount => SideCount * 2;
        }

        /// <summary>
        /// Deterministic 3-lobe layout from tree position: primary dome + two side clusters.
        /// Primary uses 6 sides; sides use 4 — 28 crown triangles total.
        /// </summary>
        public static Lobe[] LobesForSeed(float seedX, float seedZ)
        {
            // Cheap hash → [0,1) so neighbouring trees do not share the same lobe spin.
            var u = Frac(seedX * 0.137f + seedZ * 0.071f);
            var v = Frac(seedX * 0.053f - seedZ * 0.119f + 0.31f);
            var yaw = u * (float)(Math.PI * 2.0);
            var cos = (float)Math.Cos(yaw);
            var sin = (float)Math.Sin(yaw);

            // Side-lobe offsets in crown-radius fractions, rotated by yaw.
            var aX = 0.42f + v * 0.08f;
            var aZ = -0.28f;
            var bX = -0.38f;
            var bZ = 0.32f - v * 0.06f;
            Rotate(ref aX, ref aZ, cos, sin);
            Rotate(ref bX, ref bZ, cos, sin);

            // Widest rings stay in the upper half (eucalypt dome, not a conifer spike).
            return new[]
            {
                new Lobe(0f, 0f, 1f, 0.32f, 0.72f, 1f, 6),
                new Lobe(aX, aZ, 0.55f, 0.38f, 0.68f, 0.88f, 4),
                new Lobe(bX, bZ, 0.48f, 0.40f, 0.70f, 0.90f, 4)
            };
        }

        /// <summary>Sum of facet triangles for a lobe set (must stay ≤ <see cref="MaxCrownTriangles"/>).</summary>
        public static int CrownTriangleCount(Lobe[] lobes)
        {
            if (lobes == null || lobes.Length == 0)
                return 0;
            var n = 0;
            for (var i = 0; i < lobes.Length; i++)
                n += lobes[i].TriangleCount;
            return n;
        }

        /// <summary>True when every lobe's widest ring sits above mid-height.</summary>
        public static bool WidestRingsInUpperHalf(Lobe[] lobes)
        {
            if (lobes == null || lobes.Length == 0)
                return false;
            for (var i = 0; i < lobes.Length; i++)
            {
                if (lobes[i].RingHeightFrac <= 0.5f)
                    return false;
            }

            return true;
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }

        private static void Rotate(ref float x, ref float z, float cos, float sin)
        {
            var nx = x * cos - z * sin;
            var nz = x * sin + z * cos;
            x = nx;
            z = nz;
        }
    }
}
