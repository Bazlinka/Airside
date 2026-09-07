using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 5 spike — runtime global Volume with ACES tonemap,
    /// mild bloom/vignette, and day-driven color adjustments. Presentation only;
    /// not a full probe bake or authored day profiles.
    /// </summary>
    public sealed class AirsideDayVolume
    {
        private readonly ColorAdjustments _color;
        private readonly Bloom _bloom;
        private readonly Vignette _vignette;

        private AirsideDayVolume(ColorAdjustments color, Bloom bloom, Vignette vignette)
        {
            _color = color;
            _bloom = bloom;
            _vignette = vignette;
        }

        public static AirsideDayVolume Ensure(Transform host)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            Volume volume;
            if (existing != null && existing.isGlobal)
            {
                volume = existing;
            }
            else
            {
                var go = new GameObject("Airside Day Volume");
                go.transform.SetParent(host, false);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            if (volume.profile == null)
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var profile = volume.profile;
            if (!profile.TryGet(out Tonemapping tonemap))
                tonemap = profile.Add<Tonemapping>(true);
            tonemap.active = true;
            tonemap.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet(out ColorAdjustments color))
                color = profile.Add<ColorAdjustments>(true);
            color.active = true;

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.55f);

            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(new Color(0.05f, 0.07f, 0.12f));
            vignette.smoothness.Override(0.45f);

            // Ensure the main camera actually runs the URP post stack.
            var camera = Camera.main;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = true;
            }

            return new AirsideDayVolume(color, bloom, vignette);
        }

        public void Apply(float daylight, float warm)
        {
            // Day: slight lift; dusk: warmer filter; night: darker exposure + bloom.
            var exposure = Mathf.Lerp(-0.55f, 0.12f, daylight) + warm * 0.08f;
            var contrast = Mathf.Lerp(8f, 4f, daylight);
            var filter = Color.Lerp(
                new Color(0.72f, 0.78f, 1f),
                Color.Lerp(Color.white, new Color(1f, 0.82f, 0.62f), warm),
                Mathf.Clamp01(daylight + warm * 0.35f));

            _color.postExposure.Override(exposure);
            _color.contrast.Override(contrast);
            _color.colorFilter.Override(filter);
            _color.saturation.Override(Mathf.Lerp(6f, 2f, daylight));

            _bloom.intensity.Override(Mathf.Lerp(0.55f, 0.18f, daylight));
            _vignette.intensity.Override(Mathf.Lerp(0.32f, 0.12f, daylight));
        }
    }
}
