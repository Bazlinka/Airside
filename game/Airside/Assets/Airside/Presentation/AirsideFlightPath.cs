using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Presentation-only flight geometry for the air phases. Simulation timing stays
    /// authoritative — these curves only decide where the model sits inside a phase.
    ///
    /// Every phase is built from a speed profile rather than a smoothstep on position.
    /// Smoothstep starts and ends at zero velocity, which read as the aircraft stopping
    /// dead at rotation, at the flare and at the runway threshold.
    /// </summary>
    public static class AirsideFlightPath
    {
        // Runway axis landmarks shared by takeoff, landing and the taxi network.
        public const float TaxiExitX = -24f;
        public const float RotateX = 10f;
        public const float TakeoffEndX = 52f;
        public const float TakeoffEndY = 12f;
        public const float GroundY = 0.7f;
        public const float DepartedEndX = 240f;
        public const float DepartedEndY = 58f;
        public const float TouchdownProgress = 0.28f;

        // Takeoff roll accelerates from a slow lineup to rotation speed.
        private const float TakeoffStartSpeed = 0.18f;
        private const float TakeoffEndSpeed = 1.85f;

        /// <summary>Phase progress at which the takeoff geometry leaves the ground.</summary>
        public static readonly float RotateProgress = SolveTakeoffProgressForX(RotateX);

        /// <summary>
        /// Distance fraction travelled by an object whose speed ramps linearly from
        /// <paramref name="startSpeed"/> to <paramref name="endSpeed"/> over the phase.
        /// Only the ratio matters. The result is 0 at t=0 and 1 at t=1, and its
        /// derivative is never zero, so position never stalls mid-phase.
        /// </summary>
        public static float DistanceFraction(float t, float startSpeed, float endSpeed)
        {
            var u = Mathf.Clamp01(t);
            var mean = (startSpeed + endSpeed) * 0.5f;
            if (mean <= 0.0001f)
                return u;
            return Mathf.Clamp01((startSpeed * u + (endSpeed - startSpeed) * u * u * 0.5f) / mean);
        }

        /// <summary>
        /// Frame-rate independent smoothing weight. <c>Lerp(a, b, dt * rate)</c> changes
        /// behaviour with frame rate and overshoots past 1 on a long frame.
        /// </summary>
        public static float DampFactor(float rate, float deltaTime)
        {
            if (deltaTime <= 0f || rate <= 0f)
                return 0f;
            return 1f - Mathf.Exp(-rate * deltaTime);
        }

        /// <summary>
        /// Final approach: steady descent at near-constant speed onto the flare gate.
        /// </summary>
        public static Vector3 Approach(float t, float laneOffset)
        {
            var s = DistanceFraction(t, 1.08f, 0.92f);
            return new Vector3(
                Mathf.Lerp(-72f, -50f, s),
                Mathf.Lerp(7.5f, 1.7f, s),
                Mathf.Lerp(laneOffset, laneOffset * 0.35f, s));
        }

        /// <summary>
        /// Flare then braked rollout to the west taxi exit. Horizontal speed carries
        /// through touchdown; only the sink rate is arrested, which is what a flare
        /// actually does. The rollout brakes toward taxi speed instead of stopping dead.
        /// </summary>
        public static Vector3 Landing(float t, float laneOffset)
        {
            var u = Mathf.Clamp01(t);
            var z = laneOffset * 0.2f;
            if (u < TouchdownProgress)
            {
                var f = u / TouchdownProgress;
                // Vertical speed reaches zero exactly at the wheels-down point.
                var sink = 1f - (1f - f) * (1f - f);
                return new Vector3(
                    Mathf.Lerp(-50f, -46f, DistanceFraction(f, 1.05f, 0.95f)),
                    Mathf.Lerp(1.7f, GroundY, sink),
                    Mathf.Lerp(z, z * 0.5f, f));
            }

            var r = (u - TouchdownProgress) / (1f - TouchdownProgress);
            var rollout = DistanceFraction(r, 1f, 0.12f);
            return new Vector3(
                Mathf.Lerp(-46f, TaxiExitX, rollout),
                GroundY,
                Mathf.Lerp(z * 0.5f, 0f, rollout));
        }

        /// <summary>
        /// Lineup, accelerating ground roll, rotate, then climb-out — one continuous
        /// speed ramp along the runway axis, so the aircraft never slows at rotation.
        /// </summary>
        public static Vector3 Takeoff(float t)
        {
            var s = DistanceFraction(t, TakeoffStartSpeed, TakeoffEndSpeed);
            var x = Mathf.Lerp(TaxiExitX, TakeoffEndX, s);

            // Lineup bend north, away from the off-field exit south of Alpha. Fades out
            // well before rotation so the climb tracks the centreline.
            var bend = Mathf.Clamp01(1f - s / 0.09f);
            var z = 0.85f * bend * bend;

            var y = GroundY;
            if (x > RotateX)
            {
                // Climb rate builds from zero at rotate instead of snapping to a slope.
                var climb = Mathf.Clamp01((x - RotateX) / (TakeoffEndX - RotateX));
                y = Mathf.Lerp(GroundY, TakeoffEndY, climb * Mathf.Sqrt(climb));
            }

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Climb-out after the runway. The previous build teleported the model to a
        /// fixed point and froze it there, which read as the aircraft vanishing in
        /// mid-air. It now keeps flying the departure track and leaves under power.
        /// </summary>
        public static Vector3 Departed(float t)
        {
            return new Vector3(
                Mathf.Lerp(TakeoffEndX, DepartedEndX, DistanceFraction(t, 1f, 1.6f)),
                Mathf.Lerp(TakeoffEndY, DepartedEndY, DistanceFraction(t, 1.25f, 0.7f)),
                0f);
        }

        /// <summary>Nose attitude for a phase. Negative X euler is nose-up.</summary>
        public static float PitchDegrees(AircraftPhase phase, float progress)
        {
            var t = Mathf.Clamp01(progress);
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                {
                    var x = Takeoff(t).x;
                    if (x <= RotateX)
                        return 0f;
                    var climb = Mathf.Clamp01((x - RotateX) / (TakeoffEndX - RotateX));
                    return Mathf.Lerp(0f, -10f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, climb * 2.2f)));
                }
                case AircraftPhase.Approach:
                    return Mathf.Lerp(-2.5f, -3.5f, t);
                case AircraftPhase.Landing:
                    return t < TouchdownProgress
                        ? Mathf.Lerp(-2.5f, -4.2f, t / TouchdownProgress)
                        : Mathf.Lerp(-4.2f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (t - TouchdownProgress) / 0.22f)));
                case AircraftPhase.Departed:
                    return Mathf.Lerp(-9f, -4f, t);
                default:
                    return 0f;
            }
        }

        private static float SolveTakeoffProgressForX(float targetX)
        {
            var lo = 0f;
            var hi = 1f;
            for (var i = 0; i < 40; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Mathf.Lerp(TaxiExitX, TakeoffEndX, DistanceFraction(mid, TakeoffStartSpeed, TakeoffEndSpeed)) < targetX)
                    lo = mid;
                else
                    hi = mid;
            }

            return (lo + hi) * 0.5f;
        }
    }
}
