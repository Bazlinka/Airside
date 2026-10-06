using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Normal-directed box-kit UV seams. Source vertex order is remapped, never mutated.</summary>
    public static class BoxKitSurfaceGeometry
    {
        public sealed class Unwrap
        {
            public int[] SourceIndices { get; internal set; }
            public int[] Triangles { get; internal set; }
            public float[] Uvs { get; internal set; }
        }

        public static Unwrap Build(float[] positions, int[] triangles)
        {
            var low = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            var high = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (var i = 0; i < positions.Length; i++)
            { low[i % 3] = Math.Min(low[i % 3], positions[i]); high[i % 3] = Math.Max(high[i % 3], positions[i]); }
            var sources = new List<int>();
            var uv = new List<float>();
            var indices = new int[triangles.Length];
            var seams = new Dictionary<(int Vertex, int Axis, bool Negative), int>();
            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                var a = triangles[t] * 3; var b = triangles[t + 1] * 3; var c = triangles[t + 2] * 3;
                var x1 = positions[b] - positions[a]; var y1 = positions[b + 1] - positions[a + 1]; var z1 = positions[b + 2] - positions[a + 2];
                var x2 = positions[c] - positions[a]; var y2 = positions[c + 1] - positions[a + 1]; var z2 = positions[c + 2] - positions[a + 2];
                var normal = new[] { y1 * z2 - z1 * y2, z1 * x2 - x1 * z2, x1 * y2 - y1 * x2 };
                var axis = Math.Abs(normal[0]) >= Math.Abs(normal[1]) && Math.Abs(normal[0]) >= Math.Abs(normal[2]) ? 0
                    : Math.Abs(normal[1]) >= Math.Abs(normal[2]) ? 1 : 2;
                var uAxis = axis == 0 ? 2 : 0;
                var vAxis = axis == 1 ? 2 : 1;
                var negative = normal[axis] < 0f;
                for (var corner = 0; corner < 3; corner++)
                {
                    var source = triangles[t + corner];
                    var key = (source, axis, negative);
                    if (!seams.TryGetValue(key, out var index))
                    {
                        index = sources.Count;
                        seams.Add(key, index); sources.Add(source);
                        var u = (positions[source * 3 + uAxis] - low[uAxis]) / Math.Max(0.0001f, high[uAxis] - low[uAxis]);
                        uv.Add(negative ? 1f - u : u);
                        uv.Add((positions[source * 3 + vAxis] - low[vAxis]) / Math.Max(0.0001f, high[vAxis] - low[vAxis]));
                    }
                    indices[t + corner] = index;
                }
            }
            return new Unwrap { SourceIndices = sources.ToArray(), Triangles = indices, Uvs = uv.ToArray() };
        }
    }
}
