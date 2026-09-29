using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0184 — Adelaide Airport's car parks as flat geometry from <see cref="AdelaideCarParks"/>: dark surfaces
    /// (ear-clipped outlines), white bay lines, parked cars (a body and a cabin, occupancy and colour by hash so
    /// every run is the same picture) and street lamps. Pure (no UnityEngine) so the headless harness checks it.
    /// </summary>
    public static class AdelaideCarParkGeometry
    {
        /// <summary>Cars are only placed this close to the Terminal 1 forecourt, keeping the triangle count in budget.</summary>
        public const float CarRadiusMetres = 2300f;
        public const float CentreX = 1300f;
        public const float CentreZ = 500f;
        public const float Occupancy = 0.62f;
        public const int MaxCars = 4200;
        public const float SurfaceLift = 0.05f;
        public const float LampHeight = 7.5f;

        private static readonly RoadColor Surface = RoadColor.Srgb(0.29f, 0.30f, 0.31f, 0.5f);
        private static readonly RoadColor BayPaint = RoadColor.Srgb(0.86f, 0.86f, 0.82f, 1f);
        private static readonly RoadColor Steel = RoadColor.Srgb(0.42f, 0.43f, 0.45f, 1f);
        private static readonly RoadColor LampHead = RoadColor.Srgb(0.30f, 0.31f, 0.33f, 1f);
        private static readonly RoadColor Glass = RoadColor.Srgb(0.12f, 0.15f, 0.18f, 1f);

        // Australian carpark palette: lots of white, silver, grey and black.
        private static readonly RoadColor[] CarColours =
        {
            RoadColor.Srgb(0.92f, 0.92f, 0.90f, 1f), RoadColor.Srgb(0.92f, 0.92f, 0.90f, 1f),
            RoadColor.Srgb(0.92f, 0.92f, 0.90f, 1f), RoadColor.Srgb(0.68f, 0.70f, 0.72f, 1f),
            RoadColor.Srgb(0.68f, 0.70f, 0.72f, 1f), RoadColor.Srgb(0.40f, 0.42f, 0.45f, 1f),
            RoadColor.Srgb(0.14f, 0.15f, 0.17f, 1f), RoadColor.Srgb(0.14f, 0.15f, 0.17f, 1f),
            RoadColor.Srgb(0.62f, 0.14f, 0.12f, 1f), RoadColor.Srgb(0.16f, 0.30f, 0.52f, 1f),
            RoadColor.Srgb(0.55f, 0.58f, 0.40f, 1f), RoadColor.Srgb(0.80f, 0.62f, 0.20f, 1f)
        };

        // --- surfaces ---

        /// <summary>Ear-clips every car-park outline into the sink. Returns outlines drawn.</summary>
        public static int BuildSurfaces(RoadMeshSink sink, RoadBuildOptions o)
        {
            var drawn = 0;
            for (var p = 0; p < AdelaideCarParks.PolygonCount; p++)
            {
                var start = AdelaideCarParks.PolygonStarts[p];
                var end = AdelaideCarParks.PolygonStarts[p + 1];
                var xz = new List<float>((end - start) * 2);
                for (var i = start; i < end; i++)
                {
                    xz.Add(AdelaideCarParks.Points[i * 2]);
                    xz.Add(AdelaideCarParks.Points[i * 2 + 1]);
                }

                foreach (var t in Triangulate(xz))
                {
                    float Y(int k) => o.Height(xz[t[k] * 2], xz[t[k] * 2 + 1]) + o.YOffset * 0.5f + SurfaceLift * 0.2f;
                    sink.Tri(xz[t[0] * 2], Y(0), xz[t[0] * 2 + 1], xz[t[1] * 2], Y(1), xz[t[1] * 2 + 1],
                        xz[t[2] * 2], Y(2), xz[t[2] * 2 + 1], Surface);
                }

                drawn++;
            }

            return drawn;
        }

        /// <summary>Ear clipping of a simple polygon (either winding). Returns index triples into the x,z list.</summary>
        public static List<int[]> Triangulate(List<float> xz)
        {
            var n = xz.Count / 2;
            var result = new List<int[]>();
            if (n < 3)
                return result;
            var index = new List<int>(n);
            for (var i = 0; i < n; i++)
                index.Add(i);
            var sign = SignedArea(xz) >= 0f ? 1f : -1f;

            var guard = n * n + 8;
            var at = 0;
            while (index.Count > 3 && guard-- > 0)
            {
                var m = index.Count;
                var ia = index[(at + m - 1) % m];
                var ib = index[at % m];
                var ic = index[(at + 1) % m];
                if (IsEar(xz, index, ia, ib, ic, sign))
                {
                    result.Add(new[] { ia, ib, ic });
                    index.RemoveAt(at % m);
                    at = Math.Max(0, (at % m) - 1);
                }
                else
                {
                    at = (at + 1) % m;
                }
            }

            if (index.Count == 3)
                result.Add(new[] { index[0], index[1], index[2] });
            return result;
        }

        public static float SignedArea(List<float> xz)
        {
            var n = xz.Count / 2;
            var a = 0f;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                a += xz[i * 2] * xz[j * 2 + 1] - xz[j * 2] * xz[i * 2 + 1];
            }

            return a * 0.5f;
        }

        private static bool IsEar(List<float> xz, List<int> index, int a, int b, int c, float sign)
        {
            var cross = ((xz[b * 2] - xz[a * 2]) * (xz[c * 2 + 1] - xz[a * 2 + 1])
                         - (xz[b * 2 + 1] - xz[a * 2 + 1]) * (xz[c * 2] - xz[a * 2])) * sign;
            if (cross <= 1e-4f)
                return false;
            foreach (var v in index)
            {
                if (v == a || v == b || v == c)
                    continue;
                if (InTriangle(xz[v * 2], xz[v * 2 + 1], xz[a * 2], xz[a * 2 + 1], xz[b * 2], xz[b * 2 + 1],
                        xz[c * 2], xz[c * 2 + 1]))
                    return false;
            }

            return true;
        }

        private static bool InTriangle(float px, float pz, float ax, float az, float bx, float bz, float cx, float cz)
        {
            var d1 = (px - bx) * (az - bz) - (ax - bx) * (pz - bz);
            var d2 = (px - cx) * (bz - cz) - (bx - cx) * (pz - cz);
            var d3 = (px - ax) * (cz - az) - (cx - ax) * (pz - az);
            var neg = d1 < 0f || d2 < 0f || d3 < 0f;
            var pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        // --- bay lines ---

        /// <summary>White lines between the bays of every painted run. Returns lines drawn.</summary>
        public static int BuildBayLines(RoadMeshSink sink, RoadBuildOptions o)
        {
            var lines = 0;
            var r = AdelaideCarParks.Runs;
            var s = AdelaideCarParks.RunStride;
            for (var k = 0; k + s <= r.Length; k += s)
            {
                if (r[k + 6] < 0.5f || Far(r[k], r[k + 1], CarRadiusMetres * 1.3f))
                    continue;
                var dx = r[k + 2];
                var dz = r[k + 3];
                var ax = -dz;
                var az = dx;
                var n = (int)r[k + 4];
                for (var j = 0; j <= n; j++)
                {
                    // a line at each bay boundary, the depth of a bay
                    var bx = r[k] + dx * AdelaideCarParks.BayPitchMetres * (j - 0.5f);
                    var bz = r[k + 1] + dz * AdelaideCarParks.BayPitchMetres * (j - 0.5f);
                    Line(sink, o, bx - ax * 2.5f, bz - az * 2.5f, bx + ax * 2.5f, bz + az * 2.5f, 0.055f);
                    lines++;
                }
            }

            return lines;
        }

        private static void Line(RoadMeshSink sink, RoadBuildOptions o, float fx, float fz, float tx, float tz, float halfWidth)
        {
            var dx = tx - fx;
            var dz = tz - fz;
            var len = (float)Math.Sqrt(dx * dx + dz * dz);
            dx /= len;
            dz /= len;
            var px = -dz * halfWidth;
            var pz = dx * halfWidth;
            var lift = o.YOffset + AdelaideRoadGeometry.PaintLift;
            float H(float x, float z) => o.Height(x, z) + lift;
            sink.Quad(fx + px, H(fx + px, fz + pz), fz + pz, fx - px, H(fx - px, fz - pz), fz - pz,
                tx + px, H(tx + px, tz + pz), tz + pz, tx - px, H(tx - px, tz - pz), tz - pz, BayPaint);
        }

        // --- cars ---

        /// <summary>Parked cars in the bays. Returns cars placed.</summary>
        public static int BuildCars(RoadMeshSink sink, RoadBuildOptions o)
        {
            var cars = 0;
            var r = AdelaideCarParks.Runs;
            var s = AdelaideCarParks.RunStride;
            for (var k = 0; k + s <= r.Length && cars < MaxCars; k += s)
            {
                if (Far(r[k], r[k + 1], CarRadiusMetres))
                    continue;
                var dx = r[k + 2];
                var dz = r[k + 3];
                var n = (int)r[k + 4];
                var single = r[k + 6] < 0.5f;
                for (var j = 0; j < n && cars < MaxCars; j++)
                {
                    var h = Hash((uint)k, (uint)j);
                    if ((h & 0xFFFF) / 65535f > Occupancy)
                        continue;
                    var x = r[k] + dx * AdelaideCarParks.BayPitchMetres * j;
                    var z = r[k + 1] + dz * AdelaideCarParks.BayPitchMetres * j;
                    // A run's cars stand across it; a mapped single space's own axis is its direction.
                    float ux, uz;
                    if (single)
                    {
                        var angle = r[k + 5] * (float)Math.PI / 180f;
                        ux = (float)Math.Cos(angle);
                        uz = (float)Math.Sin(angle);
                    }
                    else
                    {
                        ux = -dz;
                        uz = dx;
                    }

                    // a few degrees of parking imperfection, and some cars nose-in, some reversed in
                    var jitter = (((h >> 16) & 0xFF) / 255f - 0.5f) * 0.08f;
                    var cj = (float)Math.Cos(jitter);
                    var sj = (float)Math.Sin(jitter);
                    var rx = ux * cj - uz * sj;
                    var rz = ux * sj + uz * cj;
                    if (((h >> 24) & 1) == 1)
                    {
                        rx = -rx;
                        rz = -rz;
                    }

                    var length = 4.1f + ((h >> 8) & 0xF) / 15f * 0.9f;
                    var tall = ((h >> 12) & 0x7) == 0;
                    Car(sink, o, x + ((h >> 5) & 0x7) / 7f * 0.2f - 0.1f, z, rx, rz, length, tall,
                        CarColours[(int)((h >> 20) % (uint)CarColours.Length)]);
                    cars++;
                }
            }

            return cars;
        }

        private static void Car(RoadMeshSink sink, RoadBuildOptions o, float x, float z, float ux, float uz, float length,
            bool tall, RoadColor colour)
        {
            var y = o.Height(x, z) + o.YOffset;
            var bodyHeight = tall ? 1.05f : 0.75f;
            sink.Box(x, y + 0.22f, z, ux, uz, length * 0.5f, 0.9f, bodyHeight, colour);
            // cabin, set back from the middle; dark glass band under a body-coloured roof reads as windows
            var back = length * -0.05f;
            var cx = x + ux * back;
            var cz = z + uz * back;
            var cabinLength = length * (tall ? 0.72f : 0.5f);
            var cabinBase = y + 0.22f + bodyHeight;
            sink.Box(cx, cabinBase, cz, ux, uz, cabinLength * 0.5f, 0.8f, 0.22f, Glass);
            sink.Box(cx, cabinBase + 0.22f, cz, ux, uz, cabinLength * 0.5f - 0.05f, 0.76f, 0.1f, colour);
        }

        // --- lamps ---

        /// <summary>A post and lamp head at every street lamp. Returns lamps drawn.</summary>
        public static int BuildLamps(RoadMeshSink sink, RoadBuildOptions o)
        {
            var l = AdelaideCarParks.Lamps;
            for (var i = 0; i + 1 < l.Length; i += 2)
            {
                var x = l[i];
                var z = l[i + 1];
                var y = o.Height(x, z) + o.YOffset;
                sink.Box(x, y, z, 1f, 0f, 0.09f, 0.09f, LampHeight, Steel);
                sink.Box(x + 0.5f, y + LampHeight, z, 1f, 0f, 0.7f, 0.16f, 0.16f, LampHead);
            }

            return l.Length / 2;
        }

        private static bool Far(float x, float z, float radius)
        {
            var dx = x - CentreX;
            var dz = z - CentreZ;
            return dx * dx + dz * dz > radius * radius;
        }

        private static uint Hash(uint a, uint b)
        {
            unchecked
            {
                var h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA6Bu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
