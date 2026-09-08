using System;
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
            PaintedLine,
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
            [SurfaceKind.Default] = new Profile(0.04f, 0.26f, 0.4f),
            [SurfaceKind.Asphalt] = new Profile(0.01f, 0.10f, 0.78f, 0.9f),
            [SurfaceKind.Concrete] = new Profile(0.015f, 0.18f, 0.72f, 0.94f),
            [SurfaceKind.Grass] = new Profile(0.0f, 0.08f, 0.88f, 0.86f),
            [SurfaceKind.Sand] = new Profile(0.0f, 0.16f, 0.62f, 0.84f),
            [SurfaceKind.Metal] = new Profile(0.68f, 0.52f, 0.32f, 0.96f),
            [SurfaceKind.PaintedMetal] = new Profile(0.22f, 0.55f, 0.24f, 0.97f),
            [SurfaceKind.AircraftSkin] = new Profile(0.16f, 0.68f, 0.14f, 0.98f),
            // Flat painted markings — matte, not aircraft-skin gloss.
            [SurfaceKind.PaintedLine] = new Profile(0.02f, 0.22f, 0.08f, 0.96f),
            // Slightly softer glass so curtain walls read as panes, not chrome mirrors.
            [SurfaceKind.Glass] = new Profile(0.04f, 0.88f, 0.02f, 1f, transparent: true),
            [SurfaceKind.Rubber] = new Profile(0.012f, 0.08f, 0.75f, 0.84f),
            [SurfaceKind.Plastic] = new Profile(0.04f, 0.38f, 0.28f, 0.95f),
            [SurfaceKind.Water] = new Profile(0.025f, 0.94f, 0.18f, 1f, transparent: true),
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
            [SurfaceKind.PaintedMetal] = "tx_corrugated_metal",
            // MAT-001 — dedicated glass / rubber / painted-line / plastic companions.
            [SurfaceKind.Glass] = "tx_glass_pane",
            [SurfaceKind.Rubber] = "tx_rubber_tire",
            [SurfaceKind.PaintedLine] = "tx_painted_line",
            [SurfaceKind.Plastic] = "tx_plastic_trim"
        };

        /// <summary>Default UV tiling when callers omit an explicit scale (MAT-001).</summary>
        private static readonly Dictionary<SurfaceKind, Vector2> DefaultTilingByKind = new()
        {
            [SurfaceKind.Asphalt] = new Vector2(6f, 6f),
            [SurfaceKind.Concrete] = new Vector2(4f, 4f),
            [SurfaceKind.Grass] = new Vector2(8f, 8f),
            [SurfaceKind.Sand] = new Vector2(5f, 5f),
            [SurfaceKind.Metal] = new Vector2(2.5f, 1.5f),
            [SurfaceKind.PaintedMetal] = new Vector2(2f, 1.2f),
            [SurfaceKind.AircraftSkin] = new Vector2(1.5f, 1.5f),
            [SurfaceKind.PaintedLine] = new Vector2(3f, 1f),
            [SurfaceKind.Glass] = new Vector2(1.2f, 1.2f),
            [SurfaceKind.Rubber] = new Vector2(2.5f, 2.5f),
            [SurfaceKind.Plastic] = new Vector2(2f, 2f),
            [SurfaceKind.Water] = new Vector2(3f, 3f)
        };

        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredNormals = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredAo = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredMasks = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> AuthoredAlbedo = new();
        private static bool _authoredResolved;

        private static Texture2D _sharedNormal;
        private static Texture2D _sharedOcclusion;
        private static readonly Dictionary<SurfaceKind, Texture2D> KindNormals = new();
        private static readonly Dictionary<SurfaceKind, Texture2D> KindOcclusion = new();
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
            // Frame / pillar members must beat the glass rule below: they carry "window" or
            // "windscreen" in their name but are painted metal mullions, not glazing.
            if (n.Contains("mullion") || n.Contains("transom") || n.Contains("sill") || n.Contains("header")
                || n.Contains("entrance_frame") || n.Contains("boarding_frame") || n.Contains("handle")
                || n.Contains("skylight_frame") || n.Equals("entrance") || n.Contains("entrance_door")
                || n.Contains("boarding_gate")
                || n.Contains("window_frame") || n.Contains("window frame")
                || n.Contains("windscreen_pillar") || n.Contains("windscreen pillar")
                || n.Contains("cockpit_frame") || n.Contains("cockpit frame"))
                return SurfaceKind.Metal;
            if (n.Contains("glass") || n.Contains("window") || n.Contains("glass_pane")
                || n.Equals("cockpit") || n.Contains("cabin_windows") || n.Contains("cabin window")
                || n.Contains("landside_glass") || n.Contains("door_glass")
                || n.Contains("windshield") || n.Contains("windscreen")
                || n.Equals("rear_window") || n.Contains("skylight"))
                return SurfaceKind.Glass;
            if (n.Contains("tire") || n.Contains("wheel") || n.Contains("rubber"))
                return SurfaceKind.Rubber;
            if (n.Contains("propeller") || n.Contains("propblade") || n.Contains("spinner")
                || n.Contains("gear") || n.Contains("nacelle") || n.Contains("engine")
                || n.Contains("tank") || n.Contains("hose") || n.Contains("column")
                || n.Contains("canopy_post") || n.Contains("crane") || n.Contains("antenna"))
                return SurfaceKind.Metal;
            if (n.Contains("marking") || n.Contains("centreline") || n.Contains("centerline")
                || n.Contains("threshold") || n.Contains("hold_short") || n.Contains("aiming")
                || n.Contains("tdz") || n.Contains("chevron") || n.Contains("stand_stop")
                || n.Contains("bay line") || n.Contains("stall line") || n.Contains("access dash")
                || n.Contains("edge line") || n.Contains("zebra"))
                return SurfaceKind.PaintedLine;
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
            // F3 foliage / scrub / hills — before generic "canopy" concrete match.
            if (n.Contains("leaf") || n.Contains("scrub") || n.Contains("tuft")
                || n.Contains("tree_") || n.StartsWith("tree ") || n.Contains("grass_tuft")
                || n.Contains("planter_scrub") || n.Contains("hill_") || n.Contains("dune_")
                || n.Contains("paddock") || n.Contains("canopy_b") || n.Contains("canopy_c")
                || n.Contains("canopy_d") || n.EndsWith("_canopy") || n.Contains("tree canopy"))
                return SurfaceKind.Grass;
            if (n.Contains("fence") || n.Contains("gate_") || n.Contains("bollard") || n.Contains("kerb")
                || n.Contains("sign_post") || n.Contains("sign_frame") || n.Contains("trolley"))
                return SurfaceKind.Metal;
            if (n.Contains("trunk") || n.Contains("bark") || n.Contains("flare") || n.Contains("fork")
                || n.Contains("bench") || n.Contains("planter") || n.Contains("rock"))
                return SurfaceKind.PaintedMetal;
            if (n.Contains("coast_sand") || n.Equals("berm"))
                return SurfaceKind.Sand;
            if (n.Contains("coast_water") || n.Contains("shallows"))
                return SurfaceKind.Water;
            if (n.Contains("terminal") || n.Contains("concrete") || n.Contains("apron")
                || n.Contains("canopy") || n.Contains("end_cap") || n.Contains("entrance"))
                return SurfaceKind.Concrete;
            return SurfaceKind.PaintedMetal;
        }

        /// <summary>
        /// Cache of materials handed out by <see cref="CreateShared"/>, keyed on every
        /// input <see cref="Create"/> actually reads. Everything else Create consults is
        /// static shared state, so identical inputs always produce an identical material.
        /// </summary>
        private static readonly Dictionary<SharedMaterialKey, Material> SharedMaterials = new();

        /// <summary>
        /// Shared, de-duplicated variant of <see cref="Create"/> for callers that assign
        /// to <c>Renderer.sharedMaterial</c> — the kit loaders, which between them build a
        /// material for every one of the ~2,662 meshes in the art library even though only
        /// a few dozen are distinct.
        ///
        /// The returned material is shared: never mutate it. Runtime tinting already goes
        /// through <c>Renderer.material</c>, whose first read clones the material for that
        /// renderer, so per-object colour and emission updates stay correct and private.
        /// </summary>
        public static Material CreateShared(
            Color color,
            SurfaceKind kind = SurfaceKind.Default,
            Texture2D albedo = null,
            Vector2? tiling = null,
            bool preferProcedural = false,
            bool useTextures = true)
        {
            var key = new SharedMaterialKey(color, kind, albedo, tiling, preferProcedural, useTextures);
            // Play-mode exit destroys runtime materials while the static cache survives a
            // disabled domain reload, so a hit can be a destroyed object — rebuild those.
            if (SharedMaterials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = Create(color, kind, albedo, tiling, preferProcedural, useTextures);
            SharedMaterials[key] = material;
            return material;
        }

        private readonly struct SharedMaterialKey : IEquatable<SharedMaterialKey>
        {
            private readonly Color _color;
            private readonly SurfaceKind _kind;
            private readonly int _albedoId;
            private readonly Vector2 _tiling;
            private readonly bool _hasTiling;
            private readonly bool _preferProcedural;
            private readonly bool _useTextures;

            public SharedMaterialKey(
                Color color, SurfaceKind kind, Texture2D albedo, Vector2? tiling,
                bool preferProcedural = false, bool useTextures = true)
            {
                _color = color;
                _kind = kind;
                _albedoId = albedo != null ? albedo.GetInstanceID() : 0;
                _hasTiling = tiling.HasValue;
                _tiling = tiling ?? Vector2.zero;
                _preferProcedural = preferProcedural;
                _useTextures = useTextures;
            }

            // Component-wise Equals, not == : Unity's Color and Vector2 equality operators
            // are approximate, which would disagree with GetHashCode and corrupt lookups.
            public bool Equals(SharedMaterialKey other) =>
                _kind == other._kind
                && _albedoId == other._albedoId
                && _hasTiling == other._hasTiling
                && _preferProcedural == other._preferProcedural
                && _useTextures == other._useTextures
                && _tiling.x.Equals(other._tiling.x)
                && _tiling.y.Equals(other._tiling.y)
                && _color.r.Equals(other._color.r)
                && _color.g.Equals(other._color.g)
                && _color.b.Equals(other._color.b)
                && _color.a.Equals(other._color.a);

            public override bool Equals(object obj) => obj is SharedMaterialKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + _color.r.GetHashCode();
                    hash = hash * 31 + _color.g.GetHashCode();
                    hash = hash * 31 + _color.b.GetHashCode();
                    hash = hash * 31 + _color.a.GetHashCode();
                    hash = hash * 31 + (int)_kind;
                    hash = hash * 31 + _albedoId;
                    hash = hash * 31 + _tiling.x.GetHashCode();
                    hash = hash * 31 + _tiling.y.GetHashCode();
                    hash = hash * 31 + (_hasTiling ? 1 : 0);
                    hash = hash * 31 + (_preferProcedural ? 1 : 0);
                    hash = hash * 31 + (_useTextures ? 1 : 0);
                    return hash;
                }
            }
        }

        public static Material Create(
            Color color,
            SurfaceKind kind = SurfaceKind.Default,
            Texture2D albedo = null,
            Vector2? tiling = null,
            bool preferProcedural = false,
            bool useTextures = true)
        {
            var profile = GetProfile(kind);
            EnsureSharedMaps();
            EnsureAuthoredMaps();
            // Opaque RGB callers (terminal glass colors) still need real alpha panes.
            // Aircraft preferProcedural keeps authored window alphas (opaque dark REF panes).
            if (!preferProcedural
                && (kind == SurfaceKind.Glass || kind == SurfaceKind.Water)
                && color.a >= 0.99f)
                color.a = kind == SurfaceKind.Glass ? 0.42f : 0.62f;

            // Batch F1 MAT-001 — prefer inspectable authored materials when present.
            // Aircraft kits pass preferProcedural so REF palette colours are not replaced by
            // glass/metal authored mats that hollow out the fuselage. A mesh with no usable
            // UVs passes useTextures: false, because it would sample a single texel.
            if (!preferProcedural && useTextures
                && TryInstantiateAuthored(kind, color, tiling, out var authoredInstance))
                return authoredInstance;

            Shader shader;
            if (kind == SurfaceKind.UnlitSky)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            }
            else
            {
                shader = _litShader ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (kind == SurfaceKind.UnlitSky)
            {
                // Keep sky/stars/discs free of Lit shading so day tint reads cleanly.
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color);
                }

                return material;
            }

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

            var resolvedTiling = tiling ?? ResolveDefaultTiling(kind);

            if (useTextures && albedo != null)
            {
                material.mainTexture = albedo;
                material.mainTextureScale = resolvedTiling;
            }
            else if (!preferProcedural
                     && useTextures
                     && AuthoredAlbedo.TryGetValue(kind, out var authoredAlbedo)
                     && authoredAlbedo != null)
            {
                material.mainTexture = authoredAlbedo;
                material.mainTextureScale = resolvedTiling;
            }

            // Flat REF palette on aircraft — skip noisy skin/glass maps that fragment the hull.
            if (!preferProcedural)
            {
                var normal = ResolveNormal(kind);
                if (useTextures && kind != SurfaceKind.UnlitSky && normal != null
                    && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                    if (material.HasProperty("_BumpScale"))
                        material.SetFloat("_BumpScale", profile.BumpScale);
                    material.SetTextureScale("_BumpMap", resolvedTiling);
                }

                var ao = ResolveAo(kind);
                if (useTextures && kind != SurfaceKind.UnlitSky && ao != null
                    && material.HasProperty("_OcclusionMap"))
                {
                    material.SetTexture("_OcclusionMap", ao);
                    if (material.HasProperty("_OcclusionStrength"))
                        material.SetFloat("_OcclusionStrength", Mathf.Clamp01(profile.Occlusion));
                    material.SetTextureScale("_OcclusionMap", resolvedTiling * 0.5f);
                }

                if (useTextures && AuthoredMasks.TryGetValue(kind, out var mask) && mask != null
                    && material.HasProperty("_MetallicGlossMap"))
                {
                    material.SetTexture("_MetallicGlossMap", mask);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    material.SetTextureScale("_MetallicGlossMap", resolvedTiling);
                }
            }
            else if (material.HasProperty("_Cull"))
            {
                // Belt-and-suspenders for inverted authored kits until winding is trusted.
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }

            // Opaque dark aircraft glazing (preferProcedural + full alpha) stays Opaque Lit.
            var transparent = color.a < 0.99f
                || (profile.Transparent && !(preferProcedural && color.a >= 0.99f));
            if (transparent)
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
        /// <summary>
        /// Toggles a shader keyword only when it actually changes. A keyword write forces
        /// Unity to re-resolve the shader variant and drops the material out of its SRP
        /// Batcher batch, so a redundant set is far from free.
        /// </summary>
        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled)
                return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

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
            // Cool puddle tint + modest darken — keep apron readable (0.28 crushed pavement to ink).
            var wetTint = new Color(0.04f, 0.07f, 0.12f, 0f);
            var wetColor = Color.Lerp(dryColor, dryColor * 0.58f + wetTint, wetness01);
            wetColor.a = dryColor.a;
            material.color = wetColor;
            // URP Lit reads _BaseColor; keep it in sync with .color so wet darken shows.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", wetColor);
            if (material.HasProperty("_SpecColor"))
            {
                var spec = Color.Lerp(
                    new Color(0.2f, 0.2f, 0.2f),
                    new Color(0.55f, 0.62f, 0.7f),
                    wetness01);
                material.SetColor("_SpecColor", spec);
            }

            var targetSmooth = Mathf.Max(drySmoothness, 0.96f);
            var smoothness = Mathf.Lerp(drySmoothness, targetSmooth, wetness01 * wetness01);
            // Dry metallic-gloss masks cap wet sheen — drop the keyword while wet so
            // _Smoothness reads (0025 item 4). Re-enable when dry.
            if (material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null)
            {
                SetKeyword(material, "_METALLICSPECGLOSSMAP", wetness01 <= 0.2f);
            }

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            var metallic = Mathf.Lerp(dryMetallic, Mathf.Max(dryMetallic, 0.28f), wetness01 * 0.95f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);

            // Wet surfaces lose micro-relief — bump flattens toward a mirror sheen.
            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", Mathf.Lerp(dryBumpScale, dryBumpScale * 0.16f, wetness01));

            // Slight AO deepen so wet pavement reads puddled rather than just glossy.
            if (material.HasProperty("_OcclusionStrength"))
                material.SetFloat("_OcclusionStrength", Mathf.Lerp(1f, 1.35f, wetness01));

            if (material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoatMask", wetness01);
                if (material.HasProperty("_ClearCoatSmoothness"))
                    material.SetFloat("_ClearCoatSmoothness", Mathf.Lerp(0.12f, 0.99f, wetness01));
                SetKeyword(material, "_CLEARCOAT", wetness01 > 0.02f);
            }
            else if (wetness01 > 0.02f)
            {
                // Older URP Lit without ClearCoat: push specular + cool sheen so wet still reads.
                var boostedSmooth = Mathf.Lerp(drySmoothness, Mathf.Max(drySmoothness, 0.99f), wetness01);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", boostedSmooth);
                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", boostedSmooth);
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", Mathf.Lerp(dryMetallic, Mathf.Max(dryMetallic, 0.55f), wetness01));
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    var sheen = new Color(0.07f, 0.12f, 0.16f) * (wetness01 * 0.52f);
                    material.SetColor("_EmissionColor", sheen);
                }
            }
            else if (material.HasProperty("_EmissionColor") && !material.HasProperty("_ClearCoatMask"))
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }
        }

        private static Vector2 ResolveDefaultTiling(SurfaceKind kind) =>
            DefaultTilingByKind.TryGetValue(kind, out var tiling) ? tiling : Vector2.one;

        private static Texture2D ResolveNormal(SurfaceKind kind)
        {
            if (AuthoredNormals.TryGetValue(kind, out var authored) && authored != null)
                return authored;
            if (KindNormals.TryGetValue(kind, out var kindNormal) && kindNormal != null)
                return kindNormal;
            return _sharedNormal;
        }

        /// <summary>
        /// Batch F1 MAT-001 — instance an authored Resources material when present.
        /// Returns false so callers keep the procedural Lit Create path.
        /// </summary>
        private static bool TryInstantiateAuthored(
            SurfaceKind kind,
            Color color,
            Vector2? tiling,
            out Material instance)
        {
            instance = null;
            var key = AuthoredMaterialKey(kind);
            if (key == null)
                return false;
            var template = Resources.Load<Material>($"Airside/Materials/{key}");
            if (template == null)
                return false;

            instance = new Material(template);
            if (instance.HasProperty("_BaseColor"))
            {
                var baseColor = color;
                if ((kind == SurfaceKind.Glass || kind == SurfaceKind.Water) && baseColor.a >= 0.99f)
                    baseColor.a = kind == SurfaceKind.Glass ? 0.42f : 0.62f;
                instance.SetColor("_BaseColor", baseColor);
            }
            else
            {
                instance.color = color;
            }

            if (tiling.HasValue)
            {
                instance.mainTextureScale = tiling.Value;
                if (instance.HasProperty("_BumpMap"))
                    instance.SetTextureScale("_BumpMap", tiling.Value);
                if (instance.HasProperty("_OcclusionMap"))
                    instance.SetTextureScale("_OcclusionMap", tiling.Value);
            }

            return true;
        }

        private static string AuthoredMaterialKey(SurfaceKind kind) =>
            kind switch
            {
                SurfaceKind.Asphalt => "mat_asphalt_v01",
                SurfaceKind.Concrete => "mat_concrete_v01",
                SurfaceKind.Grass => "mat_grass_v01",
                SurfaceKind.Metal => "mat_corrugated_metal_v01",
                SurfaceKind.PaintedMetal => "mat_corrugated_metal_v01",
                SurfaceKind.Glass => "mat_glass_v01",
                SurfaceKind.PaintedLine => "mat_painted_line_v01",
                SurfaceKind.AircraftSkin => "mat_aircraft_v01",
                SurfaceKind.Water => "mat_wet_v01",
                _ => null
            };

        private static Texture2D ResolveAo(SurfaceKind kind)
        {
            if (AuthoredAo.TryGetValue(kind, out var authored) && authored != null)
                return authored;
            if (KindOcclusion.TryGetValue(kind, out var kindAo) && kindAo != null)
                return kindAo;
            return _sharedOcclusion;
        }

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
            if (_sharedNormal != null && _sharedOcclusion != null && KindNormals.Count > 0)
                return;

            const int size = 64;
            _sharedNormal ??= BuildNormalMap(size, seed: 17, strength: 2f, name: "airside_proc_normal");
            _sharedOcclusion ??= BuildOcclusionMap(size, seed: 41, dark: 180, span: 75, name: "airside_proc_ao");

            // Per-kind procedural fallbacks so Glass/Rubber/PaintedLine/Plastic do not
            // share the generic asphalt-like micro-relief (MAT-001 / 0025 item 4).
            EnsureKindMaps(SurfaceKind.Glass, size, normalSeed: 101, strength: 0.35f, aoSeed: 102, dark: 230, span: 20);
            EnsureKindMaps(SurfaceKind.Rubber, size, normalSeed: 211, strength: 3.4f, aoSeed: 212, dark: 140, span: 90);
            EnsureKindMaps(SurfaceKind.PaintedLine, size, normalSeed: 307, strength: 0.55f, aoSeed: 308, dark: 210, span: 30);
            EnsureKindMaps(SurfaceKind.Plastic, size, normalSeed: 419, strength: 1.1f, aoSeed: 420, dark: 195, span: 45);
            EnsureKindMaps(SurfaceKind.AircraftSkin, size, normalSeed: 503, strength: 0.7f, aoSeed: 504, dark: 215, span: 28);
            EnsureKindMaps(SurfaceKind.Water, size, normalSeed: 601, strength: 1.6f, aoSeed: 602, dark: 200, span: 40);
        }

        private static void EnsureKindMaps(
            SurfaceKind kind,
            int size,
            int normalSeed,
            float strength,
            int aoSeed,
            int dark,
            int span)
        {
            if (!KindNormals.ContainsKey(kind) || KindNormals[kind] == null)
                KindNormals[kind] = BuildNormalMap(size, normalSeed, strength, $"airside_proc_normal_{kind}");
            if (!KindOcclusion.ContainsKey(kind) || KindOcclusion[kind] == null)
                KindOcclusion[kind] = BuildOcclusionMap(size, aoSeed, dark, span, $"airside_proc_ao_{kind}");
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
        private static Texture2D BuildNormalMap(int size, int seed, float strength = 2f, string name = "airside_proc_normal")
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
                name = name,
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
                var dx = (hL - hR) * strength;
                var dy = (hD - hU) * strength;
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

        private static Texture2D BuildOcclusionMap(
            int size,
            int seed,
            int dark = 180,
            int span = 75,
            string name = "airside_proc_ao")
        {
            var rng = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var v = (byte)Mathf.Clamp(dark + rng.Next(0, Mathf.Max(1, span)), 0, 255);
                pixels[i] = new Color32(v, v, v, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }
    }
}
