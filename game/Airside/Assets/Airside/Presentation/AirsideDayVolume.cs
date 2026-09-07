using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// Decision 0025 item 5 — runtime global Volume with ACES tonemap,
    /// bloom/vignette/film grain, shadows-midtones-highlights day profiles,
    /// white-balance / split-toning dusk warmth, and weather gloom.
    /// Presentation only; not a full authored day-profile asset.
    /// </summary>
    public sealed class AirsideDayVolume
    {
        private readonly ColorAdjustments _color;
        private readonly Bloom _bloom;
        private readonly Vignette _vignette;
        private readonly FilmGrain _grain;
        private readonly ShadowsMidtonesHighlights _tonal;
        private readonly WhiteBalance _whiteBalance;
        private readonly SplitToning _splitToning;

        private AirsideDayVolume(
            ColorAdjustments color,
            Bloom bloom,
            Vignette vignette,
            FilmGrain grain,
            ShadowsMidtonesHighlights tonal,
            WhiteBalance whiteBalance,
            SplitToning splitToning)
        {
            _color = color;
            _bloom = bloom;
            _vignette = vignette;
            _grain = grain;
            _tonal = tonal;
            _whiteBalance = whiteBalance;
            _splitToning = splitToning;
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
            bloom.threshold.Override(0.92f);
            bloom.scatter.Override(0.52f);
            bloom.clamp.Override(24f);

            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(new Color(0.05f, 0.07f, 0.12f));
            vignette.smoothness.Override(0.48f);

            if (!profile.TryGet(out FilmGrain grain))
                grain = profile.Add<FilmGrain>(true);
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.7f);

            if (!profile.TryGet(out ShadowsMidtonesHighlights tonal))
                tonal = profile.Add<ShadowsMidtonesHighlights>(true);
            tonal.active = true;

            if (!profile.TryGet(out WhiteBalance whiteBalance))
                whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.active = true;

            if (!profile.TryGet(out SplitToning splitToning))
                splitToning = profile.Add<SplitToning>(true);
            splitToning.active = true;

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

            return new AirsideDayVolume(color, bloom, vignette, grain, tonal, whiteBalance, splitToning);
        }

        /// <summary>
        /// Unity's template profile (<c>Assets/Settings/DefaultVolumeProfile.asset</c>) is
        /// still wired as URP's global default. Junk test components and presentation
        /// killers (DoF / motion blur / lens junk) were deactivated in that asset for
        /// 0025 item 5, but this safety net still pins no-ops on our owned volume so a
        /// regenerated template cannot fight dusk profiles.
        ///
        /// A higher-priority volume only wins on parameters it actually overrides.
        /// Film grain is deliberately absent here: that one is this volume's own.
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

            // Template WhiteBalance / SplitToning / LiftGammaGain stay neutral so our
            // owned copies (or this profile's overrides) drive dusk warmth alone.
            if (profile.TryGet(out LiftGammaGain liftGammaGain))
            {
                liftGammaGain.active = true;
                liftGammaGain.lift.Override(new Vector4(1f, 1f, 1f, 0f));
                liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
                liftGammaGain.gain.Override(new Vector4(1f, 1f, 1f, 0f));
            }
        }

        /// <param name="daylight">0 night … 1 noon.</param>
        /// <param name="warm">Dawn/dusk warmth 0…1.</param>
        /// <param name="weatherGloom">Rain/fog/storm cool-down 0…1 (presentation only).</param>
        public void Apply(float daylight, float warm, float weatherGloom = 0f)
        {
            weatherGloom = Mathf.Clamp01(weatherGloom);

            // Day: slight lift; dusk: warmer filter; night: darker exposure + bloom;
            // adverse weather: cooler filter + pulled exposure.
            var exposure = Mathf.Lerp(-0.72f, 0.14f, daylight) + warm * 0.18f - weatherGloom * 0.4f;
            var contrast = Mathf.Lerp(12f, 3.0f, daylight) + weatherGloom * 4.8f;
            var dayFilter = Color.Lerp(Color.white, new Color(1f, 0.74f, 0.52f), warm);
            var nightFilter = new Color(0.62f, 0.7f, 1f);
            var stormFilter = new Color(0.68f, 0.74f, 0.84f);
            var filter = Color.Lerp(
                Color.Lerp(nightFilter, dayFilter, Mathf.Clamp01(daylight + warm * 0.45f)),
                stormFilter,
                weatherGloom);

            _color.postExposure.Override(exposure);
            _color.contrast.Override(contrast);
            _color.colorFilter.Override(filter);
            _color.saturation.Override(Mathf.Lerp(12f, 1.5f, daylight) - weatherGloom * 8f + warm * 2.5f);
            _color.hueShift.Override(Mathf.Lerp(0f, -6f, weatherGloom) + warm * 3f);

            _bloom.intensity.Override(Mathf.Lerp(0.46f, 0.11f, daylight) * (1f - weatherGloom * 0.28f) + warm * 0.06f);
            _bloom.threshold.Override(Mathf.Lerp(0.8f, 0.97f, daylight));
            _vignette.intensity.Override(Mathf.Lerp(0.34f, 0.07f, daylight) + weatherGloom * 0.08f);
            // Night film grain for regional dusk grit; nearly off in bright day.
            _grain.intensity.Override(Mathf.Lerp(0.28f, 0.015f, daylight) + weatherGloom * 0.05f);
            _grain.response.Override(Mathf.Lerp(0.86f, 0.48f, daylight));

            // Lift cool night shadows; warm midtones at golden hour; soft highlight roll-off.
            var shadowTint = Color.Lerp(
                new Color(0.48f, 0.56f, 0.9f),
                Color.Lerp(new Color(0.95f, 0.95f, 1f), new Color(1f, 0.82f, 0.68f), warm),
                daylight);
            shadowTint = Color.Lerp(shadowTint, new Color(0.66f, 0.72f, 0.78f), weatherGloom);
            var midTint = Color.Lerp(
                new Color(0.8f, 0.84f, 1f),
                Color.Lerp(Color.white, new Color(1f, 0.86f, 0.7f), warm * 0.9f),
                daylight);
            var hiTint = Color.Lerp(
                new Color(0.86f, 0.88f, 1f),
                Color.Lerp(Color.white, new Color(1f, 0.93f, 0.82f), warm * 0.55f),
                daylight);

            _tonal.shadows.Override(new Vector4(shadowTint.r, shadowTint.g, shadowTint.b,
                Mathf.Lerp(0.16f, -0.06f, daylight) - weatherGloom * 0.08f));
            _tonal.midtones.Override(new Vector4(midTint.r, midTint.g, midTint.b,
                Mathf.Lerp(-0.06f, 0.04f, daylight) + warm * 0.06f));
            _tonal.highlights.Override(new Vector4(hiTint.r, hiTint.g, hiTint.b,
                Mathf.Lerp(-0.12f, -0.01f, daylight) + warm * 0.02f));
            _tonal.shadowsStart.Override(0f);
            _tonal.shadowsEnd.Override(Mathf.Lerp(0.24f, 0.38f, daylight));
            _tonal.highlightsStart.Override(Mathf.Lerp(0.4f, 0.58f, daylight));
            _tonal.highlightsEnd.Override(1f);

            // Owned dusk white-balance / split-toning (0025 item 5) — keep ranges modest
            // so night blue survives and weather gloom stays cool.
            var temperature = Mathf.Lerp(-8f, 4f, daylight) + warm * 38f - weatherGloom * 14f;
            var tint = warm * 5f - weatherGloom * 3f;
            _whiteBalance.temperature.Override(temperature);
            _whiteBalance.tint.Override(tint);

            var shadows = Color.Lerp(
                new Color(0.45f, 0.55f, 0.85f),
                new Color(0.35f, 0.42f, 0.62f),
                weatherGloom);
            var highlights = Color.Lerp(
                Color.white,
                new Color(1f, 0.72f, 0.48f),
                warm * 0.9f);
            _splitToning.shadows.Override(shadows);
            _splitToning.highlights.Override(highlights);
            _splitToning.balance.Override(Mathf.Lerp(-0.15f, 0.12f, warm) - weatherGloom * 0.1f);

            // Deeper night exposure so flood pools read against the apron (REF-002).
            if (daylight < 0.35f)
                _color.postExposure.Override(exposure - (0.35f - daylight) * 0.35f);
        }
    }
}
