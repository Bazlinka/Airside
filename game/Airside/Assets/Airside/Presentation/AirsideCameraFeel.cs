using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Zoom, orbit and drag-pan feel for the overview camera. No UnityEngine types so the
    /// headless harness can lock the math without an editor. The MonoBehaviour
    /// <see cref="AirsideCameraController"/> applies these values to the live camera.
    /// </summary>
    public static class AirsideCameraFeel
    {
        // Scroll magnitude is not portable: Windows reports 120 units per wheel notch,
        // macOS reports single digits for the same notch (the route map's IMGUI path sees
        // ~3), and trackpads stream continuous values. Assuming 120 made one real notch on
        // a Mac worth well under 1 % — zoom that never arrives. Scroll is normalised to
        // notches first, and the rate below is per *notch*, not per raw unit.
        /// <summary>Distance change per wheel notch: e^-0.5, about 39 % closer in, 65 % further out.</summary>
        public const float ZoomLogPerNotch = 0.5f;

        /// <summary>At or above this the sample is the Windows-style 120-per-notch convention.</summary>
        public const float NormalisedWheelThreshold = 40f;
        public const float NormalisedWheelUnitsPerNotch = 120f;

        /// <summary>Most notches one frame may queue, so a trackpad flick cannot teleport.</summary>
        public const float MaxNotchesPerFrame = 3f;

        public const float MaxZoomPendingLog = 2.4f;
        public const float ZoomEaseRate = 22f;
        public const float OrbitYawDegreesPerPixel = 0.26f;
        public const float OrbitPitchDegreesPerPixel = 0.2f;
        /// <summary>Fallback drag pan scale when a ground ray misses (horizon / sky).</summary>
        public const float PanMetresPerPixelAtUnitDistance = 0.0028f;

        /// <summary>
        /// How far past the overview centre the free camera may pan. Wide enough for the
        /// whole YPAD circuit plus approaches, tight enough that you cannot lose the
        /// airfield in empty ocean.
        /// </summary>
        public const float MaxPanRadiusMetres = 3800f;

        /// <summary>
        /// One scroll sample as wheel notches, whatever units the platform reports.
        /// Large samples are the normalised 120-per-notch convention; small ones are
        /// already notch-sized (macOS wheel, trackpad steps).
        /// </summary>
        public static float ScrollNotches(float scrollUnits)
        {
            var magnitude = Math.Abs(scrollUnits);
            if (magnitude < 0.0001f)
                return 0f;
            var notches = magnitude >= NormalisedWheelThreshold
                ? magnitude / NormalisedWheelUnitsPerNotch
                : magnitude;
            if (notches > MaxNotchesPerFrame)
                notches = MaxNotchesPerFrame;
            return scrollUnits < 0f ? -notches : notches;
        }

        /// <summary>
        /// How much one scroll sample grows the pending log-zoom queue. Positive scroll
        /// zooms in (distance shrinks). The queue is capped so a long flick eases in over
        /// several frames instead of jumping.
        /// </summary>
        public static float QueueScrollZoom(float pendingLog, float scrollUnits) =>
            Clamp(pendingLog - ScrollNotches(scrollUnits) * ZoomLogPerNotch,
                -MaxZoomPendingLog, MaxZoomPendingLog);

        /// <summary>Distance multiplier for a fully applied scroll sample (before easing).</summary>
        public static float ZoomFactorForScroll(float scrollUnits) =>
            (float)Math.Exp(-ScrollNotches(scrollUnits) * ZoomLogPerNotch);

        /// <summary>Ground metres moved per drag pixel at the given orbit distance.</summary>
        public static float PanMetresPerPixel(float distance) =>
            Math.Max(0f, distance) * PanMetresPerPixelAtUnitDistance;

        /// <summary>
        /// Orbit pose: camera sits <paramref name="distance"/> metres back from the centre
        /// along the look direction defined by pitch and yaw.
        /// </summary>
        public static void OrbitPose(
            float centerX, float centerY, float centerZ,
            float pitchDegrees, float yawDegrees, float distance,
            out float camX, out float camY, out float camZ,
            out float forwardX, out float forwardY, out float forwardZ,
            out float rightX, out float rightY, out float rightZ,
            out float upX, out float upY, out float upZ)
        {
            var pitch = pitchDegrees * (float)(Math.PI / 180.0);
            var yaw = yawDegrees * (float)(Math.PI / 180.0);
            var cosPitch = (float)Math.Cos(pitch);
            var sinPitch = (float)Math.Sin(pitch);
            var cosYaw = (float)Math.Cos(yaw);
            var sinYaw = (float)Math.Sin(yaw);

            // Unity yaw: +Y rotation. Pitch: +X rotation. Combined as Euler(pitch, yaw, 0).
            forwardX = sinYaw * cosPitch;
            forwardY = -sinPitch;
            forwardZ = cosYaw * cosPitch;
            rightX = cosYaw;
            rightY = 0f;
            rightZ = -sinYaw;
            // Unity is left-handed: up = forward × right.
            upX = forwardY * rightZ - forwardZ * rightY;
            upY = forwardZ * rightX - forwardX * rightZ;
            upZ = forwardX * rightY - forwardY * rightX;
            var upLen = (float)Math.Sqrt(upX * upX + upY * upY + upZ * upZ);
            if (upLen > 0.0001f)
            {
                upX /= upLen;
                upY /= upLen;
                upZ /= upLen;
            }

            camX = centerX - forwardX * distance;
            camY = centerY - forwardY * distance;
            camZ = centerZ - forwardZ * distance;
        }

        /// <summary>
        /// Perspective ray through a screen point (Input System coords: origin bottom-left).
        /// <paramref name="fovDegrees"/> is Unity's vertical field of view.
        /// </summary>
        public static void ScreenRay(
            float screenX, float screenY, float screenWidth, float screenHeight,
            float fovDegrees,
            float forwardX, float forwardY, float forwardZ,
            float rightX, float rightY, float rightZ,
            float upX, float upY, float upZ,
            out float dirX, out float dirY, out float dirZ)
        {
            var width = Math.Max(1f, screenWidth);
            var height = Math.Max(1f, screenHeight);
            var ndcX = screenX / width * 2f - 1f;
            var ndcY = screenY / height * 2f - 1f;
            var aspect = width / height;
            var tanHalf = (float)Math.Tan(fovDegrees * 0.5 * Math.PI / 180.0);
            dirX = forwardX + rightX * (ndcX * aspect * tanHalf) + upX * (ndcY * tanHalf);
            dirY = forwardY + rightY * (ndcX * aspect * tanHalf) + upY * (ndcY * tanHalf);
            dirZ = forwardZ + rightZ * (ndcX * aspect * tanHalf) + upZ * (ndcY * tanHalf);
            var len = (float)Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ);
            if (len > 0.0001f)
            {
                dirX /= len;
                dirY /= len;
                dirZ /= len;
            }
        }

        /// <summary>
        /// Where a ray meets a horizontal ground plane. Rays that miss (sky / horizon)
        /// fall back to <paramref name="farMetres"/> along the flattened direction, matching
        /// <see cref="FieldMiniMap.GroundPoint"/>.
        /// </summary>
        public static void GroundHit(
            float originX, float originY, float originZ,
            float dirX, float dirY, float dirZ,
            float groundY, float farMetres,
            out float hitX, out float hitZ)
        {
            if (dirY < -0.0001f)
            {
                var distance = (groundY - originY) / dirY;
                if (distance >= 0f && distance <= farMetres)
                {
                    hitX = originX + dirX * distance;
                    hitZ = originZ + dirZ * distance;
                    return;
                }
            }

            var flatX = dirX;
            var flatZ = dirZ;
            var flatLen = (float)Math.Sqrt(flatX * flatX + flatZ * flatZ);
            if (flatLen > 0.0001f)
            {
                flatX /= flatLen;
                flatZ /= flatLen;
            }
            else
            {
                flatX = 0f;
                flatZ = 0f;
            }

            hitX = originX + flatX * farMetres;
            hitZ = originZ + flatZ * farMetres;
        }

        /// <summary>
        /// Keep the ground point under the cursor stable while orbit distance changes by
        /// <paramref name="distanceFactor"/>. Zooming in (factor &lt; 1) pulls the centre
        /// toward the pivot; zooming out pushes it away — same idea as the destinations
        /// map's <c>ZoomAtGui</c>.
        /// </summary>
        public static void ZoomTowardPivot(
            float centerX, float centerZ,
            float pivotX, float pivotZ,
            float distanceFactor,
            out float newCenterX, out float newCenterZ)
        {
            var t = 1f - distanceFactor;
            newCenterX = centerX + (pivotX - centerX) * t;
            newCenterZ = centerZ + (pivotZ - centerZ) * t;
        }

        /// <summary>
        /// Soft pan limit: keep the orbit centre within <see cref="MaxPanRadiusMetres"/> of
        /// the overview focus so free navigation cannot lose the airfield.
        /// </summary>
        public static void ClampPanCentre(
            float overviewX, float overviewZ,
            float centerX, float centerZ,
            out float clampedX, out float clampedZ)
        {
            var dx = centerX - overviewX;
            var dz = centerZ - overviewZ;
            var radiusSq = dx * dx + dz * dz;
            var maxSq = MaxPanRadiusMetres * MaxPanRadiusMetres;
            if (radiusSq <= maxSq || radiusSq < 0.0001f)
            {
                clampedX = centerX;
                clampedZ = centerZ;
                return;
            }

            var scale = MaxPanRadiusMetres / (float)Math.Sqrt(radiusSq);
            clampedX = overviewX + dx * scale;
            clampedZ = overviewZ + dz * scale;
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
