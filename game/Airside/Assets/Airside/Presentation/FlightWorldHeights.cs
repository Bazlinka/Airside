using System;
using System.IO;

namespace Airside.Presentation
{
    /// <summary>Small geographic grid. Missing/outside data returns false, never an edge-clamped mountain.</summary>
    public sealed class FlightWorldHeights
    {
        public const string ArtPath = "Terrain/dem_south_australia_v01.bin";
        private readonly short[] _values;
        private readonly int _width, _height;
        private readonly double _west, _south, _step;
        private FlightWorldHeights(int w, int h, double west, double south, double step, short[] values)
        { _width = w; _height = h; _west = west; _south = south; _step = step; _values = values; }
        public static FlightWorldHeights Parse(byte[] data)
        {
            if (data == null || data.Length < 40) return null;
            using var reader = new BinaryReader(new MemoryStream(data, false));
            if (reader.ReadUInt32() != 0x47544153 || reader.ReadInt32() != 1) return null;
            var w = reader.ReadInt32(); var h = reader.ReadInt32();
            var west = reader.ReadDouble(); var south = reader.ReadDouble(); var step = reader.ReadDouble();
            if (w < 2 || h < 2 || w > 4096 || h > 4096 || !double.IsFinite(west)
                || !double.IsFinite(south) || !double.IsFinite(step) || step <= 0
                || data.Length != 40L + (long)w * h * 2) return null;
            var values = new short[w * h];
            for (var i = 0; i < values.Length; i++) values[i] = reader.ReadInt16();
            return new FlightWorldHeights(w, h, west, south, step, values);
        }
        public bool TryHeight(double latitude, double longitude, out double metres)
        {
            metres = 0;
            var x = (longitude - _west) / _step; var z = (latitude - _south) / _step;
            if (!double.IsFinite(x) || !double.IsFinite(z) || x < 0 || z < 0 || x > _width-1 || z > _height-1) return false;
            var ix = Math.Min((int)x, _width-2); var iz = Math.Min((int)z, _height-2);
            var fx = x-ix; var fz = z-iz;
            var a = _values[iz*_width+ix]*(1-fx)+_values[iz*_width+ix+1]*fx;
            var b = _values[(iz+1)*_width+ix]*(1-fx)+_values[(iz+1)*_width+ix+1]*fx;
            metres = a*(1-fz)+b*fz;
            return true;
        }
    }
}
