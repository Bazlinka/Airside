using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 5 spike — runtime global Volume with ACES tonemap,
    /// mild bloom/vignette/film grain, and day-driven color adjustments.
    /// Presentation only; not a full probe bake or authored day profiles.
    /// </summary>
    public sealed class AirsideDayVolume
    {
        private readonly ColorAdjustments _color;
        private readonly Bloom _bloom;
        private readonly Vignette _vignette;
        private readonly FilmGrain _grain;

        private AirsideDayVolume(ColorAdjustments color, Bloom bloom, Vignette vignette, FilmGrain grain)
        {
            _color = color;
            _bloom = bloom;
            _vignette = vignette;
            _grain = grain;
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

            if (!profile.TryGet(out FilmGrain grain))
                grain = profile.Add<FilmGrain>(true);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.7f);

            NeutraliseTemplateEffects(profile);

            // Ensure the main camera actually runs the URP post stack.
            var camera = Camera.main;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    // Nothing was setting camera AA, and the pipeline asset had MSAA off,
                    // so the game shipped with no anti-aliasing at all — on a world made
                    // entirely of hard box edges and thin poles. MSAA now covers geometric
                    // edges; SMAA cleans up what is left after the post stack, without
                    // TAA's ghosting on fast-moving propellers.
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    data.dithering = true;
                }
            }

            return new AirsideDayVolume(color, bloom, vignette, grain);
        }

        /// <summary>
        /// Unity's template profile (<c>Assets/Settings/DefaultVolumeProfile.asset</c>) is
        /// still wired as URP's global default, and it overrides DepthOfField, MotionBlur,
        /// LensDistortion, ChromaticAberration, ScreenSpaceLensFlare and PaniniProjection —
        /// alongside literal CopyPasteTestComponent1/2/3 and TestVolume.
        ///
        /// Those were harmless while post-processing was off. Switching the post stack on
        /// made them render. A higher-priority volume only wins on parameters it actually
        /// overrides, so each one is pinned to its no-op value here rather than merely left
        /// out. Film grain is deliberately absent: that one is this volume's own, set above
        /// and driven per-frame by <see cref="Apply"/>.
        ///
        /// Replacing that asset outright would be cleaner and would let this go away.
        /// </summary>
        private static void NeutraliseTemplateEffects(VolumeProfile profile)
        {
            if (profile == null)
                return;

            if (!profile.TryGet(out DepthOfField depthOfField))
                depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.active = true;
            depthOfField.mode.Override(DepthOfFieldMode.Off);

            if (!profile.TryGet(out MotionBlur motionBlur))
                motionBlur = profile.Add<MotionBlur>(true);
            motionBlur.active = true;
            motionBlur.intensity.Override(0f);

            if (!profile.TryGet(out LensDistortion lensDistortion))
                lensDistortion = profile.Add<LensDistortion>(true);
            lensDistortion.active = true;
            lensDistortion.intensity.Override(0f);

            if (!profile.TryGet(out ChromaticAberration chromaticAberration))
                chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(0f);

            if (!profile.TryGet(out PaniniProjection panini))
                panini = profile.Add<PaniniProjection>(true);
            panini.active = true;
            panini.distance.Override(0f);

            if (!profile.TryGet(out ScreenSpaceLensFlare lensFlare))
                lensFlare = profile.Add<ScreenSpaceLensFlare>(true);
            lensFlare.active = true;
            lensFlare.intensity.Override(0f);
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

            _bloom.intensity.Override(Mathf.Lerp(0.42f, 0.14f, daylight));
            _vignette.intensity.Override(Mathf.Lerp(0.28f, 0.1f, daylight));
            // Night film grain for regional dusk grit; nearly off in bright day.
            // Cap bloom so night never reintroduces the soft/smeary template look.
            _grain.intensity.Override(Mathf.Lerp(0.22f, 0.03f, daylight));
            _grain.response.Override(Mathf.Lerp(0.8f, 0.55f, daylight));
        }
    }
}
