using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Condensation trails behind high jets in the sky traffic: one thin trail per engine that widens as it ages, only
    /// above <see cref="ContrailFromFeet"/> and only where the weather lets you see them (not under rain, fog or storm).
    /// Presentation only: a TrailRenderer follows the drawn aircraft and nothing here touches the simulation.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>Real cruise altitude above which a jet leaves a trail (the air is cold and humid enough).</summary>
        public const double ContrailFromFeet = 24000.0;

        private const string ContrailName = "Contrail";
        private static Material _contrailMaterial;

        /// <summary>0..1 how visible trails are in this weather: bright in clear air, thin under overcast, none in rain, fog or storm.</summary>
        public static float ContrailVisibility(WeatherKind weather) => weather switch
        {
            WeatherKind.Clear => 1f,
            WeatherKind.Cloudy => 0.75f,
            WeatherKind.Overcast => 0.4f,
            _ => 0f
        };

        /// <summary>Jets only (turboprops and helicopters leave none) and only high enough.</summary>
        public static bool ContrailPossible(AircraftType type, bool hasPropellers, double altitudeFeet) =>
            type != null && !type.IsRotorcraft && !hasPropellers && altitudeFeet >= ContrailFromFeet;

        private void UpdateContrails(Transform view, AircraftType type, bool hasPropellers, double altitudeFeet)
        {
            if (view == null)
                return;
            var visibility = ContrailPossible(type, hasPropellers, altitudeFeet) ? ContrailVisibility(CurrentWeather) : 0f;
            var existing = view.Find(ContrailName);
            if (visibility <= 0.01f)
            {
                if (existing != null && existing.gameObject.activeSelf)
                {
                    existing.gameObject.SetActive(false);
                    foreach (var trail in existing.GetComponentsInChildren<TrailRenderer>(true))
                        trail.Clear();
                }
                return;
            }

            if (existing == null)
                existing = BuildContrails(view, type);
            if (existing == null)
                return;
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);
            var tint = new Color(1f, 1f, 1f, 0.55f * visibility * Mathf.Lerp(0.55f, 1f, PresentationDaylight));
            var material = existing.GetComponentInChildren<TrailRenderer>(true)?.sharedMaterial;
            if (material != null)
                material.SetColor("_BaseColor", tint);
        }

        private Transform BuildContrails(Transform view, AircraftType type)
        {
            var material = ContrailMaterial();
            if (material == null)
                return null;
            var span = AircraftCatalogue.TryFor(type, out var spec) ? (float)spec.WingspanMetres : 34f;
            var root = new GameObject(ContrailName).transform;
            root.SetParent(view, false);
            // Models are authored in metres, so the child inherits the aircraft's scale: undo it for trail widths.
            var scale = view.lossyScale.x > 0.0001f ? view.lossyScale.x : 1f;
            var engines = span >= 50f ? new[] { -0.17f, -0.30f, 0.17f, 0.30f } : new[] { -0.17f, 0.17f };
            foreach (var fraction in engines)
            {
                var go = new GameObject("Trail").transform;
                go.SetParent(root, false);
                go.localPosition = new Vector3(fraction * span / scale, -0.4f, -4f / scale);
                var trail = go.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.alignment = LineAlignment.View;
                trail.numCapVertices = 2;
                trail.minVertexDistance = 12f;
                trail.time = 45f;
                trail.widthMultiplier = 9f;
                // Thin at the engine, spreading as the trail ages, then dissolving.
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.15f, 0.55f),
                    new Keyframe(1f, 1f));
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.6f),
                        new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = fade;
            }
            return root;
        }

        private static Material ContrailMaterial()
        {
            if (_contrailMaterial != null)
                return _contrailMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            _contrailMaterial = new Material(shader) { name = "Airside contrail" };
            _contrailMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
            _contrailMaterial.SetFloat("_Surface", 1f);
            _contrailMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _contrailMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _contrailMaterial.SetInt("_ZWrite", 0);
            _contrailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _contrailMaterial.renderQueue = (int)RenderQueue.Transparent + 3;
            return _contrailMaterial;
        }
    }
}
