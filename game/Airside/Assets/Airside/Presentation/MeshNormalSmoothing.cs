using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Smooth-shades a runtime-built mesh by weld angle, the way Unity's own asset-import
    /// pipeline does (<c>ModelImporter.normalSmoothingAngle</c>) but for meshes built from
    /// script — <c>Mesh.RecalculateNormals()</c> has no runtime equivalent of that overload.
    /// Most of this project's procedural geometry generators (<c>oval_lathe_fuselage</c>,
    /// <c>cylinder</c>, <c>nacelle_pod</c>, <c>annulus</c>, wing/control-surface slabs, fan
    /// blades — see the Python scripts under <c>scripts/</c>) emit four fresh, unshared
    /// vertices per quad face, so a plain <c>RecalculateNormals()</c> (which only averages a
    /// vertex's normal across faces sharing its exact vertex *index*) renders every
    /// continuously curved surface as a hard facet per quad, no matter how many segments it
    /// has. This welds vertices that share a position and only smooths the shading across
    /// faces whose angle to each other is within the threshold, so a fuselage tube reads as
    /// round while a genuine hard edge (a wingtip against the fuselage, a panel break between
    /// separate meshes) stays crisp.
    /// </summary>
    public static class MeshNormalSmoothing
    {
        /// <summary>
        /// Matches the 60° smoothing angle this codebase's own authored-FBX pipeline already
        /// uses (<c>normalSmoothAngle: 60</c> in
        /// <c>scripts/generate-authored-fbx-turboprop-terminal.py</c>'s <c>.fbx.meta</c>
        /// stub), so runtime-loaded and import-time models shade consistently.
        /// </summary>
        public const float DefaultAngleDegrees = 60f;

        /// <summary>Recalculates <paramref name="mesh"/>'s normals with position-weld +
        /// angle-threshold smoothing, in place of a bare <c>RecalculateNormals()</c> call.</summary>
        public static void RecalculateSmoothNormals(
            Mesh mesh, Vector3[] vertices, int[] triangles, float angleDegrees = DefaultAngleDegrees)
        {
            mesh.SetNormals(Compute(vertices, triangles, angleDegrees));
        }

        /// <summary>The pure per-vertex normal computation, exposed separately from
        /// <see cref="RecalculateSmoothNormals"/> so it can be unit tested without a live
        /// <see cref="Mesh"/>.</summary>
        public static Vector3[] Compute(Vector3[] vertices, int[] triangles, float angleDegrees)
        {
            var vertexCount = vertices.Length;
            var direct = new Vector3[vertexCount];

            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                var i0 = triangles[t];
                var i1 = triangles[t + 1];
                var i2 = triangles[t + 2];
                var faceNormal = Vector3.Cross(
                    vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]).normalized;
                direct[i0] += faceNormal;
                direct[i1] += faceNormal;
                direct[i2] += faceNormal;
            }
            for (var i = 0; i < vertexCount; i++)
                direct[i] = direct[i].sqrMagnitude > 0f ? direct[i].normalized : Vector3.up;

            // Weld vertices that share a position (within a small epsilon of a metre — plenty
            // for real-world-scale models, and generous enough to catch the near-exact
            // floating-point coincidence the generators actually produce at unwelded quad
            // boundaries) into groups, so their otherwise-independent per-index normals can be
            // blended.
            var groups = new Dictionary<(int, int, int), List<int>>();
            for (var i = 0; i < vertexCount; i++)
            {
                var key = PositionKey(vertices[i]);
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<int>(4);
                list.Add(i);
            }

            var cosThreshold = Mathf.Cos(angleDegrees * Mathf.Deg2Rad);
            var result = new Vector3[vertexCount];
            foreach (var group in groups.Values)
            {
                if (group.Count == 1)
                {
                    result[group[0]] = direct[group[0]];
                    continue;
                }
                foreach (var i in group)
                {
                    var sum = Vector3.zero;
                    foreach (var j in group)
                        if (Vector3.Dot(direct[i], direct[j]) >= cosThreshold)
                            sum += direct[j];
                    result[i] = sum.sqrMagnitude > 0f ? sum.normalized : direct[i];
                }
            }

            return result;
        }

        private static (int, int, int) PositionKey(Vector3 position) =>
            (Mathf.RoundToInt(position.x * 1000f),
             Mathf.RoundToInt(position.y * 1000f),
             Mathf.RoundToInt(position.z * 1000f));
    }
}
