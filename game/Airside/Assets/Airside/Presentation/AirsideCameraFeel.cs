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
        public const float OrbitYawDegreesPerPixel = 0.11f;
        public const float OrbitPitchDegreesPerPixel = 0.09f;
        /// <summary>Fallback drag pan scale when a ground ray misses (horizon / sky).</summary>
        public const float PanMetresPerPixelAtUnitDistance = 0.0016f;
        /// <summary>
        /// A grazing ground hit (far taxiway / horizon) makes one trackpad pixel leap
        /// hundreds of metres. Past this multiple of orbit distance, use distance-scaled
        /// pan instead of a raw grab.
        /// </summary>
        public const float GrazingHitDistanceFactor = 1.55f;
        /// <summary>One pointer sample may not jump more than this fraction of orbit distance.</summary>
        public const float MaxPanFractionOfDistance = 0.22f;

        /// <summary>
        /// How far past the overview centre the free camera may pan. Wide enough for the
        /// whole YPAD circuit plus approaches, tight enough that you cannot lose the
        /// airfield in empty ocean.
        /// </summary>
        public const float MaxPanRadiusMetres = 3800f;

        /// <summary>Beyond the classic zoom limit the free camera may pan this fraction of its distance from the overview.</summary>
        public const float FarPanFractionOfDistance = 0.9f;

        /// <summary>The pan reach for a camera <paramref name="distance"/> metres out: the classic radius until the view is wide enough to need more.</summary>
        public static float PanRadius(float distance) =>
            Math.Max(MaxPanRadiusMetres, distance * FarPanFractionOfDistance);

        /// <summary>
        /// Near clip plane: 0.3 m as always up to 3 km out, then growing with distance so depth precision holds when
        /// zoomed far out (nothing is nearer than the ground and sky the camera sits above).
        /// </summary>
        public static float NearClip(float distance) =>
            Math.Min(300f, Math.Max(0.3f, (distance - 3000f) * 0.006f));

        /// <summary>Far clip plane: the 30 km the game has always drawn, or 2.6 x the camera distance when that is more.</summary>
        public static float FarClip(float distance, float baseFarClip) =>
            Math.Max(baseFarClip, distance * 2.6f);

        /// <summary>How far the horizon haze (fixed distances in the terrain shaders) is pushed out with the far clip.</summary>
        public static float HorizonScale(float distance, float baseFarClip) =>
            FarClip(distance, baseFarClip) / Math.Max(1f, baseFarClip);

        /// <summary>Fog thins past the classic zoom limit so the far view is hazy, not white: density x classic / distance.</summary>
        public static float FogScale(float distance, float classicMaxDistance) =>
            Math.Min(1f, classicMaxDistance / Math.Max(1f, distance));

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

        public static bool HitIsGrazing(float hitDistance, float orbitDistance) =>
            hitDistance > Math.Max(1f, orbitDistance) * GrazingHitDistanceFactor;

        public static float MaxPanStepMetres(float orbitDistance) =>
            Math.Max(8f, Math.Max(0f, orbitDistance) * MaxPanFractionOfDistance);

        public static void ClampPanStep(float dx, float dz, float maxMetres, out float clampedX, out float clampedZ)
        {
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length <= maxMetres || length < 0.0001f)
            {
                clampedX = dx;
                clampedZ = dz;
                return;
            }

            var scale = maxMetres / length;
            clampedX = dx * scale;
            clampedZ = dz * scale;
        }

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
            out float clampedX, out float clampedZ, float radiusMetres = MaxPanRadiusMetres)
        {
            var dx = centerX - overviewX;
            var dz = centerZ - overviewZ;
            var radiusSq = dx * dx + dz * dz;
            var maxSq = radiusMetres * radiusMetres;
            if (radiusSq <= maxSq || radiusSq < 0.0001f)
            {
                clampedX = centerX;
                clampedZ = centerZ;
                return;
            }

            var scale = radiusMetres / (float)Math.Sqrt(radiusSq);
            clampedX = overviewX + dx * scale;
            clampedZ = overviewZ + dz * scale;
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
