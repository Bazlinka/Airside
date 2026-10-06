using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0193 — rolling cloud ceiling, distant horizon banks and depth-integrated ground fog.
    /// Their strength comes from AtmosphereLook; presentation never alters operational weather.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const string AtmosphereRootName = "Atmosphere layers";
        private const float HorizonBandRadius = 14_000f;
        private const int HorizonBandSegments = 24;
        private Renderer _groundFog;
        private bool _weatherVolumeReported;

        private Transform _atmosphereRoot;
        private Renderer _stratusSheet;
        private readonly List<Renderer> _horizonBand = new();
        private readonly Dictionary<int, Color> _cloudTints = new();
        private readonly Dictionary<int, Renderer[]> _cloudRenderers = new();
        private readonly Dictionary<int, Renderer> _cloudUmbraRenderers = new();
        private readonly Dictionary<int, Color> _cloudUmbraTints = new();
        private MaterialPropertyBlock _atmosphereBlock;
        private static Texture2D _softNoise;

        private void StoreCloudTint(int index, Color tint) => _cloudTints[index] = tint;

        /// <summary>Card colour with its wrap-edge fade (ADR 0143), every frame, cheaply.</summary>
        private void ApplyCloudEdgeFade(Transform cloud, int index, float edge)
        {
            if (!_cloudTints.TryGetValue(index, out var tint))
                return;
            var faded = tint;
            faded.a *= edge;
            // Runs for every cloud every frame: fetching the renderers each time allocated a new
            // array per cloud per frame, garbage that later lands as a collection hitch.
            if (!_cloudRenderers.TryGetValue(index, out var renderers) || renderers.Length == 0 || renderers[0] == null)
                _cloudRenderers[index] = renderers = cloud.GetComponentsInChildren<Renderer>();
            for (var r = 0; r < renderers.Length; r++)
            {
                renderers[r].enabled = faded.a > 0.01f;
                SetRendererColor(renderers[r], faded);
            }
            if (_cloudUmbraRoot != null && index < _cloudUmbraRoot.childCount
                && _cloudUmbraTints.TryGetValue(index, out var umbraTint))
            {
                if (!_cloudUmbraRenderers.TryGetValue(index, out var umbra) || umbra == null)
                    _cloudUmbraRenderers[index] = umbra = _cloudUmbraRoot.GetChild(index).GetComponent<Renderer>();
                if (umbra != null)
                {
                    umbraTint.a *= edge;
                    umbra.enabled = umbraTint.a > 0.001f;
                    SetRendererColor(umbra, umbraTint);
                }
            }
        }

        private void UpdateAtmosphereLayers()
        {
            if (!AirsideBareField.Enabled)
                return;
            if (_atmosphereRoot == null)
                BuildAtmosphereLayers();
            if (_atmosphereRoot == null || _mainCamera == null)
                return;
            if (!AirsideSettings.Current.WeatherLayers)
            {
                HideAtmosphereLayers();
                return;
            }

            var flow = WeatherWindFlow.ShaderGlobal(PresentationWind);
            Shader.SetGlobalVector("_AirsideWeatherWind", new Vector4(flow.X, 0f, flow.Z, 0f));
            Shader.SetGlobalFloat("_AirsideWeatherTime", Time.unscaledTime);
            Shader.SetGlobalVector("_AirsideWeatherRange", new Vector4(
                WeatherCoverage.AtmosphereFadeStart, WeatherCoverage.AtmosphereDistance, 0f, 0f));
            var sky = ToColor(_atmosphere.Sky);
            var daylight = PresentationDaylight;
            var camera = _mainCamera.transform.position;
            _atmosphereBlock ??= new MaterialPropertyBlock();

            // The deck follows horizontal travel; height stays in world space, visible from either side.
            if (_stratusSheet != null)
            {
                var alpha = _atmosphere.Stratus;
                _stratusSheet.enabled = alpha > 0.01f;
                _stratusSheet.transform.position = new Vector3(camera.x, AtmosphereLook.StratusHeightMetres, camera.z);
                var under = Color.Lerp(sky * 0.85f, sky, 0.5f);
                SetLayerColour(_stratusSheet, new Color(under.r, under.g, under.b, alpha));
            }

            // Horizon band: a ring round the camera, tinted from the sky.
            var bandTint = Color.Lerp(sky, Color.white, 0.18f * daylight) * Mathf.Lerp(0.9f, 1f, daylight);
            bandTint.a = _atmosphere.HorizonBand * Mathf.Lerp(0.5f, 1f, daylight);
            for (var i = 0; i < _horizonBand.Count; i++)
            {
                var segment = _horizonBand[i];
                segment.enabled = bandTint.a > 0.01f;
                var angle = (i + 0.5f) / _horizonBand.Count * Mathf.PI * 2f;
                var at = new Vector3(camera.x + Mathf.Sin(angle) * HorizonBandRadius, 700f,
                    camera.z + Mathf.Cos(angle) * HorizonBandRadius);
                segment.transform.position = at;
                segment.transform.rotation = Quaternion.LookRotation(at - new Vector3(camera.x, 700f, camera.z), Vector3.up);
                SetLayerColour(segment, bandTint);
            }

            // Integrate ground fog through scene depth rather than stacking flat sheets. From
            // above it settles into a shallow bank; a follow camera actually moves through it.
            if (_groundFog != null)
            {
                var planeDistance = _mainCamera.nearClipPlane + 0.1f;
                var planeHeight = 2f * planeDistance * Mathf.Tan(_mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                // Parented to the camera, not placed from its position: this runs in Update but the camera
                // moves in LateUpdate, so a world-placed quad this close to the lens trailed one frame behind
                // a drag or orbit and fell out of view, and the weather vanished for as long as you dragged.
                var fogTransform = _groundFog.transform;
                if (fogTransform.parent != _mainCamera.transform)
                    fogTransform.SetParent(_mainCamera.transform, false);
                fogTransform.localPosition = new Vector3(0f, 0f, planeDistance);
                fogTransform.localRotation = Quaternion.identity;
                fogTransform.localScale = new Vector3(planeHeight * _mainCamera.aspect * 1.01f,
                    planeHeight * 1.01f, 1f);
                var strength = _atmosphere.Mist;
                _groundFog.enabled = strength > 0.01f;
                var fogColour = ToColor(_atmosphere.Fog);
                SetLayerColour(_groundFog, new Color(fogColour.r, fogColour.g, fogColour.b, strength));
                if (SoakMode && !_weatherVolumeReported && Time.unscaledTime > 8f)
                {
                    _weatherVolumeReported = true;
                    Debug.Log($"[Airside weather] {CurrentWeather} mist {strength:0.00} fog enabled {_groundFog.enabled} " +
                        $"shader {_groundFog.sharedMaterial.shader.name} camera {camera} " +
                        $"tint {fogColour} layers {AirsideSettings.Current.WeatherLayers}");
                }
            }

        }

        private void HideAtmosphereLayers()
        {
            if (_groundFog != null)
                _groundFog.enabled = false;
            if (_stratusSheet != null)
                _stratusSheet.enabled = false;
            for (var i = 0; i < _horizonBand.Count; i++)
                _horizonBand[i].enabled = false;
        }

        private void SetLayerColour(Renderer renderer, Color colour)
        {
            _atmosphereBlock.Clear();
            _atmosphereBlock.SetColor("_BaseColor", colour);
            renderer.SetPropertyBlock(_atmosphereBlock);
        }

        private void BuildAtmosphereLayers()
        {
            var material = SoftLayerMaterial();
            if (material == null)
                return;
            _atmosphereRoot = new GameObject(AtmosphereRootName).transform;

            var ceilingShader = Shader.Find("Airside/WeatherCeiling");
            var ceilingMaterial = ceilingShader != null
                ? new Material(ceilingShader) { name = "Airside rolling cloud ceiling" } : material;
            _stratusSheet = LayerQuad("Overcast sheet", ceilingMaterial, new Vector3(WeatherCoverage.AtmosphereDistance * 2f, WeatherCoverage.AtmosphereDistance * 2f, 1f));
            _stratusSheet.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var width = 2f * Mathf.PI * HorizonBandRadius / HorizonBandSegments * 1.08f;
            for (var i = 0; i < HorizonBandSegments; i++)
                _horizonBand.Add(LayerQuad($"Horizon band {i}", material, new Vector3(width, 1_100f, 1f)));

            var volumeShader = Shader.Find("Airside/HeightFog");
            if (volumeShader != null)
            {
                var fog = GameObject.CreatePrimitive(PrimitiveType.Quad);
                fog.name = "Drifting ground fog";
                DestroyPresentationObject(fog.GetComponent<Collider>());
                fog.transform.SetParent(_atmosphereRoot, false);
                _groundFog = fog.GetComponent<Renderer>();
                var fogMaterial = new Material(volumeShader) { name = "Airside ground fog volume" };
                if (_mainCamera != null)
                    _mainCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
                _groundFog.sharedMaterial = fogMaterial;
                _groundFog.shadowCastingMode = ShadowCastingMode.Off;
                _groundFog.receiveShadows = false;
                _groundFog.enabled = false;
            }

        }

        private Renderer LayerQuad(string name, Material material, Vector3 scale)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            DestroyPresentationObject(quad.GetComponent<Collider>());
            quad.transform.SetParent(_atmosphereRoot, false);
            quad.transform.localScale = scale;
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>URP Unlit, alpha blended, no depth write, over a soft tiling noise.</summary>
        private static Material SoftLayerMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;
            var material = new Material(shader) { name = "mat_atmosphere_layer" };
            material.SetTexture("_BaseMap", SoftNoise());
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent - 10;
            return material;
        }

        /// <summary>A soft, blotchy noise that fades to nothing at its edges (value noise, 3 octaves).</summary>
        private static Texture2D SoftNoise()
        {
            if (_softNoise != null)
                return _softNoise;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "airside_soft_noise", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size;
                var v = (y + 0.5f) / size;
                var n = 0.55f * Mathf.PerlinNoise(u * 4f, v * 4f)
                        + 0.3f * Mathf.PerlinNoise(u * 9f + 17f, v * 9f + 5f)
                        + 0.15f * Mathf.PerlinNoise(u * 21f + 3f, v * 21f + 11f);
                var edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 5f);
                var alpha = Mathf.Clamp01((n - 0.18f) * 1.6f) * edge * edge * (3f - 2f * edge);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            _softNoise = texture;
            return texture;
        }
    }
}
