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
        public const int VSyncCount = 1;
        public const int AnisoLevel = 8;
        public const int HighAdditionalLights = 12;
        public const int MediumAdditionalLights = 4;
        public const float HighShadowDistance = 140f;
        public const float MediumShadowDistance = 55f;
        public const int HighShadowCascades = 4;
        public const int MediumShadowCascades = 2;

        public static Ladder Current { get; private set; } = Ladder.High;

        public static int MsaaSamples => Current == Ladder.High ? HighMsaa : MediumMsaa;

        public static bool UseTerminalProbe => Current == Ladder.High;

        public static void Apply(Camera camera)
        {
            Current = ChooseLadder();
            QualitySettings.vSyncCount = VSyncCount;
            QualitySettings.antiAliasing = MsaaSamples;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.asyncUploadTimeSlice = 4;
            QualitySettings.asyncUploadBufferSize = Mathf.Max(QualitySettings.asyncUploadBufferSize, 32);
            Application.targetFrameRate = -1;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
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
