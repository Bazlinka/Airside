using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirsideFramePacingTests
    {
        [TestCase(60.0, 1)]
        [TestCase(59.94, 1)]
        [TestCase(100.0, 1)]
        [TestCase(119.88, 2)]
        [TestCase(120.0, 2)]
        [TestCase(144.0, 2)]
        [TestCase(240.0, 4)]
        [TestCase(360.0, 4)]
        public void Focused_CapsAtOrAboveSixtyOnVblank(double refreshHz, int expected)
        {
            Assert.That(AirsideFramePacing.VSyncCountFor(refreshHz, AirsideFramePacing.FocusedTargetFps),
                Is.EqualTo(expected));
        }

        [TestCase(60.0, 2)]
        [TestCase(120.0, 4)]
        [TestCase(144.0, 4)]
        public void Background_DropsToAboutThirty(double refreshHz, int expected)
        {
            Assert.That(AirsideFramePacing.VSyncCountFor(refreshHz, AirsideFramePacing.BackgroundTargetFps),
                Is.EqualTo(expected));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void UnknownRefresh_FallsBackToEveryVblank(double refreshHz)
        {
            Assert.That(AirsideFramePacing.VSyncCountFor(refreshHz, AirsideFramePacing.FocusedTargetFps), Is.EqualTo(1));
        }

        [Test]
        public void Uncapped_UsesEveryVblank()
        {
            Assert.That(AirsideFramePacing.VSyncCountFor(120.0, 0), Is.EqualTo(1));
        }

        [Test]
        public void TargetFps_BackgroundThrottlesUnlessSoaking()
        {
            Assert.That(AirsideFramePacing.TargetFps(uncapped: false, focused: true, soak: false), Is.EqualTo(60));
            Assert.That(AirsideFramePacing.TargetFps(uncapped: true, focused: true, soak: false), Is.EqualTo(0));
            Assert.That(AirsideFramePacing.TargetFps(uncapped: true, focused: false, soak: false), Is.EqualTo(30),
                "display-max is a foreground choice; a background window still throttles");
            Assert.That(AirsideFramePacing.TargetFps(uncapped: false, focused: false, soak: true), Is.EqualTo(60),
                "automated soak runs never own focus but must keep capturing at full rate");
        }
    }

    public sealed class MacRenderBudgetTests
    {
        [Test]
        public void DaylightSteady_SkipsOnlyUnmovedTints()
        {
            Assert.That(AirsideRuntimeQuality.DaylightSteady(float.NaN, 0.5f), Is.False, "first pass always applies");
            Assert.That(AirsideRuntimeQuality.DaylightSteady(0.5f, 0.5f), Is.True);
            Assert.That(AirsideRuntimeQuality.DaylightSteady(0.5f, 0.5015f), Is.True);
            Assert.That(AirsideRuntimeQuality.DaylightSteady(0.5f, 0.503f), Is.False, "dusk still re-tints");
            Assert.That(AirsideRuntimeQuality.DaylightSteady(0.5f, 0.497f), Is.False, "dawn still re-tints");
        }

        [Test]
        public void LandingLamps_CastShadowsOnlyAtNight()
        {
            Assert.That(AirsideRuntimeQuality.LandingLampShadows(night: true), Is.EqualTo(LightShadows.Soft));
            Assert.That(AirsideRuntimeQuality.LandingLampShadows(night: false), Is.EqualTo(LightShadows.None));
        }

        [Test]
        public void OptionsMenu_FitsTheFrameRateRow()
        {
            // Nine 46-point rows from y=62, then Back 56 below the last row, 38 tall.
            const float lastRowBottom = 62f + 8f * 46f + 56f + 38f;
            Assert.That(HudLayout.OptionsHeight, Is.GreaterThanOrEqualTo(lastRowBottom));
        }
    }
}
