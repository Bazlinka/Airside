using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Canary Island date-palm silhouette for landside car-park rows (ADR 0215).
    /// Pure maths — <see cref="AdelaideCarParkGeometry.BuildPalms"/> draws the triangles.
    /// </summary>
    public static class AdelaidePalmGeometry
    {
        /// <summary>Hard cap including trunk prism.</summary>
        public const int MaxPalmTriangles = 36;

        public const int TrunkSides = 5;
        public const int MinFrondCount = 5;
        public const int MaxFrondCount = 7;

        /// <summary>Trunk height as a fraction of palm height.</summary>
        public const float TrunkHeightFrac = 0.78f;

        /// <summary>Trunk radius over palm height.</summary>
        public const float TrunkRadiusOverHeight = 0.035f;

        /// <summary>Frond reach over palm height.</summary>
        public const float CrownRadiusOverHeight = 0.28f;

        public readonly struct Frond
        {
            public readonly float YawRad;
            public readonly float PitchRad;
            public readonly float LengthFrac;
            public readonly float WidthFrac;

            public Frond(float yawRad, float pitchRad, float lengthFrac, float widthFrac)
            {
                YawRad = yawRad;
                PitchRad = pitchRad;
                LengthFrac = lengthFrac;
                WidthFrac = widthFrac;
            }

            public int TriangleCount => 2;
        }

        /// <summary>Deterministic 5–7 drooping fronds from seed position.</summary>
        public static Frond[] FrondsForSeed(float seedX, float seedZ)
        {
            var u = Frac(seedX * 0.113f + seedZ * 0.067f);
            var v = Frac(seedX * 0.041f - seedZ * 0.097f + 0.27f);
            var count = MinFrondCount + (int)(u * (MaxFrondCount - MinFrondCount + 1));
            if (count > MaxFrondCount)
                count = MaxFrondCount;
            var spin = v * (float)(Math.PI * 2.0);
            var fronds = new Frond[count];
            for (var i = 0; i < count; i++)
            {
                var yaw = spin + i * (float)(Math.PI * 2.0 / count);
                // Slight irregularity so neighbouring palms do not clone.
                yaw += (Frac(u * (i + 3f)) - 0.5f) * 0.35f;
                // Negative pitch = droop (date-palm umbrella, not a stiff pine).
                var pitch = -0.35f - Frac(v * (i + 5f)) * 0.25f;
                var length = 0.85f + Frac(u + v + i * 0.17f) * 0.25f;
                var width = 0.18f + Frac(v - u + i * 0.11f) * 0.08f;
                fronds[i] = new Frond(yaw, pitch, length, width);
            }

            return fronds;
        }

        public static int TriangleCount(Frond[] fronds)
        {
            // Five-sided trunk prism: 5 side quads (10 tris) + top fan approx as 5 tris → use 10 + 2 for a closed look via Box (10 tris).
            // Layout budget counts trunk as TrunkSides * 2 (sides only); drawer may use Box (10 tris) which stays under Max.
            var trunk = TrunkSides * 2;
            if (fronds == null || fronds.Length == 0)
                return trunk;
            var n = trunk;
            for (var i = 0; i < fronds.Length; i++)
                n += fronds[i].TriangleCount;
            return n;
        }

        private static float Frac(float x)
        {
            x -= (float)Math.Floor(x);
            return x < 0f ? x + 1f : x;
        }
    }
}
