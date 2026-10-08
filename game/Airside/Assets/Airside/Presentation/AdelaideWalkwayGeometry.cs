using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0187 — marked passenger walkways across the apron, edged with posts and red-and-white barrier tape, as at a
    /// real airport: people walking to an aircraft are fenced in and cannot wander onto a taxiway or the runway. One
    /// straight corridor runs from the terminal wall to a point short of each regional bay's aircraft; the boarding
    /// walk follows it (<see cref="TryCorridor"/>). The tape itself is temporary: it is put up along the walk while
    /// passengers are using it (<see cref="BuildAlong"/>), not left standing. Pure (no UnityEngine).
    /// </summary>
    public static class AdelaideWalkwayGeometry
    {
        public const float HalfWidthMetres = 1.9f;
        public const float PostSpacingMetres = 3f;
        public const float PostHeightMetres = 1.0f;
        public const float TapeHeightMetres = 0.92f;
        public const float StripeMetres = 1.5f;

        /// <summary>The corridor stops this far from the stand's nose datum; the last steps go round the aircraft.</summary>
        public const float StopShortMetres = 32f;

        /// <summary>A longer walk than this is not fenced (the passengers are bussed).</summary>
        public const float MaxLengthMetres = 260f;

        private static readonly RoadColor PostGrey = RoadColor.Srgb(0.55f, 0.56f, 0.58f, 1f);
        private static readonly RoadColor TapeRed = RoadColor.Srgb(0.80f, 0.08f, 0.08f, 1f);
        private static readonly RoadColor TapeWhite = RoadColor.Srgb(0.94f, 0.94f, 0.92f, 1f);

        private static Dictionary<string, float[]> _corridors;

        /// <summary>The fenced walk for a regional bay: x, z pairs from the terminal wall toward the aircraft.</summary>
        public static bool TryCorridor(StableId stand, out float[] xz)
        {
            xz = null;
            if (string.IsNullOrEmpty(stand.Value))
                return false;
            _corridors ??= BuildCorridors();
            return _corridors.TryGetValue(stand.Value, out xz);
        }

        public static IReadOnlyDictionary<string, float[]> Corridors => _corridors ??= BuildCorridors();

        private static Dictionary<string, float[]> BuildCorridors()
        {
            var map = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (var bay in AdelaideLayout.Bays)
            {
                // The walk starts in front of the bay's terminal door (AdelaideTerminalDoors), not at an arbitrary
                // point of wall, and heads for a point short of the aircraft.
                if (!AdelaideTerminalDoors.TryForBay(bay.Id, out var door))
                    continue;
                var startX = door.ThresholdX;
                var startZ = door.ThresholdZ;
                var dx = bay.StopX - startX;
                var dz = bay.StopZ - startZ;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length < StopShortMetres + 6f || length > MaxLengthMetres + StopShortMetres)
                    continue;
                dx /= length;
                dz /= length;
                map[bay.Id] = new[]
                {
                    startX, startZ,
                    bay.StopX - dx * StopShortMetres, bay.StopZ - dz * StopShortMetres
                };
            }

            return map;
        }

        /// <summary>Posts and tape along both edges of every corridor. Returns the number of corridors drawn.</summary>
        public static int Build(RoadMeshSink sink, RoadBuildOptions o)
        {
            var drawn = 0;
            foreach (var corridor in Corridors.Values)
                if (Fence(sink, corridor[0], corridor[1], corridor[2], corridor[3],
                        (x, z) => o.Height(x, z) + o.YOffset))
                    drawn++;
            return drawn;
        }

        /// <summary>
        /// Posts and tape along both sides of a walk drawn as a polyline of x, y, z triples (world metres, y the ground):
        /// the route passengers actually take, so the tape goes up with them and follows them round the aircraft rather
        /// than standing on the apron all day. Returns the number of segments fenced.
        /// </summary>
        public static int BuildAlong(RoadMeshSink sink, IReadOnlyList<float> xyz)
        {
            var fenced = 0;
            var n = xyz.Count / 3;
            for (var i = 0; i + 1 < n; i++)
            {
                var y0 = xyz[i * 3 + 1];
                var y1 = xyz[i * 3 + 4];
                var ax = xyz[i * 3];
                var az = xyz[i * 3 + 2];
                var bx = xyz[i * 3 + 3];
                var bz = xyz[i * 3 + 5];
                var span = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (Fence(sink, ax, az, bx, bz, (x, z) =>
                    {
                        var t = span < 1e-3f ? 0f : (float)Math.Sqrt((x - ax) * (x - ax) + (z - az) * (z - az)) / span;
                        return y0 + (y1 - y0) * Math.Min(1f, t);
                    }))
                    fenced++;
            }

            return fenced;
        }

        private static bool Fence(RoadMeshSink sink, float ax, float az, float bx, float bz, Func<float, float, float> groundY)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < 1f)
                return false;
            var ux = dx / length;
            var uz = dz / length;
            var nx = -uz;
            var nz = ux;
            foreach (var side in new[] { -1f, 1f })
            {
                var ex = ax + nx * HalfWidthMetres * side;
                var ez = az + nz * HalfWidthMetres * side;
                var posts = (int)Math.Ceiling(length / PostSpacingMetres);
                for (var i = 0; i <= posts; i++)
                {
                    var t = Math.Min(length, i * PostSpacingMetres);
                    var x = ex + ux * t;
                    var z = ez + uz * t;
                    sink.Cylinder(x, groundY(x, z), z, 0.05f, PostHeightMetres, 6, PostGrey);
                }

                var stripes = (int)Math.Ceiling(length / StripeMetres);
                for (var i = 0; i < stripes; i++)
                {
                    var from = i * StripeMetres;
                    var to = Math.Min(length, from + StripeMetres);
                    var mid = (from + to) * 0.5f;
                    var x = ex + ux * mid;
                    var z = ez + uz * mid;
                    sink.Box(x, groundY(x, z) + TapeHeightMetres, z, ux, uz, (to - from) * 0.5f, 0.012f, 0.08f,
                        i % 2 == 0 ? TapeRed : TapeWhite);
                }
            }

            return true;
        }
    }
}
