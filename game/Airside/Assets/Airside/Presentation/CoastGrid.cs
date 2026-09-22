using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// The surroundings heightfield around the airfield: a rectilinear grid, fine near the
    /// airport and coarse towards the far clip, with lines exactly on the airfield ground
    /// rectangle so the two meshes meet edge to edge. For every vertex it knows whether it
    /// is sea and how far it is from the real coastline. No UnityEngine, so it is tested
    /// headless; the Unity mesh is built from it in <c>AirsideAdelaideSurroundings</c>.
    /// </summary>
    public sealed class CoastGrid
    {
        public const float FineStepMetres = 35f;
        public const float FineExtentMetres = 6000f;
        public const float CoarseStepMetres = 180f;
        public const float ExtentMetres = 12000f;

        /// <summary>How far inside the airfield rectangle the surroundings tuck under it.</summary>
        public const float OverlapMetres = 40f;

        /// <summary>Distances past this are reported as this — nothing looks different further out.</summary>
        public const float MaxDistanceMetres = 600f;

        private readonly float[] _xs;
        private readonly float[] _zs;
        private readonly bool[] _sea;
        private readonly float[] _coastDistance;

        public CoastGrid(float[] seaPolygon, float[] coastline, float holeHalfX, float holeHalfZ)
        {
            HoleHalfX = holeHalfX;
            HoleHalfZ = holeHalfZ;
            _xs = Axis(holeHalfX);
            _zs = Axis(holeHalfZ);
            _sea = new bool[_xs.Length * _zs.Length];
            _coastDistance = new float[_xs.Length * _zs.Length];

            var crossings = new List<float>();
            var index = new SegmentIndex(coastline, MaxDistanceMetres);
            for (var zi = 0; zi < _zs.Length; zi++)
            {
                var z = _zs[zi];
                RowCrossings(seaPolygon, z, crossings);
                var next = 0;
                var inside = false;
                for (var xi = 0; xi < _xs.Length; xi++)
                {
                    var x = _xs[xi];
                    while (next < crossings.Count && crossings[next] <= x)
                    {
                        inside = !inside;
                        next++;
                    }

                    var i = zi * _xs.Length + xi;
                    _sea[i] = inside;
                    _coastDistance[i] = index.Distance(x, z);
                }
            }
        }

        public float HoleHalfX { get; }
        public float HoleHalfZ { get; }
        public int CountX => _xs.Length;
        public int CountZ => _zs.Length;
        public float X(int xi) => _xs[xi];
        public float Z(int zi) => _zs[zi];
        public bool IsSea(int xi, int zi) => _sea[zi * _xs.Length + xi];
        public float CoastDistance(int xi, int zi) => _coastDistance[zi * _xs.Length + xi];

        /// <summary>True when a vertex sits inside the airfield rectangle's tuck-under band.</summary>
        public bool IsTuckedUnder(int xi, int zi) =>
            Math.Abs(_xs[xi]) < HoleHalfX - 0.01f && Math.Abs(_zs[zi]) < HoleHalfZ - 0.01f;

        /// <summary>
        /// Cells wholly inside the airfield (bar the overlap band) are left out — the
        /// airfield ground mesh owns them.
        /// </summary>
        public bool IsCellCovered(int xi, int zi)
        {
            var innerX = HoleHalfX - OverlapMetres + 0.01f;
            var innerZ = HoleHalfZ - OverlapMetres + 0.01f;
            return Math.Abs(_xs[xi]) <= innerX && Math.Abs(_xs[xi + 1]) <= innerX
                   && Math.Abs(_zs[zi]) <= innerZ && Math.Abs(_zs[zi + 1]) <= innerZ;
        }

        /// <summary>Metres from (x, z) out to the airfield rectangle; 0 inside it.</summary>
        public float DistanceOutsideHole(float x, float z)
        {
            var dx = Math.Max(0f, Math.Abs(x) - HoleHalfX);
            var dz = Math.Max(0f, Math.Abs(z) - HoleHalfZ);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Grid lines for one axis: fine near the middle, coarse beyond, plus the hole edges.</summary>
        public static float[] Axis(float holeHalf)
        {
            var values = new SortedSet<float>();
            for (var v = -FineExtentMetres; v <= FineExtentMetres + 0.01f; v += FineStepMetres)
                values.Add((float)Math.Round(v, 2));
            for (var v = FineExtentMetres + CoarseStepMetres; v < ExtentMetres + CoarseStepMetres; v += CoarseStepMetres)
            {
                var clamped = Math.Min(v, ExtentMetres);
                values.Add(clamped);
                values.Add(-clamped);
            }

            foreach (var edge in new[] { holeHalf, holeHalf - OverlapMetres })
            {
                values.Add(edge);
                values.Add(-edge);
            }

            // Drop lines within half a metre of an exact edge so no sliver cells remain.
            var result = new List<float>();
            foreach (var v in values)
            {
                if (result.Count > 0 && v - result[result.Count - 1] < 0.5f)
                {
                    var isEdge = Math.Abs(Math.Abs(v) - holeHalf) < 0.01f || Math.Abs(Math.Abs(v) - (holeHalf - OverlapMetres)) < 0.01f;
                    if (isEdge)
                        result[result.Count - 1] = v;
                    continue;
                }

                result.Add(v);
            }

            return result.ToArray();
        }

        /// <summary>Sorted x where a closed polygon (x,z pairs) crosses the line at <paramref name="z"/>.</summary>
        public static void RowCrossings(float[] polygon, float z, List<float> crossings)
        {
            crossings.Clear();
            var count = polygon.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                float x0 = polygon[i * 2], z0 = polygon[i * 2 + 1], x1 = polygon[j * 2], z1 = polygon[j * 2 + 1];
                // Half-open rule so a vertex exactly on the row counts once.
                if (z0 <= z == z1 <= z)
                    continue;
                crossings.Add(x0 + (z - z0) / (z1 - z0) * (x1 - x0));
            }

            crossings.Sort();
        }

        /// <summary>Distance from a point to a polyline (x,z pairs), metres.</summary>
        public static float DistanceToPolyline(float[] polyline, float x, float z)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 3 < polyline.Length; i += 2)
                best = Math.Min(best, SegmentDistance(polyline[i], polyline[i + 1], polyline[i + 2], polyline[i + 3], x, z));
            return best;
        }

        private static float SegmentDistance(float ax, float az, float bx, float bz, float px, float pz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lengthSq = dx * dx + dz * dz;
            var t = lengthSq > 1e-6f ? ((px - ax) * dx + (pz - az) * dz) / lengthSq : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var ex = ax + dx * t - px;
            var ez = az + dz * t - pz;
            return (float)Math.Sqrt(ex * ex + ez * ez);
        }

        /// <summary>Buckets coastline segments so distance lookups only check nearby ones.</summary>
        private sealed class SegmentIndex
        {
            private readonly float[] _line;
            private readonly float _cell;
            private readonly float _cap;
            private readonly Dictionary<long, List<int>> _buckets = new();

            public SegmentIndex(float[] line, float cap)
            {
                _line = line;
                _cap = cap;
                _cell = Math.Max(250f, cap);
                for (var i = 0; i + 3 < line.Length; i += 2)
                {
                    var minX = Math.Min(line[i], line[i + 2]) - cap;
                    var maxX = Math.Max(line[i], line[i + 2]) + cap;
                    var minZ = Math.Min(line[i + 1], line[i + 3]) - cap;
                    var maxZ = Math.Max(line[i + 1], line[i + 3]) + cap;
                    for (var cx = Cell(minX); cx <= Cell(maxX); cx++)
                    for (var cz = Cell(minZ); cz <= Cell(maxZ); cz++)
                    {
                        var key = Key(cx, cz);
                        if (!_buckets.TryGetValue(key, out var list))
                            _buckets[key] = list = new List<int>();
                        list.Add(i);
                    }
                }
            }

            public float Distance(float x, float z)
            {
                if (!_buckets.TryGetValue(Key(Cell(x), Cell(z)), out var segments))
                    return _cap;
                var best = _cap;
                foreach (var i in segments)
                    best = Math.Min(best, SegmentDistance(_line[i], _line[i + 1], _line[i + 2], _line[i + 3], x, z));
                return best;
            }

            private int Cell(float v) => (int)Math.Floor(v / _cell);
            private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
        }
    }

    /// <summary>On-screen data credits, composed from what the scene actually uses.</summary>
    public static class MapAttribution
    {
        public const string OpenStreetMap = "Map data © OpenStreetMap contributors";

        /// <summary>
        /// The single credit line shown over the field: OSM drives the layout and the coast,
        /// and adsb.lol (also ODbL) is credited while its live aircraft are on screen.
        /// </summary>
        public static string FieldCredit(bool usesOsmLayout, bool usesOsmCoast,
            bool usesLiveTraffic = false, bool usesLiveWeather = false)
        {
            var map = usesOsmLayout || usesOsmCoast ? OpenStreetMap : string.Empty;
            var credit = map;
            if (usesLiveTraffic)
                credit = string.IsNullOrEmpty(credit) ? Airside.Simulation.LiveTraffic.Credit
                    : credit + "  ·  " + Airside.Simulation.LiveTraffic.Credit;
            if (usesLiveWeather)
                credit = string.IsNullOrEmpty(credit) ? Airside.Simulation.LiveWeather.Credit
                    : credit + "  ·  " + Airside.Simulation.LiveWeather.Credit;
            return credit;
        }
    }
}
