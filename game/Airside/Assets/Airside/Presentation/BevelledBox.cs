using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0124 — a unit cube (−0.5…0.5) with chamfered edges and corners, replacing Unity's
    /// sharp primitive for every procedural block. The chamfer is given per axis in local units
    /// so that, once the block is scaled, every edge has the same world-metre bevel. Flat
    /// chamfer faces catch light along each edge, which is what stops boxes reading as boxes.
    /// Pure (no UnityEngine) so the geometry is tested headlessly.
    /// </summary>
    public static class BevelledBox
    {
        /// <summary>Largest local chamfer on any axis; beyond this the faces would vanish.</summary>
        public const float MaxLocalBevel = 0.25f;
        /// <summary>World chamfer: this fraction of the smallest side, capped at <see cref="MaxWorldBevel"/>.</summary>
        public const float WorldBevelFraction = 0.15f;
        public const float MaxWorldBevel = 0.12f;
        /// <summary>Thinner blocks (paint, decals, lenses) keep the plain cube: a bevel there is invisible.</summary>
        public const float MinBevelledSide = 0.05f;

        public sealed class Geometry
        {
            public readonly List<float> Positions = new();
            public readonly List<float> Normals = new();
            public readonly List<float> Uvs = new();
            public readonly List<int> Triangles = new();
            public int VertexCount => Positions.Count / 3;
        }

        /// <summary>The local per-axis chamfer for a block of this world size, or null for a plain cube.</summary>
        public static (float x, float y, float z)? LocalBevelFor(float sizeX, float sizeY, float sizeZ)
        {
            sizeX = Math.Abs(sizeX);
            sizeY = Math.Abs(sizeY);
            sizeZ = Math.Abs(sizeZ);
            var smallest = Math.Min(sizeX, Math.Min(sizeY, sizeZ));
            if (smallest < MinBevelledSide)
                return null;
            var world = Math.Min(MaxWorldBevel, smallest * WorldBevelFraction);
            return (Local(world, sizeX), Local(world, sizeY), Local(world, sizeZ));
        }

        /// <summary>Rounds a local chamfer to a thousandth so blocks of similar size share one mesh.</summary>
        public static int Quantise(float local) => (int)Math.Round(local * 1000f);

        private static float Local(float world, float size) => Math.Min(MaxLocalBevel, world / size);

        public static Geometry Build(float bx, float by, float bz)
        {
            var b = new[] { Clamp(bx), Clamp(by), Clamp(bz) };
            var g = new Geometry();

            // Six inset faces.
            for (var axis = 0; axis < 3; axis++)
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var u = (axis + 1) % 3;
                var v = (axis + 2) % 3;
                var normal = Axis(axis, sign);
                var corners = new float[4][];
                var k = 0;
                foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var p = new float[3];
                    p[axis] = 0.5f * sign;
                    p[u] = su * (0.5f - b[u]);
                    p[v] = sv * (0.5f - b[v]);
                    corners[k++] = p;
                }

                Quad(g, corners, normal, axis);
            }

            // Twelve edge chamfers.
            for (var a1 = 0; a1 < 3; a1++)
            for (var a2 = a1 + 1; a2 < 3; a2++)
            for (var s1 = -1; s1 <= 1; s1 += 2)
            for (var s2 = -1; s2 <= 1; s2 += 2)
            {
                var w = 3 - a1 - a2;
                var normal = new float[3];
                normal[a1] = s1 * b[a2];
                normal[a2] = s2 * b[a1];
                Normalise(normal);
                var corners = new float[4][];
                var k = 0;
                foreach (var (onFirst, sw) in new[] { (true, -1), (true, 1), (false, 1), (false, -1) })
                {
                    var p = new float[3];
                    p[a1] = onFirst ? 0.5f * s1 : s1 * (0.5f - b[a1]);
                    p[a2] = onFirst ? s2 * (0.5f - b[a2]) : 0.5f * s2;
                    p[w] = sw * (0.5f - b[w]);
                    corners[k++] = p;
                }

                Quad(g, corners, normal, w == 1 ? 0 : 1);
            }

            // Eight corner triangles.
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var normal = new[] { sx * b[1] * b[2], sy * b[0] * b[2], sz * b[0] * b[1] };
                Normalise(normal);
                var pa = new[] { 0.5f * sx, sy * (0.5f - b[1]), sz * (0.5f - b[2]) };
                var pb = new[] { sx * (0.5f - b[0]), 0.5f * sy, sz * (0.5f - b[2]) };
                var pc = new[] { sx * (0.5f - b[0]), sy * (0.5f - b[1]), 0.5f * sz };
                var i0 = Vertex(g, pa, normal, 1);
                var i1 = Vertex(g, pb, normal, 1);
                var i2 = Vertex(g, pc, normal, 1);
                Triangle(g, i0, i1, i2, normal);
            }

            return g;
        }

        /// <summary>
        /// A unit tube along z (−0.5…0.5) whose cross-section is a rectangle with rounded
        /// corners (radius <paramref name="rx"/>, <paramref name="ry"/> in local units per
        /// axis), with flat end caps. For parts that stretch along z at runtime — aerobridge
        /// tunnels — because the rounding lives only in the cross-section.
        /// </summary>
        public static Geometry RoundedTube(float rx, float ry, int segmentsPerCorner = 4)
        {
            rx = Math.Max(0.001f, Math.Min(0.5f, rx));
            ry = Math.Max(0.001f, Math.Min(0.5f, ry));
            var profile = new List<(float x, float y, float nx, float ny)>();
            var corners = new[] { (1, 1, 0.0), (-1, 1, 90.0), (-1, -1, 180.0), (1, -1, 270.0) };
            foreach (var (sx, sy, start) in corners)
            {
                var cx = sx * (0.5f - rx);
                var cy = sy * (0.5f - ry);
                for (var k = 0; k <= segmentsPerCorner; k++)
                {
                    var angle = (start + 90.0 * k / segmentsPerCorner) * Math.PI / 180.0;
                    var cos = (float)Math.Cos(angle);
                    var sin = (float)Math.Sin(angle);
                    var nx = cos / rx;
                    var ny = sin / ry;
                    var length = (float)Math.Sqrt(nx * nx + ny * ny);
                    profile.Add((cx + rx * cos, cy + ry * sin, nx / length, ny / length));
                }
            }

            var g = new Geometry();
            var count = profile.Count;
            var perimeter = 0f;
            var along = new float[count + 1];
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                along[i + 1] = along[i] + (float)Math.Sqrt(Math.Pow(profile[j].x - profile[i].x, 2) + Math.Pow(profile[j].y - profile[i].y, 2));
            }

            perimeter = along[count];
            // Side wall: a seam column is repeated so u runs 0…1 once round.
            var back = new int[count + 1];
            var front = new int[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var p = profile[i % count];
                var normal = new[] { p.nx, p.ny, 0f };
                back[i] = Vertex(g, new[] { p.x, p.y, -0.5f }, normal, along[i] / perimeter, 0f);
                front[i] = Vertex(g, new[] { p.x, p.y, 0.5f }, normal, along[i] / perimeter, 1f);
            }

            for (var i = 0; i < count; i++)
            {
                var mid = profile[i];
                var next = profile[(i + 1) % count];
                var outward = new[] { mid.nx + next.nx, mid.ny + next.ny, 0f };
                Triangle(g, back[i], back[i + 1], front[i + 1], outward);
                Triangle(g, back[i], front[i + 1], front[i], outward);
            }

            // End caps.
            for (var end = -1; end <= 1; end += 2)
            {
                var normal = new[] { 0f, 0f, (float)end };
                var centre = Vertex(g, new[] { 0f, 0f, 0.5f * end }, normal, 0.5f, 0.5f);
                var ring = new int[count];
                for (var i = 0; i < count; i++)
                    ring[i] = Vertex(g, new[] { profile[i].x, profile[i].y, 0.5f * end }, normal, profile[i].x + 0.5f, profile[i].y + 0.5f);
                for (var i = 0; i < count; i++)
                    Triangle(g, centre, ring[i], ring[(i + 1) % count], normal);
            }

            return g;
        }

        private static int Vertex(Geometry g, float[] p, float[] normal, float u, float v)
        {
            g.Positions.Add(p[0]);
            g.Positions.Add(p[1]);
            g.Positions.Add(p[2]);
            g.Normals.Add(normal[0]);
            g.Normals.Add(normal[1]);
            g.Normals.Add(normal[2]);
            g.Uvs.Add(u);
            g.Uvs.Add(v);
            return g.VertexCount - 1;
        }

        private static float Clamp(float local) => Math.Max(0.001f, Math.Min(MaxLocalBevel, local));

        private static float[] Axis(int axis, int sign)
        {
            var n = new float[3];
            n[axis] = sign;
            return n;
        }

        private static void Normalise(float[] n)
        {
            var length = (float)Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
            for (var i = 0; i < 3; i++)
                n[i] /= length;
        }

        private static void Quad(Geometry g, float[][] corners, float[] normal, int axis)
        {
            var i0 = Vertex(g, corners[0], normal, axis);
            var i1 = Vertex(g, corners[1], normal, axis);
            var i2 = Vertex(g, corners[2], normal, axis);
            var i3 = Vertex(g, corners[3], normal, axis);
            Triangle(g, i0, i1, i2, normal);
            Triangle(g, i0, i2, i3, normal);
        }

        /// <summary>UVs project each face onto its own plane, 0…1 across the full cube, like Unity's primitive.</summary>
        private static int Vertex(Geometry g, float[] p, float[] normal, int axis)
        {
            g.Positions.Add(p[0]);
            g.Positions.Add(p[1]);
            g.Positions.Add(p[2]);
            g.Normals.Add(normal[0]);
            g.Normals.Add(normal[1]);
            g.Normals.Add(normal[2]);
            var u = (axis + 1) % 3;
            var v = (axis + 2) % 3;
            g.Uvs.Add(p[u] + 0.5f);
            g.Uvs.Add(p[v] + 0.5f);
            return g.VertexCount - 1;
        }

        /// <summary>Adds a triangle wound as a front face seen from outside.</summary>
        private static void Triangle(Geometry g, int a, int b, int c, float[] normal)
        {
            float P(int i, int k) => g.Positions[i * 3 + k];
            var e1 = new[] { P(b, 0) - P(a, 0), P(b, 1) - P(a, 1), P(b, 2) - P(a, 2) };
            var e2 = new[] { P(c, 0) - P(a, 0), P(c, 1) - P(a, 1), P(c, 2) - P(a, 2) };
            var cross = new[]
            {
                e1[1] * e2[2] - e1[2] * e2[1],
                e1[2] * e2[0] - e1[0] * e2[2],
                e1[0] * e2[1] - e1[1] * e2[0]
            };
            // Same convention as Unity's own quad (triangle 0,3,1 of its -z face): the plain
            // cross product of a front face's edges points along the outward normal.
            var dot = cross[0] * normal[0] + cross[1] * normal[1] + cross[2] * normal[2];
            if (dot < 0f)
                (b, c) = (c, b);
            g.Triangles.Add(a);
            g.Triangles.Add(b);
            g.Triangles.Add(c);
        }
    }
}
