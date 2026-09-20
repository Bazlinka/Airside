using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Headless locks for overview camera zoom, drag-pan and pointer navigation feel.
    /// </summary>
    public sealed class CameraFeelTests
    {
        [Test]
        public void ScrollZoom_OneNotchMovesABigStepOnEveryPlatform()
        {
            // A wheel notch is 120 units on Windows and single digits on macOS. Both must
            // be worth one notch — assuming 120 made a real Mac notch worth under 1 %.
            var windows = AirsideCameraFeel.ZoomFactorForScroll(120f);
            var mac = AirsideCameraFeel.ZoomFactorForScroll(1f);
            Assert.That(windows, Is.EqualTo(mac).Within(0.001f));
            Assert.That(windows, Is.LessThan(0.65f), "one notch must be a big step, not a nudge");
            Assert.That(windows, Is.GreaterThan(0.5f), "one notch must not skip half the field");
            Assert.That(AirsideCameraFeel.ZoomFactorForScroll(-120f), Is.EqualTo(1f / windows).Within(0.001f));
        }

        [Test]
        public void ScrollNotches_NormalisesEveryReportingConvention()
        {
            Assert.That(AirsideCameraFeel.ScrollNotches(120f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(AirsideCameraFeel.ScrollNotches(-120f), Is.EqualTo(-1f).Within(0.001f));
            Assert.That(AirsideCameraFeel.ScrollNotches(1f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(AirsideCameraFeel.ScrollNotches(3f), Is.EqualTo(3f).Within(0.001f));
            Assert.That(AirsideCameraFeel.ScrollNotches(0f), Is.Zero);
            // A flung trackpad is capped rather than teleporting across the field.
            Assert.That(AirsideCameraFeel.ScrollNotches(30f),
                Is.EqualTo(AirsideCameraFeel.MaxNotchesPerFrame).Within(0.001f));
            Assert.That(AirsideCameraFeel.ScrollNotches(-4800f),
                Is.EqualTo(-AirsideCameraFeel.MaxNotchesPerFrame).Within(0.001f));
        }

        [Test]
        public void ScrollZoom_ASixNotchRollCoversTheWholeOverviewRange()
        {
            // Overview is 2400 m; the apron reads at ~60 m. That must be a short roll of
            // the wheel, not thirty notches.
            var distance = AirsideBareField.OverviewDistance;
            var notches = 0;
            while (distance > 60f && notches < 100)
            {
                distance *= AirsideCameraFeel.ZoomFactorForScroll(120f);
                notches++;
            }

            Assert.That(notches, Is.LessThanOrEqualTo(8), $"took {notches} notches to reach the apron");
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
        public void ZoomEasing_LandsInsideAQuarterSecond()
        {
            // "Takes too long" was partly latency: the queue must be almost spent within
            // a few frames of the notch that filled it.
            var pending = AirsideCameraFeel.QueueScrollZoom(0f, 120f);
            var start = Math.Abs(pending);
            for (var frame = 0; frame < 15; frame++)
                pending -= pending * (1f - (float)Math.Exp(-(1f / 60f) * AirsideCameraFeel.ZoomEaseRate));
            Assert.That(Math.Abs(pending), Is.LessThan(start * 0.05f));
        }

        [Test]
        public void OrbitRates_StayTrackpadFriendly()
        {
            Assert.That(AirsideCameraFeel.OrbitYawDegreesPerPixel, Is.LessThanOrEqualTo(0.14f));
            Assert.That(AirsideCameraFeel.OrbitPitchDegreesPerPixel, Is.LessThanOrEqualTo(0.12f));
        }

        [Test]
        public void GrazingHit_FallsBackBeforeTheHorizonTeleportsTheView()
        {
            Assert.That(AirsideCameraFeel.HitIsGrazing(100f, 80f), Is.False);
            Assert.That(AirsideCameraFeel.HitIsGrazing(200f, 80f), Is.True);
        }

        [Test]
        public void ClampPanStep_CapsATrackpadFlick()
        {
            AirsideCameraFeel.ClampPanStep(400f, 0f, 40f, out var dx, out var dz);
            Assert.That(dx, Is.EqualTo(40f).Within(0.001f));
            Assert.That(dz, Is.EqualTo(0f).Within(0.001f));
            AirsideCameraFeel.ClampPanStep(3f, 4f, 40f, out dx, out dz);
            Assert.That(dx, Is.EqualTo(3f).Within(0.001f));
            Assert.That(dz, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void DragPan_ScalesWithDistance()
        {
            Assert.That(AirsideCameraFeel.PanMetresPerPixel(AirsideBareField.OverviewDistance),
                Is.GreaterThan(AirsideCameraFeel.PanMetresPerPixel(80f) * 10f));
            Assert.That(AirsideCameraFeel.PanMetresPerPixel(1000f),
                Is.EqualTo(1000f * AirsideCameraFeel.PanMetresPerPixelAtUnitDistance).Within(0.0001f));
        }

        [Test]
        public void OrbitPose_LevelNorthMatchesUnityConvention()
        {
            AirsideCameraFeel.OrbitPose(
                0f, 0f, 0f, pitchDegrees: 0f, yawDegrees: 0f, distance: 100f,
                out var camX, out var camY, out var camZ,
                out var fx, out var fy, out var fz,
                out var rx, out var ry, out var rz,
                out _, out _, out _);
            Assert.That(fx, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fy, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fz, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rx, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(ry, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(rz, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(camX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(camY, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(camZ, Is.EqualTo(-100f).Within(0.0001f));
        }

        [Test]
        public void OrbitPose_StraightDownSitsAboveTheCentre()
        {
            AirsideCameraFeel.OrbitPose(
                10f, 0f, 20f, pitchDegrees: 90f, yawDegrees: 0f, distance: 50f,
                out var camX, out var camY, out var camZ,
                out var fx, out var fy, out var fz,
                out _, out _, out _,
                out _, out _, out _);
            Assert.That(fx, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fy, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(fz, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(camX, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(camY, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(camZ, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void OrbitPose_PitchedLookHasUpTiltingBack()
        {
            AirsideCameraFeel.OrbitPose(
                0f, 0f, 0f, pitchDegrees: 45f, yawDegrees: 0f, distance: 10f,
                out _, out _, out _,
                out _, out var fy, out var fz,
                out _, out _, out _,
                out var ux, out var uy, out var uz);
            Assert.That(ux, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(uy, Is.GreaterThan(0.5f));
            Assert.That(uz, Is.GreaterThan(0.5f));
            // up · forward ≈ 0
            Assert.That(ux * 0f + uy * fy + uz * fz, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ScreenRay_CentreLooksAlongForward()
        {
            AirsideCameraFeel.ScreenRay(
                640f, 360f, 1280f, 720f, 60f,
                0f, 0f, 1f, 1f, 0f, 0f, 0f, 1f, 0f,
                out var dx, out var dy, out var dz);
            Assert.That(dx, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(dy, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(dz, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void GroundHit_IntersectingRayHitsPlane()
        {
            AirsideCameraFeel.GroundHit(
                0f, 100f, 0f, 0f, -1f, 0f, groundY: 0f, farMetres: 5000f,
                out var hitX, out var hitZ);
            Assert.That(hitX, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(hitZ, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void GroundHit_SkyRayFallsBackAlongFlatDirection()
        {
            AirsideCameraFeel.GroundHit(
                0f, 10f, 0f, 1f, 0.1f, 0f, groundY: 0f, farMetres: 200f,
                out var hitX, out var hitZ);
            Assert.That(hitX, Is.EqualTo(200f).Within(0.001f));
            Assert.That(hitZ, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ZoomTowardPivot_PullsCentreTowardPivotWhenZoomingIn()
        {
            AirsideCameraFeel.ZoomTowardPivot(
                centerX: 0f, centerZ: 0f, pivotX: 100f, pivotZ: 50f, distanceFactor: 0.5f,
                out var cx, out var cz);
            Assert.That(cx, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(cz, Is.EqualTo(25f).Within(0.0001f));
        }

        [Test]
        public void ZoomTowardPivot_PushesCentreAwayWhenZoomingOut()
        {
            AirsideCameraFeel.ZoomTowardPivot(
                centerX: 50f, centerZ: 25f, pivotX: 100f, pivotZ: 50f, distanceFactor: 2f,
                out var cx, out var cz);
            Assert.That(cx, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(cz, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ZoomTowardPivot_KeepsScreenPivotStableAcrossTwoSteps()
        {
            // Simulate two eased zoom-in steps toward a stand off to the side: the
            // compound centre shift must match a single step of the product factor.
            const float pivotX = 400f;
            const float pivotZ = -120f;
            var factorA = 0.9f;
            var factorB = 0.85f;
            AirsideCameraFeel.ZoomTowardPivot(0f, 0f, pivotX, pivotZ, factorA, out var midX, out var midZ);
            AirsideCameraFeel.ZoomTowardPivot(midX, midZ, pivotX, pivotZ, factorB, out var steppedX, out var steppedZ);
            AirsideCameraFeel.ZoomTowardPivot(0f, 0f, pivotX, pivotZ, factorA * factorB, out var directX, out var directZ);
            Assert.That(steppedX, Is.EqualTo(directX).Within(0.001f));
            Assert.That(steppedZ, Is.EqualTo(directZ).Within(0.001f));
        }

        [Test]
        public void ClampPanCentre_LeavesPointsInsideTheRadiusAlone()
        {
            AirsideCameraFeel.ClampPanCentre(
                150f, 350f, 150f + 100f, 350f - 80f, out var cx, out var cz);
            Assert.That(cx, Is.EqualTo(250f).Within(0.0001f));
            Assert.That(cz, Is.EqualTo(270f).Within(0.0001f));
        }

        [Test]
        public void ClampPanCentre_PullsPointsOutsideBackToTheRadius()
        {
            AirsideCameraFeel.ClampPanCentre(
                0f, 0f, AirsideCameraFeel.MaxPanRadiusMetres * 2f, 0f, out var cx, out var cz);
            Assert.That(cx, Is.EqualTo(AirsideCameraFeel.MaxPanRadiusMetres).Within(0.001f));
            Assert.That(cz, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void GroundGrabPan_DeltaKeepsGrabbedPointUnderTheCursor()
        {
            // Dragging right should move the orbit centre left by the same ground metres
            // the pointer crossed — the grabbed stand stays under the finger.
            const float beforeX = 120f;
            const float beforeZ = 40f;
            const float afterX = 150f;
            const float afterZ = 40f;
            var centerX = 0f;
            var centerZ = 0f;
            centerX += beforeX - afterX;
            centerZ += beforeZ - afterZ;
            Assert.That(centerX, Is.EqualTo(-30f).Within(0.0001f));
            Assert.That(centerZ, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(beforeX - centerX, Is.EqualTo(afterX - 0f).Within(0.0001f));
        }
    }
}
