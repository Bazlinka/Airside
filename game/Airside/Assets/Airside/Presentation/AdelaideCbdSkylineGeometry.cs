using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure mesh arrays for the CBD silhouette boxes (ADR 0208). No UnityEngine —
    /// headless tests lock vertex/triangle counts and base heights on DEM relief.
    /// </summary>
    public static class AdelaideCbdSkylineGeometry
    {
        public sealed class Result
        {
            public float[] Positions; // x, y, z
            public float[] Colours;   // linear r, g, b, a
            public int[] Triangles;
            public int VertexCount => Positions.Length / 3;
            public int TriangleCount => Triangles.Length / 3;
        }

        /// <summary>Grey tower silhouette in linear colour (matches far Built / commercial).</summary>
        public static readonly float[] TowerLinear = { 0.42f, 0.41f, 0.40f, 1f };

        public static Result Build(float plainY, Func<float, float, float> heightAboveSea)
        {
            var n = AdelaideCbdSkyline.Count;
            var positions = new float[n * 8 * 3];
            var colours = new float[n * 8 * 4];
            var triangles = new int[n * 12 * 3];
            var vi = 0;
            var ti = 0;
            for (var i = 0; i < n; i++)
            {
                var tower = AdelaideCbdSkyline.Get(i);
                var dem = heightAboveSea != null
                    ? heightAboveSea(tower.CentreX, tower.CentreZ)
                    : AdelaideTerrainHeights.PlainAboveSeaMetres;
                var baseY = plainY + AdelaideTerrainHeights.ReliefAbovePlain(dem);
                var topY = baseY + tower.HeightMetres;
                var x0 = tower.CentreX - tower.HalfExtentX;
                var x1 = tower.CentreX + tower.HalfExtentX;
                var z0 = tower.CentreZ - tower.HalfExtentZ;
                var z1 = tower.CentreZ + tower.HalfExtentZ;
                var baseIndex = vi / 3;
                // Bottom then top, CCW when viewed from +Y for the top face.
                WriteVert(positions, colours, ref vi, x0, baseY, z0);
                WriteVert(positions, colours, ref vi, x1, baseY, z0);
                WriteVert(positions, colours, ref vi, x1, baseY, z1);
                WriteVert(positions, colours, ref vi, x0, baseY, z1);
                WriteVert(positions, colours, ref vi, x0, topY, z0);
                WriteVert(positions, colours, ref vi, x1, topY, z0);
                WriteVert(positions, colours, ref vi, x1, topY, z1);
                WriteVert(positions, colours, ref vi, x0, topY, z1);

                // 12 triangles: bottom, top, four sides (two tris each).
                AddQuad(triangles, ref ti, baseIndex + 0, baseIndex + 3, baseIndex + 2, baseIndex + 1); // bottom (-Y)
                AddQuad(triangles, ref ti, baseIndex + 4, baseIndex + 5, baseIndex + 6, baseIndex + 7); // top (+Y)
                AddQuad(triangles, ref ti, baseIndex + 0, baseIndex + 1, baseIndex + 5, baseIndex + 4); // -Z
                AddQuad(triangles, ref ti, baseIndex + 1, baseIndex + 2, baseIndex + 6, baseIndex + 5); // +X
                AddQuad(triangles, ref ti, baseIndex + 2, baseIndex + 3, baseIndex + 7, baseIndex + 6); // +Z
                AddQuad(triangles, ref ti, baseIndex + 3, baseIndex + 0, baseIndex + 4, baseIndex + 7); // -X
            }

            return new Result { Positions = positions, Colours = colours, Triangles = triangles };
        }

        public static float BaseY(float plainY, float heightAboveSea) =>
            plainY + AdelaideTerrainHeights.ReliefAbovePlain(heightAboveSea);

        private static void WriteVert(float[] positions, float[] colours, ref int vi,
            float x, float y, float z)
        {
            var pi = vi;
            positions[pi] = x;
            positions[pi + 1] = y;
            positions[pi + 2] = z;
            var ci = (vi / 3) * 4;
            colours[ci] = TowerLinear[0];
            colours[ci + 1] = TowerLinear[1];
            colours[ci + 2] = TowerLinear[2];
            colours[ci + 3] = TowerLinear[3];
            vi += 3;
        }

        private static void AddQuad(int[] triangles, ref int ti, int a, int b, int c, int d)
        {
            triangles[ti++] = a;
            triangles[ti++] = b;
            triangles[ti++] = c;
            triangles[ti++] = a;
            triangles[ti++] = c;
            triangles[ti++] = d;
        }
    }
}
