using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// Turns URP's post stack on for the runtime camera and gives it a deliberate look.
    ///
    /// Two things were in the way. The camera is created in code and never carried a
    /// <see cref="UniversalAdditionalCameraData"/>, so post-processing and camera
    /// anti-aliasing were both off — and the Presentation assembly did not reference URP,
    /// so the code could not have switched them on. And the profile wired as URP's global
    /// default is Unity's template one: DepthOfField, MotionBlur, FilmGrain,
    /// LensDistortion, ChromaticAberration, ScreenSpaceLensFlare and a couple of literal
    /// test components. Enabling post without dealing with that would have handed the game
    /// depth of field and film grain rather than the REF-004 look.
    ///
    /// So this builds its own profile at runtime on a high-priority global volume: it sets
    /// the grade we actually want, and explicitly neutralises every template effect that
    /// would otherwise blend in underneath.
    /// </summary>
    public static class AirsidePostProcessing
    {
        public const float VolumePriority = 100f;

        public static void Apply(Camera camera)
        {
            if (camera == null)
                return;

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            data.renderPostProcessing = true;
            // MSAA covers geometric edges; SMAA cleans up what is left after the post
            // stack without TAA's ghosting on fast-moving propellers.
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.dithering = true;

            CreateGlobalVolume();
        }

        private static void CreateGlobalVolume()
        {
            if (GameObject.Find("Airside post volume") != null)
                return;

            var go = new GameObject("Airside post volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VolumePriority;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Airside runtime profile";
            volume.sharedProfile = profile;

            // --- the look ------------------------------------------------------
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            // Neutral keeps the REF-004 palette where it was authored; ACES would
            // pull the Coastal Blue and Safety Yellow off their specified values.
            tonemapping.mode.value = TonemappingMode.Neutral;

            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.55f;      // apron floods and window glow, not haze
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;

            var grade = profile.Add<ColorAdjustments>();
            grade.active = true;
            grade.postExposure.overrideState = true;
            grade.postExposure.value = 0.15f;
            grade.contrast.overrideState = true;
            grade.contrast.value = 8f;
            grade.saturation.overrideState = true;
            grade.saturation.value = 4f;

            var whiteBalance = profile.Add<WhiteBalance>();
            whiteBalance.active = true;
            whiteBalance.temperature.overrideState = true;
            whiteBalance.temperature.value = 6f;   // warm Australian daylight

            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.5f;

            NeutraliseTemplateEffects(profile);
        }

        /// <summary>
        /// The template default profile sits underneath this one at priority 0 and its
        /// overrides would still blend in. A higher-priority volume only wins on the
        /// parameters it overrides, so each unwanted effect is explicitly overridden to
        /// its no-op value rather than merely left out.
        /// </summary>
        private static void NeutraliseTemplateEffects(VolumeProfile profile)
        {
            var depthOfField = profile.Add<DepthOfField>();
            depthOfField.active = true;
            depthOfField.mode.overrideState = true;
            depthOfField.mode.value = DepthOfFieldMode.Off;

            var motionBlur = profile.Add<MotionBlur>();
            motionBlur.active = true;
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = 0f;

            var filmGrain = profile.Add<FilmGrain>();
            filmGrain.active = true;
            filmGrain.intensity.overrideState = true;
            filmGrain.intensity.value = 0f;

            var lensDistortion = profile.Add<LensDistortion>();
            lensDistortion.active = true;
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = 0f;

            var chromaticAberration = profile.Add<ChromaticAberration>();
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = 0f;

            var panini = profile.Add<PaniniProjection>();
            panini.active = true;
            panini.distance.overrideState = true;
            panini.distance.value = 0f;
        }
    }
}
