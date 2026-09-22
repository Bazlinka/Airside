using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Airside.Presentation
{
    /// <summary>
    /// P2 graphics ladder plus startup pacing. High matches the documented PC look
    /// (4× MSAA, SMAA high, four cascades, two probes). Medium keeps the same grade
    /// with cheaper MSAA, two apron-centred cascades and one probe.
    /// </summary>
    public static class AirsideRuntimeQuality
    {
        public enum Ladder
        {
            Medium = 0,
            High = 1
        }

        public const int HighMsaa = 4;
        public const int MediumMsaa = 2;
        // At a 4K-class backing surface, 4x MSAA allocates several very large HDR/depth
        // buffers for little visible gain because the camera already uses high-quality SMAA.
        // Keep the high world/lighting ladder, but cap only MSAA above this pixel budget.
        public const long HighMsaaPixelBudget = 5_000_000;
        public const int VSyncCount = 1;
        public const int AnisoLevel = 8;
        // URP's per-object additional-lights cap: the max real-time Point/Spot lights that
        // can affect any ONE renderer/mesh at once, chosen fresh each frame from whichever
        // are nearest. Apron floods (10), runway edge/threshold/PAPI/ALS lights (hundreds
        // along a ~3.9km strip), stand markers and landside streetlights all compete for
        // this same budget on any ground mesh they're all near — with the old cap of 12
        // (4 on Medium), a combined ground/apron mesh could easily lose its apron floods to
        // nearer or more numerous runway lights, which reads as "the apron floods don't
        // light anything up" even though the lights themselves are correctly placed and lit.
        // Raised well clear of that starvation point; still far short of a real cost concern
        // for a field this size. Unverified without a Unity look at the actual apron.
        public const int HighAdditionalLights = 24;
        public const int MediumAdditionalLights = 12;
        public const float HighShadowDistance = 140f;
        public const float MediumShadowDistance = 55f;
        public const int HighShadowCascades = 4;
        public const int MediumShadowCascades = 2;
        public const int HighEdgeLightStep = 10;
        public const int MediumEdgeLightStep = 16;
        public const int HighRainDrops = 28;
        public const int MediumRainDrops = 16;
        public const int HighRainFallback = 48;
        public const int MediumRainFallback = 32;
        public const int HighFilletLights = 3;
        public const int MediumFilletLights = 1;
        public const int HighLightingFixtureStep = 12;
        public const int MediumLightingFixtureStep = 16;
        public const int HighBirdCount = 28;
        public const int MediumBirdCount = 12;

        public static Ladder Current { get; private set; } = Ladder.High;

        public static int MsaaSamples => MsaaForPixels(Current, Screen.width, Screen.height);

        /// <summary>
        /// Chooses the multi-sample count independently from the visual ladder.  A capable
        /// Mac driving a Retina/4K display should keep High's lights, shadows and detail, but
        /// avoid multiplying all of its HDR targets by four when SMAA already cleans edges.
        /// </summary>
        public static int MsaaForPixels(Ladder ladder, int width, int height)
        {
            if (ladder != Ladder.High)
                return MediumMsaa;
            var pixels = (long)Mathf.Max(0, width) * Mathf.Max(0, height);
            return pixels > HighMsaaPixelBudget ? MediumMsaa : HighMsaa;
        }

        public static bool UseTerminalProbe => Current == Ladder.High;

        public static int EdgeLightStep => Current == Ladder.High ? HighEdgeLightStep : MediumEdgeLightStep;

        public static int RainDropCount(bool hasKit) => Current == Ladder.High
            ? (hasKit ? HighRainDrops : HighRainFallback)
            : (hasKit ? MediumRainDrops : MediumRainFallback);

        public static int ApronFloodCount(int highCount) =>
            Current == Ladder.High ? highCount : Mathf.Min(8, highCount);

        public static int LandsideLightCount(int highCount) =>
            Current == Ladder.High ? highCount : Mathf.Max(1, highCount / 2);

        public static int ThresholdLightCount(int highCount) =>
            Current == Ladder.High ? highCount : Mathf.Min(8, highCount);

        public static int FilletLightCount =>
            Current == Ladder.High ? HighFilletLights : MediumFilletLights;

        public static int LightingFixtureStep =>
            Current == Ladder.High ? HighLightingFixtureStep : MediumLightingFixtureStep;

        public static int BirdCount =>
            Current == Ladder.High ? HighBirdCount : MediumBirdCount;

        public static bool PlaceFenceRails => Current == Ladder.High;

        public static bool WindowPointLights => Current == Ladder.High;

        public static int PanePointLights => Current == Ladder.High ? 4 : 0;

        /// <summary>Daylight change below which static light tints are not rewritten.</summary>
        public const float DaylightTintEpsilon = 0.002f;

        /// <summary>
        /// True when a daylight-only tint pass last applied at <paramref name="applied"/> would
        /// write the same values again. NaN (never applied, or reset) is never steady.
        /// </summary>
        public static bool DaylightSteady(float applied, float daylight) =>
            !float.IsNaN(applied) && Mathf.Abs(daylight - applied) < DaylightTintEpsilon;

        /// <summary>
        /// Landing lamps cast soft shadows only after dark (ADR 0101). Each shadowed spot is
        /// another shadow-caster pass per frame; in daylight the sun's shadow hides it anyway.
        /// </summary>
        public static LightShadows LandingLampShadows(bool night) =>
            night ? LightShadows.Soft : LightShadows.None;

        /// <summary>False in the Editor, where the pipeline asset is a tracked project file.</summary>
        public static bool WritesPipelineAsset => !Application.isEditor;

        public static void Apply(Camera camera)
        {
            Current = ChooseLadder();
            QualitySettings.vSyncCount = VSyncCount;
            // ADR 0101: cap ProMotion/120 Hz+ at 60 and throttle background windows.
            AirsideFramePacing.Apply(AirsideSettings.Current.UncappedFrameRate, soak: false);
            QualitySettings.antiAliasing = MsaaSamples;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.asyncUploadTimeSlice = 4;
            QualitySettings.asyncUploadBufferSize = Mathf.Max(QualitySettings.asyncUploadBufferSize, 32);
            Application.targetFrameRate = -1;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            // Writing to the pipeline asset in the Editor edits the committed asset itself:
            // a Play on a Medium-ladder machine left 2x MSAA and a 55 m shadow distance in
            // PC_RPAsset.asset, ready to be committed by accident. The player writes it; the
            // Editor keeps the authored settings.
            if (WritesPipelineAsset && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = MsaaSamples;
                urp.supportsDynamicBatching = true;
                urp.shadowDistance = Current == Ladder.High ? HighShadowDistance : MediumShadowDistance;
                urp.shadowCascadeCount = Current == Ladder.High ? HighShadowCascades : MediumShadowCascades;
                urp.maxAdditionalLightsCount = Current == Ladder.High
                    ? HighAdditionalLights
                    : MediumAdditionalLights;
            }

            if (camera == null)
                return;

            camera.allowMSAA = true;
            camera.allowHDR = true;
            camera.useOcclusionCulling = true;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null)
                return;

            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
        }

        public static Ladder ChooseLadder()
        {
            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2048)
                return Ladder.Medium;
            if (SystemInfo.processorCount > 0 && SystemInfo.processorCount <= 4
                && SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 8192)
                return Ladder.Medium;
            return Ladder.High;
        }

        public static int ProbeBand(float daylight, float wetness)
        {
            if (wetness > 0.2f)
                return 3;
            if (daylight < 0.22f)
                return 0;
            if (daylight < 0.55f)
                return 1;
            return 2;
        }

        public static void AfterWorldBuilt()
        {
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
        }

        public static void StripVisualCollider(GameObject go)
        {
            if (go == null)
                return;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        public static void EnableInstancing(Material material)
        {
            if (material != null)
                material.enableInstancing = true;
        }
    }
}
