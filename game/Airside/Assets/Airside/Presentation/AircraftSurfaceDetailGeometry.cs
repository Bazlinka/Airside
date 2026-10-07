using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Clips small painted/metal details to actual side-facing skin triangles, in aircraft metres.</summary>
    public static class AircraftSurfaceDetailGeometry
    {
        public sealed class Patch
        {
            public readonly float[] Positions;
            public readonly int[] Triangles;
            public Patch(float[] positions, int[] triangles)
            {
                Positions = positions;
                Triangles = triangles;
            }
        }

        public sealed class Details
        {
            public readonly List<Patch> Trim, Hardware;
            public Details(List<Patch> trim, List<Patch> hardware) { Trim = trim; Hardware = hardware; }
        }

        public static Details Build(float[] skin, int[] triangles, bool door, bool cargo, bool sliding,
            float centreX, float minY, float maxY, float minZ, float maxZ)
        {
            var height = maxY - minY;
            var width = maxZ - minZ;
            var trim = new List<Patch>();
            var metal = new List<Patch>();
            if (door)
            {
                var side = centreX < 0f ? -1 : 1;
                if (height < 0.25f || width < 0.25f) return new Details(trim, metal);
                // Keep the seam modest at follow zoom. Every rectangle is clipped to
                // the actual curved leaf, including its authored rounded corners.
                Frame(trim, skin, triangles, side, minY + 0.015f, maxY - 0.015f,
                    minZ + 0.015f, maxZ - 0.015f, 0.022f);
                var handleY = minY + height * (sliding ? 0.40f : 0.50f);
                // Match door_set's authored handle station where a handle already
                // exists; adding another latch on the opposite edge reads wrongly.
                var handleZ = minZ + width * (sliding ? 0.30f : 0.775f);
                Rect(trim, skin, triangles, side, handleY - 0.055f, handleY + 0.055f,
                    handleZ - 0.115f, handleZ + 0.115f);
                Rect(metal, skin, triangles, side, handleY - 0.016f, handleY + 0.016f,
                    handleZ - 0.085f, handleZ + 0.085f, 0.012f);
                // A metal threshold, with two extra lower latches for a cargo leaf.
                Rect(metal, skin, triangles, side, minY + height * 0.07f,
                    minY + height * 0.07f + 0.025f,
                    minZ + width * 0.18f, maxZ - width * 0.18f);
                for (var j = 0; j < 2; j++)
                {
                    var y = minY + height * (cargo ? 0.18f : 0.25f + j * 0.50f);
                    var z = cargo ? minZ + width * (0.24f + j * 0.52f) : maxZ - width * 0.09f;
                    Rect(metal, skin, triangles, side, y - 0.03f, y + 0.03f, z - 0.035f, z + 0.035f);
                }
                if (sliding)
                    Rect(metal, skin, triangles, side, maxY - height * 0.09f - 0.025f,
                        maxY - height * 0.09f, minZ + width * 0.10f, maxZ - width * 0.10f);
            }
            else
            {
                // Restrained access hatches below the window/title belt. Use metre
                // dimensions, capped by the hull, rather than scaling up on widebodies.
                var y = ((minY + maxY) * 0.5f) - height * 0.20f;
                var z = maxZ - width * 0.23f;
                var halfWidth = Math.Min(0.23f, width * 0.02f);
                var halfHeight = Math.Min(0.16f, height * 0.07f);
                foreach (var side in new[] { -1, 1 })
                {
                    Frame(trim, skin, triangles, side, y - halfHeight, y + halfHeight,
                        z - halfWidth, z + halfWidth, 0.012f);
                    Rect(metal, skin, triangles, side, y - 0.012f, y + 0.012f, z - 0.06f, z + 0.06f);
                }
            }
            return new Details(trim, metal);
        }

        private static void Frame(List<Patch> target, float[] skin, int[] triangles,
            int side, float y0, float y1, float z0, float z1, float width)
        {
            Rect(target, skin, triangles, side, y0, y0 + width, z0, z1);
            Rect(target, skin, triangles, side, y1 - width, y1, z0, z1);
            Rect(target, skin, triangles, side, y0 + width, y1 - width, z0, z0 + width);
            Rect(target, skin, triangles, side, y0 + width, y1 - width, z1 - width, z1);
        }

        private static void Rect(List<Patch> target, float[] skin, int[] triangles,
            int side, float y0, float y1, float z0, float z1, float offset = 0.006f) =>
            target.Add(Rectangle(skin, triangles, side, y0, y1, z0, z1, offset));

        private readonly struct Point
        {
            public readonly float X, Y, Z;
            public Point(float x, float y, float z) { X = x; Y = y; Z = z; }
            public float Axis(int axis) => axis == 1 ? Y : Z;
            public static Point Lerp(Point a, Point b, float t) =>
                new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>One rectangle projected onto the outward face; no back faces or floating decal boards.</summary>
        public static Patch Rectangle(float[] skin, int[] triangles, int side,
            float minY, float maxY, float minZ, float maxZ, float offset = 0.006f)
        {
            if (skin == null || triangles == null || skin.Length % 3 != 0 || triangles.Length % 3 != 0)
                throw new ArgumentException("Skin needs complete vertices and triangles.");
            if (side != -1 && side != 1)
                throw new ArgumentOutOfRangeException(nameof(side));
            var vertices = new List<float>();
            var indices = new List<int>();
            if (minY >= maxY || minZ >= maxZ)
                return new Patch(vertices.ToArray(), indices.ToArray());
            // Some legacy box panels (notably Bell sliding doors) have inward
            // winding. Their visible outer face is still the one we must decorate.
            var winding = Winding(skin, triangles);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = Read(skin, triangles[i]);
                var b = Read(skin, triangles[i + 1]);
                var c = Read(skin, triangles[i + 2]);
                var nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y);
                if (nx * side * winding <= 0.0000001f || (a.X + b.X + c.X) * side <= 0f)
                    continue;
                if (Math.Max(a.Y, Math.Max(b.Y, c.Y)) < minY || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > maxY
                    || Math.Max(a.Z, Math.Max(b.Z, c.Z)) < minZ || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > maxZ)
                    continue;
                var polygon = new List<Point> { a, b, c };
                polygon = Clip(polygon, 1, minY, true);
                polygon = Clip(polygon, 1, maxY, false);
                polygon = Clip(polygon, 2, minZ, true);
                polygon = Clip(polygon, 2, maxZ, false);
                var start = vertices.Count / 3;
                foreach (var p in polygon)
                {
                    vertices.Add(p.X + side * offset);
                    vertices.Add(p.Y);
                    vertices.Add(p.Z);
                }
                for (var j = 1; j + 1 < polygon.Count; j++)
                {
                    var p = polygon[0]; var q = polygon[j]; var r = polygon[j + 1];
                    if (Math.Abs((q.Y - p.Y) * (r.Z - p.Z) - (q.Z - p.Z) * (r.Y - p.Y)) < 0.00000001f)
                        continue;
                    indices.Add(start);
                    indices.Add(start + (winding > 0 ? j : j + 1));
                    indices.Add(start + (winding > 0 ? j + 1 : j));
                }
            }
            return new Patch(vertices.ToArray(), indices.ToArray());
        }

        private static int Winding(float[] skin, int[] triangles)
        {
            double volume = 0;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = Read(skin, triangles[i]); var b = Read(skin, triangles[i + 1]); var c = Read(skin, triangles[i + 2]);
                volume += a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y)
                    + a.Y * ((double)b.Z * c.X - (double)b.X * c.Z)
                    + a.Z * ((double)b.X * c.Y - (double)b.Y * c.X);
            }
            return volume < -0.0000001 ? -1 : 1;
        }

        private static Point Read(float[] positions, int index)
        {
            if (index < 0 || index >= positions.Length / 3)
                throw new ArgumentException("Triangle index is outside the skin.");
            var i = index * 3;
            return new Point(positions[i], positions[i + 1], positions[i + 2]);
        }

        private static List<Point> Clip(List<Point> source, int axis, float limit, bool above)
        {
            var result = new List<Point>();
            if (source.Count == 0) return result;
            var a = source[source.Count - 1];
            var aInside = above ? a.Axis(axis) >= limit : a.Axis(axis) <= limit;
            foreach (var b in source)
            {
                var bInside = above ? b.Axis(axis) >= limit : b.Axis(axis) <= limit;
                if (aInside != bInside)
                    result.Add(Point.Lerp(a, b, (limit - a.Axis(axis)) / (b.Axis(axis) - a.Axis(axis))));
                if (bInside) result.Add(b);
                a = b; aInside = bInside;
            }
            return result;
        }
    }
}
