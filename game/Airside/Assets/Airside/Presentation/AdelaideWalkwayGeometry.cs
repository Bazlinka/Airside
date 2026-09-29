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
    /// walk follows it (<see cref="TryCorridor"/>) and the tape is drawn along both edges. Pure (no UnityEngine).
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
            float[] terminalXz = null;
            foreach (var outline in AdelaideLayout.Terminals)
                if (outline.Name == "Domestic & International Terminal")
                    terminalXz = outline.Xz;
            if (terminalXz == null)
                return map;

            foreach (var bay in AdelaideLayout.Bays)
            {
                NearestOnOutline(terminalXz, bay.StopX, bay.StopZ, out var wallX, out var wallZ);
                var dx = bay.StopX - wallX;
                var dz = bay.StopZ - wallZ;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length < StopShortMetres + 6f || length > MaxLengthMetres + StopShortMetres)
                    continue;
                dx /= length;
                dz /= length;
                map[bay.Id] = new[]
                {
                    wallX + dx * 1.2f, wallZ + dz * 1.2f,
                    bay.StopX - dx * StopShortMetres, bay.StopZ - dz * StopShortMetres
                };
            }

            return map;
        }

        private static void NearestOnOutline(float[] xz, float x, float z, out float qx, out float qz)
        {
            qx = xz[0];
            qz = xz[1];
            var best = float.MaxValue;
            var n = xz.Length / 2;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var ax = xz[i * 2];
                var az = xz[i * 2 + 1];
                var bx = xz[j * 2] - ax;
                var bz = xz[j * 2 + 1] - az;
                var l2 = bx * bx + bz * bz;
                var t = l2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * bx + (z - az) * bz) / l2));
                var px = ax + bx * t;
                var pz = az + bz * t;
                var d = (x - px) * (x - px) + (z - pz) * (z - pz);
                if (d >= best)
                    continue;
                best = d;
                qx = px;
                qz = pz;
            }
        }

        /// <summary>Posts and tape along both edges of every corridor. Returns the number of corridors drawn.</summary>
        public static int Build(RoadMeshSink sink, RoadBuildOptions o)
        {
            var drawn = 0;
            foreach (var corridor in Corridors.Values)
            {
                var ax = corridor[0];
                var az = corridor[1];
                var dx = corridor[2] - ax;
                var dz = corridor[3] - az;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length < 1f)
                    continue;
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
                        sink.Cylinder(x, o.Height(x, z) + o.YOffset, z, 0.05f, PostHeightMetres, 6, PostGrey);
                    }

                    var stripes = (int)Math.Ceiling(length / StripeMetres);
                    for (var i = 0; i < stripes; i++)
                    {
                        var from = i * StripeMetres;
                        var to = Math.Min(length, from + StripeMetres);
                        var mid = (from + to) * 0.5f;
                        var x = ex + ux * mid;
                        var z = ez + uz * mid;
                        sink.Box(x, o.Height(x, z) + o.YOffset + TapeHeightMetres, z, ux, uz, (to - from) * 0.5f, 0.012f, 0.08f,
                            i % 2 == 0 ? TapeRed : TapeWhite);
                    }
                }

                drawn++;
            }

            return drawn;
        }
    }
}
