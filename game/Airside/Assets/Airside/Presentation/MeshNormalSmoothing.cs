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
            var faceCount = triangles.Length / 3;
            var faceNormals = new Vector3[faceCount];
            // Corner angle at each of a face's three vertices: weighting by it (rather than by
            // face count) stops a fan of thin triangles at a vertex from out-voting the one wide
            // face beside them, which is what made smoothly curved panels read lumpy.
            var cornerAngles = new float[faceCount * 3];
            for (var f = 0; f < faceCount; f++)
            {
                var p0 = vertices[triangles[f * 3]];
                var p1 = vertices[triangles[f * 3 + 1]];
                var p2 = vertices[triangles[f * 3 + 2]];
                faceNormals[f] = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                cornerAngles[f * 3] = Vector3.Angle(p1 - p0, p2 - p0);
                cornerAngles[f * 3 + 1] = Vector3.Angle(p0 - p1, p2 - p1);
                cornerAngles[f * 3 + 2] = Vector3.Angle(p0 - p2, p1 - p2);
            }

            // Weld corners that share a position (within a millimetre: the generators emit
            // near-exact coincidence at unwelded quad boundaries) so each corner can blend with
            // every neighbouring face, whichever vertex index that face happens to use.
            var groups = new Dictionary<(int, int, int), List<int>>();
            for (var c = 0; c < faceCount * 3; c++)
            {
                var key = PositionKey(vertices[triangles[c]]);
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<int>(6);
                list.Add(c);
            }

            // Each corner averages the faces in its group that lie within the threshold of its own
            // face; a vertex index shared by several corners averages their results.
            var cosThreshold = Mathf.Cos(angleDegrees * Mathf.Deg2Rad);
            var sums = new Vector3[vertexCount];
            foreach (var group in groups.Values)
            {
                foreach (var c in group)
                {
                    var own = faceNormals[c / 3];
                    var sum = Vector3.zero;
                    foreach (var d in group)
                    {
                        var other = faceNormals[d / 3];
                        if (Vector3.Dot(own, other) >= cosThreshold)
                            sum += other * cornerAngles[d];
                    }
                    sums[triangles[c]] += sum.sqrMagnitude > 0f ? sum.normalized : own;
                }
            }

            var result = new Vector3[vertexCount];
            for (var i = 0; i < vertexCount; i++)
                result[i] = sums[i].sqrMagnitude > 0f ? sums[i].normalized : Vector3.up;
            return result;
        }

        private static (int, int, int) PositionKey(Vector3 position) =>
            (Mathf.RoundToInt(position.x * 1000f),
             Mathf.RoundToInt(position.y * 1000f),
             Mathf.RoundToInt(position.z * 1000f));
    }
}
