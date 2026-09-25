using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Releases a runtime-generated mesh with the GameObject that owns it.</summary>
    [ExecuteAlways]
    public sealed class AirsideGeneratedMeshOwner : MonoBehaviour
    {
        internal Mesh Mesh { get; set; }

        private void OnDestroy()
        {
            var owned = Mesh;
            Mesh = null;
            if (owned == null)
                return;
            if (Application.isPlaying)
                Destroy(owned);
            else
                DestroyImmediate(owned);
        }
    }
}
