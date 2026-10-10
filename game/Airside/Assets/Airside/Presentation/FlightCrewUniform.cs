using System;
using System.Linq;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original uniform styling on shipped CC0 suit characters. No shared source material is edited.</summary>
    public static class FlightCrewUniform
    {
        private static readonly Color Navy = new(.065f, .105f, .18f);
        public static void Apply(GameObject figure, bool pilot)
        {
            foreach (var renderer in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    var name = source.name.Split('-').Last().Replace(" (Instance)", "");
                    if (name.Equals("Suit", StringComparison.OrdinalIgnoreCase))
                        materials[i] = AirsideMaterialLibrary.CreateShared(pilot ? Navy : new Color(.09f,.22f,.31f), AirsideMaterialLibrary.SurfaceKind.Default);
                    else if (name.Equals("Tie", StringComparison.OrdinalIgnoreCase))
                        materials[i] = AirsideMaterialLibrary.CreateShared(pilot ? Navy : new Color(.65f,.25f,.15f), AirsideMaterialLibrary.SurfaceKind.Default);
                    else if (name.EndsWith("White", StringComparison.OrdinalIgnoreCase))
                        materials[i] = AirsideMaterialLibrary.CreateShared(new Color(.93f,.94f,.95f), AirsideMaterialLibrary.SurfaceKind.Default);
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (!pilot) return;
            var crown = figure.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Head_end");
            if (crown == null) return;
            var cap = new GameObject("Pilot cap").transform;
            cap.SetParent(crown, false);
            var scale = Mathf.Max(.001f, Mathf.Abs(crown.lossyScale.x));
            cap.localScale = Vector3.one / scale;
            var up = crown.parent != null ? (crown.position-crown.parent.position).normalized : figure.transform.up;
            var forward = Vector3.ProjectOnPlane(figure.transform.forward, up);
            cap.rotation = Quaternion.LookRotation(forward.sqrMagnitude > .001f ? forward : figure.transform.forward, up);
            Part(cap, "Cap crown", PrimitiveType.Cylinder, new Vector3(0,.015f,0), new Vector3(.215f,.033f,.205f), Navy);
            Part(cap, "Cap visor", PrimitiveType.Cube, new Vector3(0,-.005f,.11f), new Vector3(.19f,.012f,.10f), Navy);
            Part(cap, "Cap badge", PrimitiveType.Cube, new Vector3(0,.01f,.105f), new Vector3(.04f,.025f,.008f), new Color(.87f,.7f,.3f));
        }
        private static void Part(Transform parent, string name, PrimitiveType shape, Vector3 at, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = at; part.transform.localScale = size;
            if (Application.isPlaying) UnityEngine.Object.Destroy(part.GetComponent<Collider>());
            else UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(color, AirsideMaterialLibrary.SurfaceKind.Default);
        }
    }
}
