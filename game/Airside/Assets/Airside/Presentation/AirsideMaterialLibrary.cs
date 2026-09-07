using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 4 — coherent URP Lit material profiles for the greybox /
    /// kit presentation. Profiles set metallic/smoothness and attach authored
    /// Batch B normal / AO / metallic-smoothness masks when present (StreamingAssets),
    /// falling back to shared procedural maps so surfaces never read as flat unlit
    /// plastic. Full Addressables materials remain the longer-term production path.
    /// </summary>
    public static class AirsideMaterialLibrary
    {
        public enum SurfaceKind
        {
            Default,
            Asphalt,
            Concrete,
            Grass,
            Sand,
            Metal,
            PaintedMetal,
            AircraftSkin,
            Glass,
            Rubber,
            Plastic,
            Water,
            UnlitSky
        }

        public readonly struct Profile
        {
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly float BumpScale;
            public readonly float Occlusion;
            public readonly bool Transparent;

            public Profile(float metallic, float smoothness, float bumpScale, float occlusion = 1f, bool transparent = false)
            {
                Metallic = metallic;
                Smoothness = smoothness;
                BumpScale = bumpScale;
                Occlusion = occlusion;
                Transparent = transparent;
            }
        }

        private static readonly Dictionary<SurfaceKind, Profile> Profiles = new()
        {
            // Dry profiles tuned so wet variants can raise gloss without starting shiny.
            [SurfaceKind.Default] = new Profile(0.04f, 0.28f, 0.38f),
            [SurfaceKind.Asphalt] = new Profile(0.015f, 0.16f, 0.62f, 0.9f),
            [SurfaceKind.Concrete] = new Profile(0.025f, 0.22f, 0.5f, 0.93f),
            [SurfaceKind.Grass] = new Profile(0.0f, 0.14f, 0.75f, 0.86f),
            [SurfaceKind.Sand] = new Profile(0.0f, 0.16f, 0.55f, 0.88f),
            [SurfaceKind.Metal] = new Profile(0.58f, 0.44f, 0.38f, 0.95f),
            [SurfaceKind.PaintedMetal] = new Profile(0.22f, 0.46f, 0.28f, 0.96f),
            [SurfaceKind.AircraftSkin] = new Profile(0.16f, 0.58f, 0.18f, 0.97f),
            [SurfaceKind.Glass] = new Profile(0.04f, 0.88f, 0.04f, 1f, transparent: true),
            [SurfaceKind.Rubber] = new Profile(0.02f, 0.12f, 0.65f, 0.88f),
            [SurfaceKind.Plastic] = new Profile(0.05f, 0.38f, 0.28f, 0.96f),
            [SurfaceKind.Water] = new Profile(0.02f, 0.9f, 0.22f, 1f, transparent: true),
            [SurfaceKind.UnlitSky] = new Profile(0f, 0f, 0f, 1f)
        };

        /// <summary>Authored Batch B map stems under Textures/Surfaces/.</summary>
        private static readonly Dictionary<SurfaceKind, string> AuthoredStemByKind = new()
        {
            [SurfaceKind.Asphalt] = "tx_asphalt_runway",
            [SurfaceKind.Concrete] = "tx_concrete_apron",
            [SurfaceKind.Grass] = "tx_grass_kingscote",
            [SurfaceKind.Sand] = "tx_sand_coast",
            [SurfaceKind.Water] = "tx_water_coast",
            [SurfaceKind.AircraftSkin] = "tx_aircraft_skin",
            [SurfaceKind.Metal] = "tx_corrugated_metal",
            [SurfaceKind.PaintedMetal] = "tx_corrugated_metal"
        };

        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredNormals = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredAo = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredMasks = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredAlbedo = new();
        private static bool _authoredResolved;

        private static Texture2D _sharedNormal;
        private static Texture2D _sharedOcclusion;
        private static Shader _litShader;

        public static Profile GetProfile(SurfaceKind kind) =>
            Profiles.TryGetValue(kind, out var profile) ? profile : Profiles[SurfaceKind.Default];

        public static SurfaceKind InferFromTexturePath(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return SurfaceKind.Default;
            var p = artRelativePath.ToLowerInvariant();
            if (p.Contains("asphalt") || p.Contains("runway"))
                return SurfaceKind.Asphalt;
            if (p.Contains("concrete") || p.Contains("apron"))
                return SurfaceKind.Concrete;
            if (p.Contains("grass"))
                return SurfaceKind.Grass;
            if (p.Contains("corrugated") || p.Contains("metal"))
                return SurfaceKind.Metal;
            if (p.Contains("glass"))
                return SurfaceKind.Glass;
            if (p.Contains("sand"))
                return SurfaceKind.Sand;
            if (p.Contains("water"))
                return SurfaceKind.Water;
            if (p.Contains("aircraft_skin") || p.Contains("livery"))
                return SurfaceKind.AircraftSkin;
            return SurfaceKind.Default;
        }

        public static SurfaceKind InferFromMeshName(string meshName)
        {
            if (string.IsNullOrEmpty(meshName))
                return SurfaceKind.Default;
            var n = meshName.ToLowerInvariant();
            if (n.Contains("glass") || n.Contains("window") || n.Contains("mullion")
                || n.Equals("cockpit") || n.Contains("cabin_windows") || n.Contains("cabin window"))
                return SurfaceKind.Glass;
            if (n.Contains("tire") || n.Contains("wheel") || n.Contains("rubber"))
                return SurfaceKind.Rubber;
            if (n.Contains("propeller") || n.Contains("propblade") || n.Contains("spinner")
                || n.Contains("gear") || n.Contains("nacelle") || n.Contains("engine")
                || n.Contains("tank") || n.Contains("hose") || n.Contains("column")
                || n.Contains("canopy_post") || n.Contains("crane") || n.Contains("antenna"))
                return SurfaceKind.Metal;
            if (n.Contains("fuselage") || n.Contains("nose") || n.Contains("wing") || n.Contains("tail")
                || n.Contains("rudder") || n.Contains("elevator") || n.Contains("flap")
                || n.Contains("aileron") || n.Contains("cabindoor") || n.Contains("cabin door")
                || n.Contains("cargo door") || n.Contains("body") || n.Contains("cab")
                || n.Contains("bus_") || n.Equals("tug") || n.Contains("tug_")
                || n.Contains("livery") || n.Contains("stripe") || n.Contains("fairing"))
                return SurfaceKind.AircraftSkin;
            if (n.Contains("roof") || n.Contains("corrugat") || n.Contains("hangar") || n.Contains("shed")
                || n.Contains("buttress") || n.Contains("vent") || n.Contains("track")
                || n.Contains("door_panel") || n.Contains("door_opening") || n.Contains("door_rib")
                || n.Contains("door_track") || n.Contains("service_wing") || n.Contains("signage"))
                return SurfaceKind.Metal;
            if (n.Contains("terminal") || n.Contains("concrete") || n.Contains("apron")
                || n.Contains("canopy") || n.Contains("end_cap") || n.Contains("entrance"))
                return SurfaceKind.Concrete;
            return SurfaceKind.PaintedMetal;
        }

        public static Material Create(
            Color color,
            SurfaceKind kind = SurfaceKind.Default,
            Texture2D albedo = null,
            Vector2? tiling = null)
        {
            var profile = GetProfile(kind);
            EnsureSharedMaps();
            EnsureAuthoredMaps();
            var shader = _litShader ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", profile.Metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", profile.Smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", profile.Smoothness);
            if (material.HasProperty("_SpecularHighlights"))
                material.SetFloat("_SpecularHighlights", 1f);
            if (material.HasProperty("_EnvironmentReflections"))
                material.SetFloat("_EnvironmentReflections", 1f);

            if (albedo != null)
            {
                material.mainTexture = albedo;
                material.mainTextureScale = tiling ?? Vector2.one;
            }
            else if (AuthoredAlbedo.TryGetValue(kind, out var authoredAlbedo) && authoredAlbedo != null)
            {
                material.mainTexture = authoredAlbedo;
                material.mainTextureScale = tiling ?? Vector2.one;
            }

            var normal = ResolveNormal(kind);
            if (kind != SurfaceKind.UnlitSky && normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", profile.BumpScale);
                if (tiling.HasValue)
                    material.SetTextureScale("_BumpMap", tiling.Value);
            }

            var ao = ResolveAo(kind);
            if (kind != SurfaceKind.UnlitSky && ao != null && material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", ao);
                if (material.HasProperty("_OcclusionStrength"))
                    material.SetFloat("_OcclusionStrength", 1f - profile.Occlusion + 0.15f);
                if (tiling.HasValue)
                    material.SetTextureScale("_OcclusionMap", tiling.Value * 0.5f);
            }

            if (AuthoredMasks.TryGetValue(kind, out var mask) && mask != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (tiling.HasValue)
                    material.SetTextureScale("_MetallicGlossMap", tiling.Value);
            }

            if (profile.Transparent || color.a < 0.99f)
                ApplyTransparent(material);

            return material;
        }

        public static float DrySmoothness(SurfaceKind kind) => GetProfile(kind).Smoothness;

        public static float DryMetallic(SurfaceKind kind) => GetProfile(kind).Metallic;

        public static float DryBumpScale(SurfaceKind kind) => GetProfile(kind).BumpScale;

        /// <summary>
        /// Wet-variant response for paved / ground surfaces (0025 item 4). Darkens
        /// albedo, raises smoothness, flattens micro-bump, and enables a clear-coat
        /// sheen so rain reads on URP Lit without authoring separate wet mats.
        /// </summary>
        public static void ApplyWetness(
            Material material,
            float wetness01,
            Color dryColor,
            float drySmoothness,
            float dryMetallic = 0.02f,
            float dryBumpScale = 0.5f)
        {
            if (material == null)
                return;
            wetness01 = Mathf.Clamp01(wetness01);
            // Cool puddle tint + darken — asphalt goes nearly black; grass stays greenish.
            var wetTint = new Color(0.03f, 0.06f, 0.1f, 0f);
            var wetColor = Color.Lerp(dryColor, dryColor * 0.38f + wetTint, wetness01);
            wetColor.a = dryColor.a;
            material.color = wetColor;

            var targetSmooth = Mathf.Max(drySmoothness, 0.92f);
            var smoothness = Mathf.Lerp(drySmoothness, targetSmooth, wetness01 * wetness01);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            var metallic = Mathf.Lerp(dryMetallic, Mathf.Max(dryMetallic, 0.22f), wetness01 * 0.9f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);

            // Wet surfaces lose micro-relief — bump flattens toward a mirror sheen.
            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", Mathf.Lerp(dryBumpScale, dryBumpScale * 0.22f, wetness01));

            // Slight AO deepen so wet pavement reads puddled rather than just glossy.
            if (material.HasProperty("_OcclusionStrength"))
                material.SetFloat("_OcclusionStrength", Mathf.Lerp(1f, 1.25f, wetness01));

            if (material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoatMask", wetness01);
                if (material.HasProperty("_ClearCoatSmoothness"))
                    material.SetFloat("_ClearCoatSmoothness", Mathf.Lerp(0.12f, 0.98f, wetness01));
                if (wetness01 > 0.02f)
                    material.EnableKeyword("_CLEARCOAT");
                else
                    material.DisableKeyword("_CLEARCOAT");
            }
        }

        private static Texture2D ResolveNormal(SurfaceKind kind) =>
            AuthoredNormals.TryGetValue(kind, out var tex) && tex != null ? tex : _sharedNormal;

        private static Texture2D ResolveAo(SurfaceKind kind) =>
            AuthoredAo.TryGetValue(kind, out var tex) && tex != null ? tex : _sharedOcclusion;

        private static void ApplyTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static void EnsureSharedMaps()
        {
            if (_sharedNormal != null && _sharedOcclusion != null)
                return;

            const int size = 64;
            _sharedNormal = BuildNormalMap(size, seed: 17);
            _sharedOcclusion = BuildOcclusionMap(size, seed: 41);
        }

        private static void EnsureAuthoredMaps()
        {
            if (_authoredResolved)
                return;
            _authoredResolved = true;

            foreach (var pair in AuthoredStemByKind)
            {
                var kind = pair.Key;
                var stem = pair.Value;
                var normal = TryLoadArtTexture($"Textures/Surfaces/{stem}_normal_v01.png", linear: true);
                var ao = TryLoadArtTexture($"Textures/Surfaces/{stem}_ao_v01.png", linear: true);
                var mask = TryLoadArtTexture($"Textures/Surfaces/{stem}_mask_v01.png", linear: true);
                var albedo = TryLoadArtTexture($"Textures/Surfaces/{stem}_basecolor_v01.png", linear: false);
                if (normal != null)
                    AuthoredNormals[kind] = normal;
                if (ao != null)
                    AuthoredAo[kind] = ao;
                if (mask != null)
                    AuthoredMasks[kind] = mask;
                if (albedo != null)
                    AuthoredAlbedo[kind] = albedo;
            }
        }

        private static Texture2D TryLoadArtTexture(string artRelativePath, bool linear)
        {
            var fullPath = ArtRuntimePaths.ResolveExisting(artRelativePath);
            if (fullPath == null)
                return null;

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: linear);
                if (!texture.LoadImage(bytes))
                    return null;
                texture.name = Path.GetFileNameWithoutExtension(artRelativePath);
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tiny procedural normal map — enough micro-relief that Lit lighting catches edges.
        /// </summary>
        private static Texture2D BuildNormalMap(int size, int seed)
        {
            var height = new float[size * size];
            var rng = new System.Random(seed);
            for (var i = 0; i < height.Length; i++)
                height[i] = (float)rng.NextDouble();

            // Soft blur so normals are not pure noise sparkle.
            var blurred = new float[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float sum = 0f;
                for (var oy = -1; oy <= 1; oy++)
                for (var ox = -1; ox <= 1; ox++)
                {
                    var sx = (x + ox + size) % size;
                    var sy = (y + oy + size) % size;
                    sum += height[sy * size + sx];
                }

                blurred[y * size + x] = sum / 9f;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: true)
            {
                name = "airside_proc_normal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var hL = blurred[y * size + ((x - 1 + size) % size)];
                var hR = blurred[y * size + ((x + 1) % size)];
                var hD = blurred[((y - 1 + size) % size) * size + x];
                var hU = blurred[((y + 1) % size) * size + x];
                var dx = (hL - hR) * 2f;
                var dy = (hD - hU) * 2f;
                var normal = new Vector3(dx, dy, 1f).normalized;
                // Unity tangent-space normal encoding.
                pixels[y * size + x] = new Color32(
                    (byte)((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)((normal.z * 0.5f + 0.5f) * 255f),
                    255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }

        private static Texture2D BuildOcclusionMap(int size, int seed)
        {
            var rng = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: true)
            {
                name = "airside_proc_ao",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var v = (byte)(180 + rng.Next(0, 75));
                pixels[i] = new Color32(v, v, v, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }
    }
}
