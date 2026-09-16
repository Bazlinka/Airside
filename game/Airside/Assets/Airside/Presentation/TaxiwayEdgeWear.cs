using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>A short darkened strip just inside one taxiway pavement edge.</summary>
    public readonly struct TaxiwayWearStrip
    {
        public TaxiwayWearStrip(float startX, float startZ, float endX, float endZ)
        {
            StartX = startX;
            StartZ = startZ;
            EndX = endX;
            EndZ = endZ;
        }

        public float StartX { get; }
        public float StartZ { get; }
        public float EndX { get; }
        public float EndZ { get; }
        public float Length => (float)Math.Sqrt((EndX - StartX) * (EndX - StartX) + (EndZ - StartZ) * (EndZ - StartZ));
    }

    /// <summary>
    /// Builds sparse, deterministic edge darkening from a taxiway centreline. The strips sit
    /// inside the pavement and are visual only: they never change pavement, routes or collision.
    /// </summary>
    public static class TaxiwayEdgeWear
    {
        public const float WidthMetres = 0.32f;
        public const float PatchLengthMetres = 9f;
        public const float PatchStrideMetres = 23f;

        public static IReadOnlyList<TaxiwayWearStrip> Generate(float[] xz, float taxiwayHalfWidth)
        {
            var result = new List<TaxiwayWearStrip>();
            if (xz == null || xz.Length < 4 || xz.Length % 2 != 0 || taxiwayHalfWidth <= WidthMetres)
                return result;

            for (var i = 0; i + 3 < xz.Length; i += 2)
            {
                var ax = xz[i];
                var az = xz[i + 1];
                var bx = xz[i + 2];
                var bz = xz[i + 3];
                var dx = bx - ax;
                var dz = bz - az;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length < 1f)
                    continue;

                dx /= length;
                dz /= length;
                var insetOffset = taxiwayHalfWidth - WidthMetres * 0.65f;
                var normalX = -dz;
                var normalZ = dx;

                for (var side = -1; side <= 1; side += 2)
                {
                    var phase = Phase(ax, az, bx, bz, side) * PatchStrideMetres;
                    for (var patch = phase - PatchStrideMetres; patch < length; patch += PatchStrideMetres)
                    {
                        var start = Math.Max(0f, patch);
                        var end = Math.Min(length, patch + PatchLengthMetres);
                        if (end - start < 1.5f)
                            continue;

                        var ox = normalX * insetOffset * side;
                        var oz = normalZ * insetOffset * side;
                        result.Add(new TaxiwayWearStrip(
                            ax + dx * start + ox,
                            az + dz * start + oz,
                            ax + dx * end + ox,
                            az + dz * end + oz));
                    }
                }
            }

            return result;
        }

        private static float Phase(float ax, float az, float bx, float bz, int side)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)Math.Round(ax * 10f);
                hash = hash * 31 + (int)Math.Round(az * 10f);
                hash = hash * 31 + (int)Math.Round(bx * 10f);
                hash = hash * 31 + (int)Math.Round(bz * 10f);
                hash = hash * 31 + side;
                return (hash & 0x7fffffff) % 1000 / 1000f;
            }
        }
    }
}
