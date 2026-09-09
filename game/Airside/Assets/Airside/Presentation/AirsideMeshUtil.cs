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
    }
}
