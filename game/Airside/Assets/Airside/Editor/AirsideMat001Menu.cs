using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Editor
{
    /// <summary>
    /// Batch F1 MAT-001 — create inspectable URP Lit materials under
    /// Assets/Airside/Art/Materials (contract path) and a Resources mirror for
    /// runtime <see cref="Airside.Presentation.AirsideMaterialLibrary"/> loads.
    /// Runtime Create() remains the fallback when a material asset is missing.
    /// </summary>
    public static class AirsideMat001Menu
    {
        private const string MenuPath = "Airside/Art/Create MAT-001 Materials";
        private const string MaterialsDir = "Assets/Airside/Art/Materials";
        private const string ResourcesDir = "Assets/Resources/Airside/Materials";
        private const string SurfacesDir = "Assets/Airside/Art/Textures/Surfaces";

        private static readonly (string file, string stem, Color baseColor, float metallic, float smooth, float bump, bool transparent)[] Specs =
        {
            ("mat_asphalt_v01.mat", "tx_asphalt_runway", new Color(0.22f, 0.23f, 0.24f), 0.01f, 0.10f, 0.55f, false),
            ("mat_concrete_v01.mat", "tx_concrete_apron", new Color(0.62f, 0.64f, 0.66f), 0.015f, 0.18f, 0.45f, false),
            ("mat_grass_v01.mat", "tx_grass_kingscote", new Color(0.28f, 0.38f, 0.22f), 0f, 0.08f, 0.55f, false),
            ("mat_corrugated_metal_v01.mat", "tx_corrugated_metal", new Color(0.55f, 0.58f, 0.6f), 0.55f, 0.45f, 0.28f, false),
            ("mat_glass_v01.mat", "tx_glass_pane", new Color(0.35f, 0.55f, 0.7f, 0.35f), 0.04f, 0.88f, 0.02f, true),
            ("mat_painted_line_v01.mat", "tx_painted_line", new Color(0.92f, 0.93f, 0.9f), 0.02f, 0.22f, 0.08f, false),
            ("mat_aircraft_v01.mat", "tx_aircraft_skin", new Color(0.9f, 0.92f, 0.94f), 0.16f, 0.62f, 0.12f, false),
            ("mat_wet_v01.mat", "tx_asphalt_runway", new Color(0.14f, 0.15f, 0.16f), 0.02f, 0.72f, 0.4f, false),
        };

        [MenuItem(MenuPath)]
        public static void CreateMaterials()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Airside/Art/Materials"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Airside/Materials"));

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Airside] MAT-001: URP Lit shader not found.");
                return;
            }

            var created = 0;
            foreach (var spec in Specs)
            {
                WriteMaterial($"{MaterialsDir}/{spec.file}", shader, spec);
                WriteMaterial($"{ResourcesDir}/{spec.file}", shader, spec);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Airside] MAT-001: wrote/updated {created} materials ×2 (Art + Resources)");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Airside MAT-001",
                    $"Created/updated {created} URP Lit materials under {MaterialsDir} (+ Resources mirror).",
                    "OK");
            }
        }

        private static void WriteMaterial(
            string path,
            Shader shader,
            (string file, string stem, Color baseColor, float metallic, float smooth, float bump, bool transparent) spec)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(spec.file) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", spec.baseColor);
            mat.SetFloat("_Metallic", spec.metallic);
            mat.SetFloat("_Smoothness", spec.smooth);
            mat.SetFloat("_BumpScale", spec.bump);
            if (spec.transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)RenderQueue.Geometry;
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            AssignMap(mat, "_BaseMap", $"{SurfacesDir}/{spec.stem}_basecolor_v01.png");
            AssignMap(mat, "_BumpMap", $"{SurfacesDir}/{spec.stem}_normal_v01.png");
            AssignMap(mat, "_OcclusionMap", $"{SurfacesDir}/{spec.stem}_ao_v01.png");
            AssignMap(mat, "_MetallicGlossMap", $"{SurfacesDir}/{spec.stem}_mask_v01.png");
            if (mat.GetTexture("_BumpMap") != null)
                mat.EnableKeyword("_NORMALMAP");
            if (mat.GetTexture("_MetallicGlossMap") != null)
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (mat.GetTexture("_OcclusionMap") != null)
                mat.EnableKeyword("_OCCLUSIONMAP");

            EditorUtility.SetDirty(mat);
        }

        private static void AssignMap(Material mat, string prop, string assetPath)
        {
            if (!mat.HasProperty(prop))
                return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null)
                mat.SetTexture(prop, tex);
        }
    }
}
