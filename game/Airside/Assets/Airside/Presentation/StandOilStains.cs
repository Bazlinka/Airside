using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One oil / fuel stain on an apron stand, in runway-frame metres.</summary>
    public readonly struct OilStain
    {
        public OilStain(float centreX, float centreZ, float radiusAlong, float radiusAcross,
            float yawDegrees, bool heavy)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            RadiusAlong = radiusAlong;
            RadiusAcross = radiusAcross;
            YawDegrees = yawDegrees;
            Heavy = heavy;
        }

        public float CentreX { get; }
        public float CentreZ { get; }
        /// <summary>Half-length along the parked nose heading.</summary>
        public float RadiusAlong { get; }
        /// <summary>Half-width across the stand.</summary>
        public float RadiusAcross { get; }
        public float YawDegrees { get; }
        /// <summary>Fresh dark drip versus a lighter older haze.</summary>
        public bool Heavy { get; }
    }

    /// <summary>
    /// Seeded oil and fuel stains under parked aircraft at every regional bay and
    /// terminal gate (visual overhaul Phase 1). Presentation only — never used for
    /// routing, collision or saves. Positions sit under the main gear / APU / GPU
    /// spots relative to the stand stop.
    /// </summary>
    public static class StandOilStains
    {
        public const int Seed = 7741;
        public const int PatchesPerRegional = 3;
        public const int PatchesPerTerminal = 4;
        public const int EllipseSides = 8;

        private static OilStain[] _cached;

        public static IReadOnlyList<OilStain> All() => _cached ??= Generate();

        public static OilStain[] Generate()
        {
            var random = new Random(Seed);
            var list = new List<OilStain>(
                AdelaideLayout.Bays.Length * PatchesPerRegional
                + AdelaideGateAlignment.Gates.Length * PatchesPerTerminal);

            foreach (var bay in AdelaideLayout.Bays)
                AddStand(list, random, bay.StopX, bay.StopZ, bay.HeadingDegrees,
                    PatchesPerRegional, regional: true);
            foreach (var gate in AdelaideGateAlignment.Gates)
                AddStand(list, random, gate.NoseX, gate.NoseZ, gate.HeadingDegrees,
                    PatchesPerTerminal, regional: false);

            return list.ToArray();
        }

        /// <summary>
        /// Axis-aligned ellipse corners in world x/z, rotated by the stain yaw.
        /// Eight sides is enough for a soft blotch without a new mesh asset.
        /// </summary>
        public static float[] EllipseCorners(OilStain stain, int sides = EllipseSides)
        {
            sides = Math.Max(6, sides);
            var yaw = stain.YawDegrees * Math.PI / 180d;
            var cos = (float)Math.Cos(yaw);
            var sin = (float)Math.Sin(yaw);
            var corners = new float[sides * 2];
            for (var i = 0; i < sides; i++)
            {
                var a = i * (Math.PI * 2d / sides);
                var lx = (float)Math.Cos(a) * stain.RadiusAcross;
                var lz = (float)Math.Sin(a) * stain.RadiusAlong;
                // Rotate local (across, along) into world using the stand yaw basis.
                corners[i * 2] = stain.CentreX + lx * cos + lz * sin;
                corners[i * 2 + 1] = stain.CentreZ - lx * sin + lz * cos;
            }

            return corners;
        }

        private static void AddStand(List<OilStain> list, Random random,
            float stopX, float stopZ, float headingDegrees, int count, bool regional)
        {
            var heading = headingDegrees * Math.PI / 180d;
            var noseX = (float)Math.Sin(heading);
            var noseZ = (float)Math.Cos(heading);
            var acrossX = -noseZ;
            var acrossZ = noseX;

            // Typical drip zones aft of the stop: mains, APU, GPU / hydrant.
            var aftMin = regional ? 4f : 8f;
            var aftMax = regional ? 18f : 36f;
            var acrossMax = regional ? 5.5f : 9f;

            for (var i = 0; i < count; i++)
            {
                var aft = Lerp(aftMin, aftMax, (float)random.NextDouble());
                var side = ((float)random.NextDouble() * 2f - 1f) * acrossMax
                    * (0.35f + 0.65f * (float)random.NextDouble());
                var cx = stopX - noseX * aft + acrossX * side;
                var cz = stopZ - noseZ * aft + acrossZ * side;

                var heavy = random.NextDouble() > (regional ? 0.55 : 0.4);
                var along = Lerp(heavy ? 1.1f : 1.8f, heavy ? 2.6f : 4.2f, (float)random.NextDouble());
                var across = Lerp(heavy ? 0.7f : 1.2f, heavy ? 1.8f : 3.0f, (float)random.NextDouble());
                if (!regional)
                {
                    along *= 1.35f;
                    across *= 1.25f;
                }

                // Slight yaw jitter so stains are not all stand-aligned bricks.
                var yaw = headingDegrees + Lerp(-18f, 18f, (float)random.NextDouble());
                list.Add(new OilStain(cx, cz, along, across, yaw, heavy));
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
