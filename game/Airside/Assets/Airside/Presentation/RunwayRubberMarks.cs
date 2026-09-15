using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>One rubber deposit streak on the runway, in runway-frame metres.</summary>
    public readonly struct RubberStreak
    {
        public RubberStreak(float centreX, float centreZ, float length, float width, bool heavy)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            Length = length;
            Width = width;
            Heavy = heavy;
        }

        public float CentreX { get; }
        public float CentreZ { get; }
        public float Length { get; }
        public float Width { get; }

        /// <summary>Darker, fresher rubber rather than the lighter, older haze around it.</summary>
        public bool Heavy { get; }
    }

    /// <summary>
    /// Tyre rubber in the touchdown zones of 05/23: streaks clustered on the main-gear tracks
    /// either side of the centreline, thickest a few hundred metres past each threshold where
    /// wheels spin up, thinning towards the runway middle. Seeded, so every run and every build
    /// paints the same marks. Replaces four solid 180 m black slabs.
    /// </summary>
    public static class RunwayRubberMarks
    {
        public const int Seed = 5230;
        public const int StreaksAtWestEnd = 190;
        public const int StreaksAtEastEnd = 120;
        public const float NearestFromThreshold = 170f;
        public const float FurthestFromThreshold = 1050f;
        public const float PeakFromThreshold = 430f;

        /// <summary>Lateral gear-track offsets from the centreline, metres (turboprop and jet mains).</summary>
        public static readonly float[] GearTracks = { 2.1f, 3.3f, 4.6f, 5.9f };

        public const float MaxAbsZ = 11f;

        private static RubberStreak[] _cached;

        public static IReadOnlyList<RubberStreak> All() => _cached ??= Generate();

        /// <summary>A fresh copy of the marks; <see cref="All"/> caches one.</summary>
        public static RubberStreak[] Generate()
        {
            var random = new Random(Seed);
            var list = new List<RubberStreak>(StreaksAtWestEnd + StreaksAtEastEnd);
            AddEnd(list, random, AirsideRunwayMarkings.WestThresholdX, +1f, StreaksAtWestEnd);
            AddEnd(list, random, AirsideRunwayMarkings.EastThresholdX, -1f, StreaksAtEastEnd);
            return list.ToArray();
        }

        private static void AddEnd(List<RubberStreak> list, Random random, float thresholdX, float inward, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var distance = Triangular(random, NearestFromThreshold, PeakFromThreshold, FurthestFromThreshold);
                // Fewer, lighter marks further in: most tyres are already rolling by then.
                var intoRoll = (distance - NearestFromThreshold) / (FurthestFromThreshold - NearestFromThreshold);
                var heavy = random.NextDouble() > 0.35 + 0.5 * intoRoll;

                var track = GearTracks[random.Next(GearTracks.Length)];
                var side = random.Next(2) == 0 ? -1f : 1f;
                var z = side * track + Gaussian(random) * 0.9f;
                z = Math.Max(-MaxAbsZ, Math.Min(MaxAbsZ, z));

                var length = Lerp(14f, 70f, (float)random.NextDouble()) * (heavy ? 1f : 1.4f);
                var width = Lerp(0.35f, 1.3f, (float)random.NextDouble()) * (heavy ? 1f : 1.8f);
                list.Add(new RubberStreak(thresholdX + inward * distance, z, length, width, heavy));
            }
        }

        private static float Triangular(Random random, float min, float mode, float max)
        {
            var u = random.NextDouble();
            var split = (mode - min) / (max - min);
            return u < split
                ? (float)(min + Math.Sqrt(u * (max - min) * (mode - min)))
                : (float)(max - Math.Sqrt((1 - u) * (max - min) * (max - mode)));
        }

        private static float Gaussian(Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
