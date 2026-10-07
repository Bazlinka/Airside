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
        /// <param name="landCover">Optional class map on the same grid (ADR 0190); land is coloured by class when given, by height alone when null.</param>
        /// <param name="reliefAbovePlain">Metres of relief above the plain for a DEM height (AdelaideTerrainHeights.ReliefAbovePlain).</param>
        public static Result Build(int count, float spacing, float origin, Func<int, int, float> heightAt,
            Func<float, float> reliefAbovePlain, float plainY, float seaY,
            float[] plainLinear, float[] hillLinear, float[] seaLinear, AdelaideFarLandCover landCover = null)
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
                    if (landCover != null)
                    {
                        var slope = Slope(heightAt, xi * Stride, zi * Stride, count, spacing);
                        c = new float[3];
                        a = LandCoverColour(landCover, xi * Stride, zi * Stride, slope, seaLinear, c);
                    }

                    // ADR 0210: cool aerial haze on distant / high land (not water).
                    if (a < 0.5f)
                    {
                        var dist = (float)Math.Sqrt(x * x + z * z);
                        var r = c[0];
                        var g = c[1];
                        var b = c[2];
                        AdelaideAerialPerspective.ApplyRgb(ref r, ref g, ref b, dist, h);
                        c = new[] { r, g, b };
                    }
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

        /// <summary>0 (flat) to 1 (about 25 degrees or steeper) from the height differences around a DEM sample.</summary>
        /// <summary>
        /// Land-cover colour of cell (xi, zi) into <paramref name="into"/>; returns the vertex alpha (water sheen). Shared with
        /// the far ring, which fades its satellite drape into this so the two meet in one colour.
        /// </summary>
        public static float LandCoverColour(AdelaideFarLandCover landCover, int xi, int zi, float slope, float[] seaLinear,
            float[] into)
        {
            var cls = landCover.ClassAt(xi, zi);
            if (cls == AdelaideFarLandCover.Water)
            {
                // A lake or reservoir: lit like the sea, a little lighter.
                into[0] = seaLinear[0] * 1.3f; into[1] = seaLinear[1] * 1.3f; into[2] = seaLinear[2] * 1.3f;
                return 0.7f;
            }

            AdelaideFarLandCover.Colour(cls, xi, zi, slope, into);
            return 0f;
        }

        public static float Slope(Func<int, int, float> heightAt, int xi, int zi, int count, float spacing)
        {
            var xa = Math.Max(0, xi - 1);
            var xb = Math.Min(count - 1, xi + 1);
            var za = Math.Max(0, zi - 1);
            var zb = Math.Min(count - 1, zi + 1);
            var dx = (heightAt(xb, zi) - heightAt(xa, zi)) / Math.Max(1f, (xb - xa) * spacing);
            var dz = (heightAt(xi, zb) - heightAt(xi, za)) / Math.Max(1f, (zb - za) * spacing);
            var tan = (float)Math.Sqrt(dx * dx + dz * dz);
            return Math.Max(0f, Math.Min(1f, tan / 0.47f));
        }

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
