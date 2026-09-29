using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0185 — the land and Gulf beyond the 30 km far ring, out to about 96 km, for the zoomed-out camera: one mesh of 500 m
    /// cells on the far Copernicus DEM (Yorke Peninsula, the Gulf, the Fleurieu and the Mount Lofty Ranges), coloured by height
    /// and by sea. Pure (no UnityEngine): the arrays go straight into a Unity mesh, and the headless harness checks them.
    /// </summary>
    public static class AdelaideOuterTerrainGeometry
    {
        /// <summary>Every second DEM sample: 500 m cells, sub-pixel past 30 km.</summary>
        public const int Stride = 2;
        /// <summary>The far ring (AirsideAdelaideFarTerrain) owns the disc to 30 km; this mesh starts a little inside it.</summary>
        public const float InnerRadiusMetres = 29000f;
        /// <summary>Cells nearer than this to the centre sit under the far ring so the two never fight.</summary>
        public const float TuckRadiusMetres = 31500f;
        public const float TuckMetres = 8f;

        public sealed class Result
        {
            public float[] Positions;   // x, y, z
            public float[] Colours;     // linear r, g, b, a
            public int[] Triangles;
            public int VertexCount => Positions.Length / 3;
        }

        /// <param name="heightAt">Height above sea level of DEM sample (xi, zi); 0 is sea.</param>
        /// <param name="reliefAbovePlain">Metres of relief above the plain for a DEM height (AdelaideTerrainHeights.ReliefAbovePlain).</param>
        public static Result Build(int count, float spacing, float origin, Func<int, int, float> heightAt,
            Func<float, float> reliefAbovePlain, float plainY, float seaY,
            float[] plainLinear, float[] hillLinear, float[] seaLinear)
        {
            var n = (count - 1) / Stride + 1;
            var positions = new float[n * n * 3];
            var colours = new float[n * n * 4];
            var innerSq = InnerRadiusMetres * InnerRadiusMetres;
            var tuckSq = TuckRadiusMetres * TuckRadiusMetres;
            for (var zi = 0; zi < n; zi++)
            for (var xi = 0; xi < n; xi++)
            {
                var x = origin + xi * Stride * spacing;
                var z = origin + zi * Stride * spacing;
                var h = heightAt(xi * Stride, zi * Stride);
                var i = zi * n + xi;
                var tuck = x * x + z * z < tuckSq ? TuckMetres : 0f;
                float y;
                float[] c;
                float a;
                if (h <= 0.01f)
                {
                    y = seaY - tuck;
                    c = seaLinear;
                    a = 1f;
                }
                else
                {
                    y = plainY + reliefAbovePlain(h) - tuck;
                    var t = Smooth(h / 420f);
                    c = new[]
                    {
                        plainLinear[0] + (hillLinear[0] - plainLinear[0]) * t,
                        plainLinear[1] + (hillLinear[1] - plainLinear[1]) * t,
                        plainLinear[2] + (hillLinear[2] - plainLinear[2]) * t
                    };
                    a = 0f;
                }

                positions[i * 3] = x;
                positions[i * 3 + 1] = y;
                positions[i * 3 + 2] = z;
                colours[i * 4] = c[0];
                colours[i * 4 + 1] = c[1];
                colours[i * 4 + 2] = c[2];
                colours[i * 4 + 3] = a;
            }

            var triangles = new List<int>(n * n * 3);
            for (var zi = 0; zi < n - 1; zi++)
            for (var xi = 0; xi < n - 1; xi++)
            {
                float x0 = positions[(zi * n + xi) * 3], z0 = positions[(zi * n + xi) * 3 + 2];
                float x1 = x0 + Stride * spacing, z1 = z0 + Stride * spacing;
                if (WhollyInside(x0, z0, x1, z1, innerSq))
                    continue;
                int a0 = zi * n + xi, b = a0 + 1, c0 = a0 + n, d = c0 + 1;
                triangles.Add(a0); triangles.Add(c0); triangles.Add(b);
                triangles.Add(b); triangles.Add(c0); triangles.Add(d);
            }

            return new Result { Positions = positions, Colours = colours, Triangles = triangles.ToArray() };
        }

        /// <summary>True when all four corners of the cell lie inside the disc of squared radius <paramref name="radiusSq"/>.</summary>
        public static bool WhollyInside(float x0, float z0, float x1, float z1, float radiusSq) =>
            x0 * x0 + z0 * z0 <= radiusSq && x1 * x1 + z0 * z0 <= radiusSq
            && x0 * x0 + z1 * z1 <= radiusSq && x1 * x1 + z1 * z1 <= radiusSq;

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
