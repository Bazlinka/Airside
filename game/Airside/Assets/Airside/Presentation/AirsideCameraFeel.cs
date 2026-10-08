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
        /// airfield in empty ocean. It was 3.8 km, which a drag hit as an invisible wall
        /// (the pan clamped to nothing until you zoomed out); 12 km keeps drags free.
        /// </summary>
        public const float MaxPanRadiusMetres = 12000f;

        /// <summary>Beyond the classic zoom limit the free camera may pan this fraction of its distance from the overview.</summary>
        public const float FarPanFractionOfDistance = 0.9f;

        /// <summary>The pan reach for a camera <paramref name="distance"/> metres out: the classic radius until the view is wide enough to need more.</summary>
        public static float PanRadius(float distance) =>
            Math.Max(MaxPanRadiusMetres, distance * FarPanFractionOfDistance);

        /// <summary>
        /// Near clip plane: 0.3 m close in, growing with distance. The old 0.3 m held to 3 km, which left the default
        /// 2.4 km overview at a 1 : 100,000 near/far ratio and the coplanar ground layers (markings, roads, aprons)
        /// z-fighting and shimmering as the camera moved. Nothing is nearer than the ground the camera sits above, so
        /// 0.2 % of the orbit distance is safe (4.8 m at the overview); past 3 km the original steeper ramp takes over.
        /// </summary>
        public static float NearClip(float distance) =>
            Math.Min(300f, Math.Max(0.3f, Math.Max(distance * 0.002f, (distance - 3000f) * 0.006f)));

        /// <summary>Far clip plane: the 30 km the game has always drawn, or 2.6 x the camera distance when that is more.</summary>
        public static float FarClip(float distance, float baseFarClip) =>
            Math.Max(baseFarClip, distance * 2.6f);

        /// <summary>
        /// The surroundings fade starts at this many metres when <see cref="HorizonScale"/> is 1
        /// (the same value as the far surroundings material). Kept here so the scale can hold that
        /// ring outside the view without pulling in the shader types.
        /// </summary>
        public const float HorizonFadeStartMetres = 25500f;

        /// <summary>Orbit distance at which the classic horizon fade would start cutting a ring through the view.</summary>
        public const float ClassicZoomForHorizonMetres = 4500f;

        /// <summary>
        /// How far the camera-centred horizon haze is pushed. Up to the classic zoom it stays just
        /// inside the 30 km clip. Further out that sphere crosses the city as a ring, so the fade
        /// is held past the far clip and the thinned weather fog is the only haze.
        /// </summary>
        public static float HorizonScale(float distance, float baseFarClip)
        {
            var far = FarClip(distance, baseFarClip);
            var withClip = far / Math.Max(1f, baseFarClip);
            if (distance <= ClassicZoomForHorizonMetres)
                return withClip;
            var clearOfView = far / HorizonFadeStartMetres * 1.05f;
            return Math.Max(withClip, clearOfView);
        }

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
        /// Where a ray meets a horizontal ground plane. A hit further than
        /// <paramref name="maxRayMetres"/> (default <paramref name="farMetres"/>) is treated as a
        /// miss. Rays that miss (sky / horizon) fall back to <paramref name="farMetres"/> along
        /// the flattened direction, matching <see cref="FieldMiniMap.GroundPoint"/>.
        /// </summary>
        public static void GroundHit(
            float originX, float originY, float originZ,
            float dirX, float dirY, float dirZ,
            float groundY, float farMetres,
            out float hitX, out float hitZ, float maxRayMetres = 0f)
        {
            var rayLimit = maxRayMetres > 0f ? maxRayMetres : farMetres;
            if (dirY < -0.0001f)
            {
                var distance = (groundY - originY) / dirY;
                if (distance >= 0f && distance <= rayLimit)
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
            out float clampedX, out float clampedZ, float radiusMetres = MaxPanRadiusMetres,
            float keepMetres = 0f)
        {
            var dx = centerX - overviewX;
            var dz = centerZ - overviewZ;
            var radiusSq = dx * dx + dz * dz;
            // keepMetres lets a view that zoomed in on a far point stay there. The leash still
            // stops a pan or a zoom-out from walking further from the overview than allowed.
            var allowed = radiusMetres > keepMetres ? radiusMetres : keepMetres;
            var maxSq = allowed * allowed;
            if (radiusSq <= maxSq || radiusSq < 0.0001f)
            {
                clampedX = centerX;
                clampedZ = centerZ;
                return;
            }

            var scale = allowed / (float)Math.Sqrt(radiusSq);
            clampedX = overviewX + dx * scale;
            clampedZ = overviewZ + dz * scale;
        }

        // --- Follow camera (ADR 0189) ---------------------------------------------------------------
        //
        // The follow camera used to low-pass its centre with Lerp(centre, target, 1 - exp(-dt * rate)). A first-order
        // filter always trails a moving target by speed / rate, so the aircraft slid across the frame at speed, and a
        // sudden turn of the look-ahead point swung the view. It now follows with a critically damped spring that is
        // handed the target's velocity: no lag once settled, no overshoot, and the same result at any frame rate.

        /// <summary>
        /// One step of a critically damped spring tracking a moving target. <paramref name="smoothTime"/> is roughly the
        /// time to settle; <paramref name="targetVelocity"/> is fed forward so a target moving steadily is followed with
        /// no lag. <paramref name="velocity"/> is the follower's own velocity, kept between calls.
        /// </summary>
        public static float SmoothFollow(float position, float target, float targetVelocity, ref float velocity,
            float smoothTime, float deltaTime)
        {
            if (deltaTime <= 0f)
                return position;
            smoothTime = Math.Max(0.0001f, smoothTime);
            var omega = 2f / smoothTime;
            var x = omega * deltaTime;
            var damp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            // Solve in the target's frame, where a steadily moving target is at rest.
            var relative = position - target;
            var relativeVelocity = velocity - targetVelocity;
            var temp = (relativeVelocity + omega * relative) * deltaTime;
            velocity = targetVelocity + (relativeVelocity - omega * temp) * damp;
            return target + targetVelocity * deltaTime + (relative + temp) * damp;
        }

        /// <summary>
        /// Turn an angle (degrees) toward another along the short way: an exponential ease with a ceiling on the turn
        /// rate, so a sharp change of heading swings the view at a natural pace instead of whipping it.
        /// </summary>
        public static float TurnToward(float currentDegrees, float targetDegrees, float rate, float maxDegreesPerSecond,
            float deltaTime)
        {
            if (deltaTime <= 0f)
                return currentDegrees;
            var delta = DeltaAngle(currentDegrees, targetDegrees);
            var step = delta * (1f - (float)Math.Exp(-deltaTime * rate));
            var cap = maxDegreesPerSecond * deltaTime;
            step = Clamp(step, -cap, cap);
            return currentDegrees + step;
        }

        /// <summary>The signed short way from one angle to another, in (-180, 180].</summary>
        public static float DeltaAngle(float fromDegrees, float toDegrees)
        {
            var d = (toDegrees - fromDegrees) % 360f;
            if (d > 180f)
                d -= 360f;
            else if (d <= -180f)
                d += 360f;
            return d;
        }

        /// <summary>
        /// How much of the phase's look-ahead to use at this ground speed: a parked or crawling aircraft is framed about
        /// where it is, a moving one is led. 0.35 at rest, full from 8 m/s.
        /// </summary>
        public static float LookAheadSpeedFactor(float speedMetresPerSecond) =>
            0.35f + 0.65f * SmoothStep(Clamp(speedMetresPerSecond / 8f, 0f, 1f));

        /// <summary>A little extra field of view (degrees) at speed: takeoff roll and climb-out feel faster.</summary>
        public static float SpeedFovBoost(float speedMetresPerSecond) =>
            3f * SmoothStep(Clamp((speedMetresPerSecond - 25f) / 55f, 0f, 1f));

        /// <summary>
        /// Scale on the follow smoothing time just after follow starts, easing from <see cref="FollowStartSlowdown"/> times
        /// slower to 1 over <see cref="FollowStartSeconds"/>, so pressing Follow glides in instead of snapping.
        /// </summary>
        public static float FollowStartSmoothScale(float secondsSinceFollowStarted)
        {
            var t = Clamp(secondsSinceFollowStarted / FollowStartSeconds, 0f, 1f);
            return 1f + (FollowStartSlowdown - 1f) * (1f - SmoothStep(t));
        }

        public const float FollowStartSeconds = 1.4f;
        public const float FollowStartSlowdown = 3f;

        /// <summary>Follow smoothing time (seconds) for a phase of flight, by whether it is fast, cruising or ground handling.</summary>
        public static float FollowSmoothTime(bool fast, bool onGround) => fast ? 0.34f : onGround ? 0.55f : 0.42f;

        /// <summary>Vertical follow is slower still: a climb-out or flare should not bob the frame.</summary>
        public const float VerticalSmoothScale = 2.2f;

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
