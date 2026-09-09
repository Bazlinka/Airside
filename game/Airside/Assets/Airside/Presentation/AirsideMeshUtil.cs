using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Mesh queries that do not force a CPU copy of vertex data. Accessing
    /// <see cref="Mesh.uv"/> on a non-readable imported mesh logs
    /// <c>mesh isReadable is false</c> and hitch the packaged player.
    /// </summary>
    public static class AirsideMeshUtil
    {
        public static bool HasUsableUvs(Mesh mesh) =>
            mesh != null && mesh.HasVertexAttribute(VertexAttribute.TexCoord0);

        public static void UploadStatic(Mesh mesh)
        {
            if (mesh == null)
                return;
            mesh.UploadMeshData(true);
        }

        /// <summary>
        /// One mesh of scaled/offset copies of a readable source (greybox shrub clumps).
        /// </summary>
        public static Mesh CombineTransformed(Mesh source, Matrix4x4[] locals)
        {
            if (source == null || locals == null || locals.Length == 0)
                return null;
            var combine = new CombineInstance[locals.Length];
            for (var i = 0; i < locals.Length; i++)
            {
                combine[i].mesh = source;
                combine[i].transform = locals[i];
            }

            var mesh = new Mesh { name = source.name + " combined" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            UploadStatic(mesh);
            return mesh;
        }
    }
}
