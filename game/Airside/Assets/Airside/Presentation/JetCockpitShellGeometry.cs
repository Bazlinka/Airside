using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Continuous opaque flight-deck skin. The only boundary is the deliberate window belt.
    /// Coordinates are relative to the fitted pilot-eye datum; independent of Unity.</summary>
    public static class JetCockpitShellGeometry
    {
        public readonly struct Point
        {
            public readonly float X, Y, Z;
            public Point(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
        public sealed class Geometry
        {
            public readonly List<Point> Vertices = new();
            public readonly List<int> Triangles = new();
            public readonly HashSet<int> WindowBoundary = new();
        }

        public static Geometry Build(float halfWidth)
        {
            if (halfWidth <= 0f || float.IsNaN(halfWidth) || float.IsInfinity(halfWidth))
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            var g = new Geometry();
            // Weld identical coordinates before triangulation, allowing topology checks to find
            // an unintended seam instead of hiding it with overlapping primitive boxes.
            var ids = new Dictionary<(float, float, float), int>();
            int Vertex(float x, float y, float z)
            {
                var key = (x, y, z);
                if (ids.TryGetValue(key, out var existing)) return existing;
                var id = g.Vertices.Count; ids.Add(key, id);
                g.Vertices.Add(new Point(x, y, z)); return id;
            }
            void Face(params int[] ring)
            {
                float x = 0, y = 0, z = 0;
                foreach (var id in ring) { var p = g.Vertices[id]; x += p.X; y += p.Y; z += p.Z; }
                var centre = Vertex(x / ring.Length, y / ring.Length, z / ring.Length);
                var c = g.Vertices[centre]; var a = g.Vertices[ring[0]]; var b = g.Vertices[ring[1]];
                var ax = a.X - c.X; var ay = a.Y - c.Y; var az = a.Z - c.Z;
                var bx = b.X - c.X; var by = b.Y - c.Y; var bz = b.Z - c.Z;
                var nx = ay * bz - az * by; var ny = az * bx - ax * bz; var nz = ax * by - ay * bx;
                // This convex shell surrounds an interior point at (0, -0.4, -0.2).
                var reverse = nx * c.X + ny * (c.Y + 0.4f) + nz * (c.Z + 0.2f) < 0f;
                for (var i = 0; i < ring.Length; i++)
                {
                    g.Triangles.Add(centre);
                    g.Triangles.Add(ring[reverse ? (i + 1) % ring.Length : i]);
                    g.Triangles.Add(ring[reverse ? i : (i + 1) % ring.Length]);
                }
            }
            var floor = new List<int>(); var roof = new List<int>();
            var bottom = new int[2, 4]; var top = new int[2, 4]; var sill = new int[2, 4];
            var zs = new[] { -1.73f, -0.61f, 0.72f, 1.30f };
            for (var side = 0; side < 2; side++)
            {
                var sign = side == 0 ? -1f : 1f;
                for (var station = 0; station < 4; station++)
                {
                    var front = station == 3;
                    bottom[side, station] = Vertex(sign * halfWidth * (front ? 0.84f : 1f), -1.40f, zs[station]);
                    sill[side, station] = Vertex(sign * halfWidth * (front ? 0.84f : 1f), -0.05f, zs[station]);
                    top[side, station] = Vertex(sign * halfWidth * (front ? 0.73f : 1f), front ? 0.51f : 0.67f, front ? 1.05f : zs[station]);
                    if (station > 0) { g.WindowBoundary.Add(sill[side, station]); g.WindowBoundary.Add(top[side, station]); }
                }
                for (var station = 0; station < 3; station++)
                    Face(bottom[side, station], bottom[side, station + 1], sill[side, station + 1], sill[side, station]);
                // Solid rear sidewall, split at sill height to avoid a T-junction with the lower wall.
                Face(sill[side, 0], sill[side, 1], top[side, 1], top[side, 0]);
            }
            for (var station = 0; station < 4; station++) floor.Add(bottom[0, station]);
            for (var station = 3; station >= 0; station--) floor.Add(bottom[1, station]);
            // Flat main ceiling keeps the overhead panel below the lining. Only the
            // forward windscreen wedge slopes: a fan across a non-planar perimeter
            // pulled the centre of the old roof down through the overhead controls.
            for (var station = 0; station < 3; station++) roof.Add(top[0, station]);
            for (var station = 2; station >= 0; station--) roof.Add(top[1, station]);
            Face(floor.ToArray()); Face(roof.ToArray());
            Face(top[0, 2], top[0, 3], top[1, 3], top[1, 2]);
            Face(bottom[0, 0], sill[0, 0], sill[1, 0], bottom[1, 0]);
            Face(sill[0, 0], top[0, 0], top[1, 0], sill[1, 0]);
            Face(bottom[0, 3], bottom[1, 3], sill[1, 3], sill[0, 3]);
            return g;
        }
    }
}
