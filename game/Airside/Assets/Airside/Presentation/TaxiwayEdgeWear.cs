using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>A short darkened strip just inside one taxiway pavement edge.</summary>
    public readonly struct TaxiwayWearStrip
    {
        public TaxiwayWearStrip(float startX, float startZ, float endX, float endZ, float widthMetres = TaxiwayEdgeWear.WidthMetres)
        {
            StartX = startX;
            StartZ = startZ;
            EndX = endX;
            EndZ = endZ;
            Width = widthMetres;
        }

        public float StartX { get; }
        public float StartZ { get; }
        public float EndX { get; }
        public float EndZ { get; }
        public float Width { get; }
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
                var normalX = -dz;
                var normalZ = dx;

                for (var side = -1; side <= 1; side += 2)
                {
                    var phase = Phase(ax, az, bx, bz, side) * PatchStrideMetres;
                    var patchNumber = 0;
                    for (var patch = phase - PatchStrideMetres; patch < length;)
                    {
                        var variation = phase*.173f+patchNumber++*.618034f;
                        variation -= (float)Math.Floor(variation);
                        var patchLength = PatchLengthMetres*(.48f+variation*.52f);
                        var width = WidthMetres*(.55f+variation*.45f);
                        var start = Math.Max(0f, patch);
                        var end = Math.Min(length, patch + patchLength);
                        if (end-start >= 1.5f)
                        {
                            var offset = taxiwayHalfWidth-width*(.58f+variation*.4f);
                            var ox = normalX*offset*side; var oz = normalZ*offset*side;
                            result.Add(new TaxiwayWearStrip(ax+dx*start+ox,az+dz*start+oz,
                                ax+dx*end+ox,az+dz*end+oz,width));
                        }
                        patch += PatchStrideMetres*(.78f+variation*.44f);
                    }
                }
            }

            return result;
        }

        /// <summary>Tapered six-point wear island; every point stays inside the strip width.</summary>
        public static float[] Corners(TaxiwayWearStrip strip)
        {
            var length=strip.Length;
            if (length<1e-5f) return Array.Empty<float>();
            var dx=(strip.EndX-strip.StartX)/length; var dz=(strip.EndZ-strip.StartZ)/length;
            var seed=Phase(strip.StartX,strip.StartZ,strip.EndX,strip.EndZ,1);
            var mid=.37f+seed*.24f;
            var along=new[] {0f,mid,1f,1f,mid,0f};
            var across=new[] {-.2f,-.5f,-.28f,.18f,.5f,.25f};
            var result=new float[12];
            for(var i=0;i<6;i++)
            {
                result[i*2]=strip.StartX+dx*length*along[i]-dz*strip.Width*across[i];
                result[i*2+1]=strip.StartZ+dz*length*along[i]+dx*strip.Width*across[i];
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
