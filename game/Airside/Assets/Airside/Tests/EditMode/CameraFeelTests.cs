using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Headless locks for overview camera zoom and drag-pan feel.
    /// </summary>
    public sealed class CameraFeelTests
    {
        [Test]
        public void ScrollZoom_OneNotchMovesAUsefulFraction()
        {
            // ~120 is one macOS mouse-wheel notch. The old 0.001 rate only moved ~11 %;
            // players needed dozens of notches to leave the 2.4 km overview.
            var factor = AirsideCameraFeel.ZoomFactorForScroll(120f);
            Assert.That(factor, Is.LessThan(0.8f));
            Assert.That(factor, Is.GreaterThan(0.65f));
            Assert.That(AirsideCameraFeel.ZoomFactorForScroll(-120f), Is.EqualTo(1f / factor).Within(0.001f));
        }

        [Test]
        public void ScrollZoom_TrackpadCanQueueMoreThanOneDoubling()
        {
            var queued = AirsideCameraFeel.QueueScrollZoom(0f, 120f);
            queued = AirsideCameraFeel.QueueScrollZoom(queued, 120f);
            queued = AirsideCameraFeel.QueueScrollZoom(queued, 120f);
            Assert.That(queued, Is.LessThan(-0.8f));
            Assert.That(Math.Abs(queued), Is.LessThanOrEqualTo(AirsideCameraFeel.MaxZoomPendingLog));
        }

        [Test]
        public void DragPan_ScalesWithDistance()
        {
            Assert.That(AirsideCameraFeel.PanMetresPerPixel(AirsideBareField.OverviewDistance),
                Is.GreaterThan(AirsideCameraFeel.PanMetresPerPixel(80f) * 10f));
            Assert.That(AirsideCameraFeel.PanMetresPerPixel(1000f),
                Is.EqualTo(1000f * AirsideCameraFeel.PanMetresPerPixelAtUnitDistance).Within(0.0001f));
        }
    }
}
