using System;
using System.IO;
using System.IO.Compression;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0250 — one land-cover class per 0.01 degree cell (~1 km) over South Australia
    /// (<see cref="FlightWorldGrid.Covered"/>), baked from ESA WorldCover 2021 by
    /// <c>scripts/generate-sa-landcover.py</c>. It colours the streamed flight/overview terrain by what is
    /// really on the ground. Classes are <see cref="AdelaideFarLandCover"/>'s. Pure (no UnityEngine).
    /// Missing or out-of-bounds data returns false, never an edge-clamped class.
    /// </summary>
    public sealed class FlightWorldLandCover
    {
        public const string ArtPath = "Terrain/landcover_south_australia_v01.bin";
        private const int HeaderBytes = 40;

        private readonly byte[] _cells;
        private readonly double _west, _south, _step;

        private FlightWorldLandCover(int width, int height, double west, double south, double step, byte[] cells)
        { Width = width; Height = height; _west = west; _south = south; _step = step; _cells = cells; }

        public int Width { get; }
        public int Height { get; }
        /// <summary>Cell size in degrees (0.01 in the shipped map).</summary>
        public double StepDegrees => _step;

        /// <summary>
        /// Header (SALC, version 1, width, height, west, south, step) then a raw-deflate stream of one byte per cell,
        /// row 0 southernmost. Null for anything else, including a stream that is short, long or holds a class above 7.
        /// </summary>
        public static FlightWorldLandCover Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes + 1
                || bytes[0] != 'S' || bytes[1] != 'A' || bytes[2] != 'L' || bytes[3] != 'C') return null;
            if (BitConverter.ToInt32(bytes, 4) != 1) return null;
            var width = BitConverter.ToInt32(bytes, 8);
            var height = BitConverter.ToInt32(bytes, 12);
            var west = BitConverter.ToDouble(bytes, 16);
            var south = BitConverter.ToDouble(bytes, 24);
            var step = BitConverter.ToDouble(bytes, 32);
            if (width < 1 || height < 1 || width > 4096 || height > 4096 || !double.IsFinite(west)
                || !double.IsFinite(south) || !double.IsFinite(step) || step <= 0) return null;
            var cells = new byte[width * height];
            try
            {
                using var source = new MemoryStream(bytes, HeaderBytes, bytes.Length - HeaderBytes);
                using var inflate = new DeflateStream(source, CompressionMode.Decompress);
                var read = 0;
                while (read < cells.Length)
                {
                    var n = inflate.Read(cells, read, cells.Length - read);
                    if (n <= 0) return null;
                    read += n;
                }
                if (inflate.ReadByte() != -1) return null;   // longer than width * height
            }
            catch (InvalidDataException) { return null; }
            foreach (var c in cells)
                if (c >= AdelaideFarLandCover.ClassCount) return null;
            return new FlightWorldLandCover(width, height, west, south, step, cells);
        }

        /// <summary>The class of the cell containing the point; false outside the grid.</summary>
        public bool TryClass(double latitude, double longitude, out int cls)
        {
            cls = 0;
            var x = (longitude - _west) / _step;
            var z = (latitude - _south) / _step;
            if (!double.IsFinite(x) || !double.IsFinite(z) || x < 0 || z < 0 || x >= Width || z >= Height) return false;
            cls = _cells[(int)z * Width + (int)x];
            return true;
        }

        /// <summary>The cell indices of the point (for stable per-cell variation), or false outside the grid.</summary>
        public bool TryCell(double latitude, double longitude, out int xi, out int zi)
        {
            xi = zi = 0;
            var x = (longitude - _west) / _step;
            var z = (latitude - _south) / _step;
            if (!double.IsFinite(x) || !double.IsFinite(z) || x < 0 || z < 0 || x >= Width || z >= Height) return false;
            xi = (int)x; zi = (int)z;
            return true;
        }
    }
}
