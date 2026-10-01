using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Bake-time LOD for NDVI suburb trees (ADR 0220). Pure maths — distance from
    /// ARP picks Full / Medium / Billboard crown detail so far suburbs stay cheap.
    /// </summary>
    public static class AdelaideTreeLod
    {
        public enum Detail
        {
            /// <summary>3-lobe eucalypt (ADR 0212), near the field.</summary>
            Full = 0,

            /// <summary>Primary lobe only — mid ring.</summary>
            Medium = 1,

            /// <summary>Crossed cards — far suburbs at overview.</summary>
            Billboard = 2
        }

        /// <summary>Full multi-lobe inside this range (metres from ARP).</summary>
        public const float FullRangeMetres = 1400f;

        /// <summary>Primary lobe only between Full and this range.</summary>
        public const float MediumRangeMetres = 2800f;

        public static float DistanceFromOrigin(float x, float z) =>
            (float)Math.Sqrt(x * x + z * z);

        public static Detail ForDistance(float distanceMetres)
        {
            if (distanceMetres <= FullRangeMetres)
                return Detail.Full;
            if (distanceMetres <= MediumRangeMetres)
                return Detail.Medium;
            return Detail.Billboard;
        }

        public static Detail ForPosition(float x, float z) =>
            ForDistance(DistanceFromOrigin(x, z));

        /// <summary>Crown triangle budget excluding trunk.</summary>
        public static int MaxCrownTriangles(Detail detail) => detail switch
        {
            Detail.Full => AdelaideTreeGeometry.MaxCrownTriangles,
            Detail.Medium => 12,
            Detail.Billboard => 4,
            _ => AdelaideTreeGeometry.MaxCrownTriangles
        };

        /// <summary>
        /// Lobes for Full/Medium. Billboard returns empty — drawer uses crossed cards.
        /// </summary>
        public static AdelaideTreeGeometry.Lobe[] LobesForDetail(float seedX, float seedZ, Detail detail)
        {
            var full = AdelaideTreeGeometry.LobesForSeed(seedX, seedZ);
            return detail switch
            {
                Detail.Full => full,
                Detail.Medium => new[] { full[0] },
                _ => Array.Empty<AdelaideTreeGeometry.Lobe>()
            };
        }
    }
}
