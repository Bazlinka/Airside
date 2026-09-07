using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Applies <see cref="AirsideMaterialLibrary"/> Lit profiles to MeshRenderers on
    /// Resources/Addressables prefabs that ship without authored .mat assets.
    /// Decision 0025 item 1–2 — lets the first prefab drop work before Bailey assigns
    /// Editor materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirsideRuntimeMaterialBinder : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new Color(0.7f, 0.72f, 0.74f, 1f);
        [SerializeField] private Color accentColor = new Color(0.85f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color stepColor = new Color(0.55f, 0.56f, 0.58f, 1f);

        private void Awake() => Apply();

        public void Apply()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name.ToLowerInvariant();
                Color color;
                var kind = AirsideMaterialLibrary.SurfaceKind.PaintedMetal;
                if (n.Contains("rail") || n.Contains("accent"))
                {
                    color = accentColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else if (n.Contains("step") || n.Contains("tread"))
                {
                    color = stepColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else
                {
                    color = baseColor;
                    kind = AirsideMaterialLibrary.InferFromMeshName(n);
                }

                renderer.material = AirsideMaterialLibrary.Create(color, kind);
            }
        }
    }
}
