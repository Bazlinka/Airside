using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private const int WeatherRainDrops = 768;
        private static Mesh _weatherRainMesh;
        private static Vector3[] _weatherRainVertices;
        private static readonly Vector3[] WeatherRainSeeds = new Vector3[WeatherRainDrops];
        private Renderer _weatherRainRenderer;

        // A single dynamic mesh replaces hundreds of separately drawn cube drops. No per-frame
        // allocations, particle simulation or randomness can change airport state.
        private static Transform BuildRainRoot()
        {
            var root = new GameObject("Rain").transform;
            var shader = Shader.Find("Airside/WeatherRain");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "Airside rain streaks" };
            material.SetColor("_BaseColor", new Color(0.77f, 0.83f, 0.88f, 0.32f));
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + 5;
            _weatherRainVertices = new Vector3[WeatherRainDrops * 4];
            var uv = new Vector2[WeatherRainDrops * 4];
            var triangles = new int[WeatherRainDrops * 6];
            var rng = new System.Random(42);
            for (var i = 0; i < WeatherRainDrops; i++)
            {
                WeatherRainSeeds[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var v = i * 4; var t = i * 6;
                uv[v] = new Vector2(0f, 0f); uv[v+1] = new Vector2(1f, 0f);
                uv[v+2] = new Vector2(0f, 1f); uv[v+3] = new Vector2(1f, 1f);
                triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1;
                triangles[t+3] = v+1; triangles[t+4] = v+2; triangles[t+5] = v+3;
            }
            _weatherRainMesh = new Mesh { name = "Batched wind-driven rain" };
            _weatherRainMesh.MarkDynamic();
            _weatherRainMesh.vertices = _weatherRainVertices;
            _weatherRainMesh.uv = uv;
            _weatherRainMesh.triangles = triangles;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = _weatherRainMesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.gameObject.SetActive(false);
            return root;
        }

        private void UpdateRainMesh(float precipitation, bool storm)
        {
            var altitudeFade = CockpitObserverWeather.Rain(1f,
                _cockpitView != null ? _cockpitView.position.y : 0f, InCockpit && _cockpitView != null);
            precipitation = CockpitObserverWeather.Rain(precipitation,
                _cockpitView != null ? _cockpitView.position.y : 0f, InCockpit && _cockpitView != null);
            if (_mainCamera == null || _weatherRainMesh == null)
                return;
            _weatherRainRenderer ??= _rainRoot.GetComponent<Renderer>();
            var range = _cameraController != null
                ? Vector3.Distance(_mainCamera.transform.position, _cameraController.FocusPoint) : 100f;
            // Keep droplets near the lens at every zoom, instead of stretching metre-wide cubes
            // over the apron. The world-depth test hides streaks behind aircraft and roofs.
            var radius = Mathf.Clamp(range * 0.13f, 18f, 420f);
            var centre = _mainCamera.transform.position + _mainCamera.transform.forward * radius * 1.25f;
            _rainRoot.position = centre;
            _rainRoot.localScale = Vector3.one;
            var flow = WeatherWindFlow.Rain(PresentationWind, storm);
            var drift = new Vector3(flow.X, 0f, flow.Z);
            var fall = Vector3.down * Mathf.Lerp(16f, 28f, precipitation) + drift;
            var halfLength = Mathf.Clamp(radius * 0.015f, 0.18f, 4.8f);
            var width = Mathf.Clamp(radius * 0.0008f, 0.012f, 0.32f);
            var right = _mainCamera.transform.right * width;
            var along = fall.normalized * halfLength;
            var count = Mathf.CeilToInt(WeatherRainDrops * Mathf.Lerp(0.25f, 1f, precipitation));
            var time = Time.unscaledTime;
            for (var i = 0; i < WeatherRainDrops; i++)
            {
                var seed = WeatherRainSeeds[i];
                var p = new Vector3(
                    (Mathf.Repeat(seed.x + drift.x * time / (radius * 2f), 1f) * 2f - 1f) * radius,
                    (Mathf.Repeat(seed.y + fall.y * time / (radius * 2f), 1f) * 2f - 1f) * radius,
                    (Mathf.Repeat(seed.z + drift.z * time / (radius * 2f), 1f) * 2f - 1f) * radius);
                var v = i * 4;
                if (i >= count)
                {
                    _weatherRainVertices[v] = _weatherRainVertices[v+1] = _weatherRainVertices[v+2] = _weatherRainVertices[v+3] = p;
                    continue;
                }
                _weatherRainVertices[v] = p - right - along;
                _weatherRainVertices[v+1] = p + right - along;
                _weatherRainVertices[v+2] = p - right + along;
                _weatherRainVertices[v+3] = p + right + along;
            }
            _weatherRainMesh.vertices = _weatherRainVertices;
            _weatherRainMesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2f + 10f));
            var tint = Color.Lerp(new Color(0.30f, 0.38f, 0.48f), new Color(0.77f, 0.83f, 0.88f), PresentationDaylight);
            tint.a = Mathf.Lerp(0.28f, 0.60f, precipitation) * altitudeFade;
            SetRendererColor(_weatherRainRenderer, tint);
        }
    }
}
