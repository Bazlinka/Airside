using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Disc corners for OSM golf bunkers (ADR 0207). Pure maths — Surroundings
    /// builds the sand mesh; never used for routing, collision or saves.
    /// </summary>
    public static class GolfBunkerMarks
    {
        public const int DiscSides = 8;

        public static float[] DiscCorners(float centreX, float centreZ, float radiusMetres,
            int sides = DiscSides)
        {
            sides = Math.Max(6, sides);
            radiusMetres = Math.Max(0.5f, radiusMetres);
            var corners = new float[sides * 2];
            for (var i = 0; i < sides; i++)
            {
                var a = i * (Math.PI * 2d / sides);
                corners[i * 2] = centreX + (float)Math.Cos(a) * radiusMetres;
                corners[i * 2 + 1] = centreZ + (float)Math.Sin(a) * radiusMetres;
            }

            return corners;
        }

        public static bool NearGlenelg(float centreX, float centreZ, float withinMetres = 120f)
        {
            var dx = centreX - (-839f);
            var dz = centreZ - (-924f);
            return dx * dx + dz * dz <= withinMetres * withinMetres;
        }

        public static int CountNearGlenelg()
        {
            var n = 0;
            for (var i = 0; i < AdelaideGolfBunkers.Count; i++)
            {
                AdelaideGolfBunkers.Get(i, out var x, out var z, out _);
                if (NearGlenelg(x, z))
                    n++;
            }

            return n;
        }
    }
}
