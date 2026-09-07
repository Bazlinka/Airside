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
                if (n.Contains("rail") || n.Contains("accent") || n.Contains("beacon")
                    || n.Contains("collar") || n.Contains("stripe") || n.Contains("face")
                    || n.Contains("cargo") || n.Contains("hinge") || n.Contains("band")
                    || n.Contains("pipe") || n.Contains("drawer") || n.Contains("drum"))
                {
                    color = accentColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else if (n.Contains("tire") || n.Contains("tyre") || n.Contains("rubber"))
                {
                    // Must run before the step/wheel metal branch — "wheel" names used to
                    // steal Rubber and leave tyres as painted metal.
                    color = stepColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Rubber;
                }
                else if (n.Contains("step") || n.Contains("tread") || n.Contains("wheel")
                         || n.Contains("leg") || n.Contains("base") || n.Contains("post")
                         || n.Contains("plate") || n.Contains("pad") || n.Contains("bund")
                         || n.Contains("pump") || n.Contains("gear") || n.Contains("prop")
                         || n.Contains("frame") || n.Contains("board"))
                {
                    color = stepColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else if (n.Contains("window") || n.Contains("glass") || n.Contains("canopy")
                         || n.Equals("cockpit") || n.Contains("cabin window"))
                {
                    color = new Color(0.18f, 0.35f, 0.48f, 0.42f);
                    kind = AirsideMaterialLibrary.SurfaceKind.Glass;
                }
                else if (n.Contains("headlight") || n.Contains("taillight"))
                {
                    color = n.Contains("tail")
                        ? new Color(0.85f, 0.15f, 0.12f)
                        : new Color(0.95f, 0.95f, 0.85f);
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else if (n.Contains("mullion") || n.Contains("transom") || n.Contains("strut")
                         || n.Contains("spinner") || n.Contains("hub") || n.Contains("scissor")
                         || n.Contains("antenna") || n.Contains("pitot") || n.Contains("exhaust")
                         || n.Contains("fairing") || n.Contains("grille") || n.Contains("mirror"))
                {
                    color = stepColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.Metal;
                }
                else if (n.Contains("fuselage") || n.Contains("nose") || n.Contains("cowling")
                         || (n.Contains("cabin") && !n.Contains("window")))
                {
                    color = baseColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.AircraftSkin;
                }
                else if (n.Contains("wing") || n.Contains("tail") || n.Contains("rudder")
                         || n.Contains("elevator") || n.Contains("flap") || n.Contains("aileron")
                         || n.Contains("cabindoor") || n.Contains("cabin door") || n.Contains("door"))
                {
                    color = accentColor;
                    kind = AirsideMaterialLibrary.SurfaceKind.AircraftSkin;
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
