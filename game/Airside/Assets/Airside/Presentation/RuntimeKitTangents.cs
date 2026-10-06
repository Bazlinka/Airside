using System;

namespace Airside.Presentation
{
    /// <summary>Tangent-space basis from final mesh geometry, normals and UVs, before cache publication.</summary>
    public static class RuntimeKitTangents
    {
        public static float[] Build(float[] positions, float[] normals, float[] uvs, int[] triangles)
        {
            var count = positions.Length / 3;
            var alongU = new double[count * 3]; var alongV = new double[count * 3];
            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                var ia = triangles[t]; var ib = triangles[t + 1]; var ic = triangles[t + 2];
                var du1 = uvs[ib * 2] - uvs[ia * 2]; var dv1 = uvs[ib * 2 + 1] - uvs[ia * 2 + 1];
                var du2 = uvs[ic * 2] - uvs[ia * 2]; var dv2 = uvs[ic * 2 + 1] - uvs[ia * 2 + 1];
                var determinant = (double)du1 * dv2 - (double)du2 * dv1;
                if (Math.Abs(determinant) < 1e-12) continue;
                for (var a = 0; a < 3; a++)
                {
                    var edge1 = positions[ib * 3 + a] - positions[ia * 3 + a];
                    var edge2 = positions[ic * 3 + a] - positions[ia * 3 + a];
                    var u = (edge1 * dv2 - edge2 * dv1) / determinant;
                    var v = (edge2 * du1 - edge1 * du2) / determinant;
                    alongU[ia * 3 + a] += u; alongV[ia * 3 + a] += v;
                    alongU[ib * 3 + a] += u; alongV[ib * 3 + a] += v;
                    alongU[ic * 3 + a] += u; alongV[ic * 3 + a] += v;
                }
            }
            var result = new float[count * 4];
            for (var vertex = 0; vertex < count; vertex++)
            {
                var i = vertex * 3;
                var nx = (double)normals[i]; var ny = (double)normals[i + 1]; var nz = (double)normals[i + 2];
                var nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl < 1e-12) { nx = 0; ny = 1; nz = 0; nl = 1; }
                nx /= nl; ny /= nl; nz /= nl;
                var dot = nx * alongU[i] + ny * alongU[i + 1] + nz * alongU[i + 2];
                var x = alongU[i] - nx * dot; var y = alongU[i + 1] - ny * dot; var z = alongU[i + 2] - nz * dot;
                var length = Math.Sqrt(x * x + y * y + z * z);
                if (length < 1e-12)
                {
                    // Degenerate UV/geometry islands still receive a finite orthogonal frame.
                    // This does not repair texture mapping; non-aircraft UV seams are fixed first.
                    if (Math.Abs(nx) < 0.9) { x = 1 - nx * nx; y = -nx * ny; z = -nx * nz; }
                    else { x = -ny * nx; y = 1 - ny * ny; z = -ny * nz; }
                    length = Math.Sqrt(x * x + y * y + z * z);
                }
                x /= length; y /= length; z /= length;
                var handedness = (ny * z - nz * y) * alongV[i] + (nz * x - nx * z) * alongV[i + 1]
                    + (nx * y - ny * x) * alongV[i + 2] < 0 ? -1f : 1f;
                result[vertex * 4] = (float)x; result[vertex * 4 + 1] = (float)y;
                result[vertex * 4 + 2] = (float)z; result[vertex * 4 + 3] = handedness;
            }
            return result;
        }
    }
}
