using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Real-metre circuit on the Adelaide 05/23 strip. Approach, land, roll to a
    /// stop, take off, climb out until the model is off the field. Taxi geometry
    /// is not used — ground-wait phases sit on the rollout end.
    ///
    /// Simulation timing stays authoritative. These curves only decide where the
    /// model sits inside a phase. Speeds are sized against
    /// <see cref="AirportCircuit"/> durations so a regional turboprop reads as
    /// one on a 3 100 m runway.
    /// </summary>
    public static class AirsideFlightPath
    {
        public static float PhaseSeconds(AircraftPhase phase) =>
            AirportCircuit.DurationSeconds(phase) >= long.MaxValue / 2
                ? DepartureFlyOutSeconds
                : AirportCircuit.DurationSeconds(phase);

        public const float DepartureFlyOutSeconds = 22f;

        public const float GroundY = 0.72f;

        public static float WestThresholdX => -AirsideBareField.RunwayHalfLength;
        public static float EastThresholdX => AirsideBareField.RunwayHalfLength;

        // Long straight-in from west of the field so the arrival is a distant speck.
        public const float ApproachStartX = -4200f;
        public const float ApproachStartY = 155f;
        public const float ShortFinalX = -1850f;
        public const float ShortFinalY = 16f;

        /// <summary>Touchdown 300 m past the west threshold, typical TDZ.</summary>
        public const float TouchdownX = -1250f;
        public const float TouchdownProgress = 0.22f;

        public static bool HasTouchedDown(float landingProgress) =>
            Mathf.Clamp01(landingProgress) >= TouchdownProgress;

        /// <summary>Rollout end / takeoff start — still on the 3 100 m strip.</summary>
        public const float RolloutEndX = -200f;
        public const float RunwayEntryX = RolloutEndX;

        /// <summary>No taxi line-up. Takeoff begins on the centreline.</summary>
        public const float LineupProgress = 0f;
        public const float LineupEndX = RolloutEndX;
        public const float HoldShortX = RolloutEndX;
        public const float HoldShortZ = 0f;

        /// <summary>Rotate after ~900 m of roll, still well short of the east end.</summary>
        public const float RotateX = 700f;
        public const float TakeoffEndX = 1650f;
        public const float TakeoffEndY = 95f;
        public const float DepartedEndX = 4000f;
        public const float DepartedEndY = 280f;

        private const float RollStartSpeed = 0f;
        private const float RollEndSpeed = 1f;
        private const float RolloutStartSpeed = 1f;
        private const float RolloutEndSpeed = 0f;

        public static readonly float RotateProgress = SolveTakeoffProgressForX(RotateX);

        public static float DistanceFraction(float t, float startSpeed, float endSpeed)
        {
            var u = Mathf.Clamp01(t);
            var mean = (startSpeed + endSpeed) * 0.5f;
            if (mean <= 0.0001f)
                return u;
            return Mathf.Clamp01((startSpeed * u + (endSpeed - startSpeed) * u * u * 0.5f) / mean);
        }

        public static float DampFactor(float rate, float deltaTime)
        {
            if (deltaTime <= 0f || rate <= 0f)
                return 0f;
            return 1f - Mathf.Exp(-rate * deltaTime);
        }

        /// <summary>Long straight-in, descending at near-constant speed on a 3° slope.</summary>
        public static Vector3 Approach(float t, float laneOffset)
        {
            var s = DistanceFraction(t, 1.04f, 0.96f);
            return new Vector3(
                Mathf.Lerp(ApproachStartX, ShortFinalX, s),
                Mathf.Lerp(ApproachStartY, ShortFinalY, s),
                Mathf.Lerp(laneOffset * 8f, laneOffset, s));
        }

        /// <summary>
        /// Flare to a soft touchdown, then a long braked rollout that almost stops
        /// on the centreline so takeoff can spool from rest.
        /// </summary>
        public static Vector3 Landing(float t, float laneOffset)
        {
            var u = Mathf.Clamp01(t);
            if (u < TouchdownProgress)
            {
                var f = u / TouchdownProgress;
                var sink = 1f - (1f - f) * (1f - f);
                return new Vector3(
                    Mathf.Lerp(ShortFinalX, TouchdownX, DistanceFraction(f, 1.04f, 0.96f)),
                    Mathf.Lerp(ShortFinalY, GroundY, sink),
                    Mathf.Lerp(laneOffset, 0f, f));
            }

            var r = (u - TouchdownProgress) / (1f - TouchdownProgress);
            var rollout = DistanceFraction(r, RolloutStartSpeed, RolloutEndSpeed);
            return new Vector3(Mathf.Lerp(TouchdownX, RolloutEndX, rollout), GroundY, 0f);
        }

        /// <summary>Hold on the rollout end — taxi is not drawn.</summary>
        public static Vector3 OnRunwayHold(float unused = 0f) =>
            new(RolloutEndX, GroundY, 0f);

        /// <summary>
        /// Accelerating roll from the rollout end, rotate, then a climbing departure
        /// that leaves the east threshold still accelerating.
        /// </summary>
        public static Vector3 Takeoff(float t)
        {
            var u = Mathf.Clamp01(t);
            var x = Mathf.Lerp(RolloutEndX, TakeoffEndX, DistanceFraction(u, RollStartSpeed, RollEndSpeed));
            var y = GroundY;
            if (x > RotateX)
            {
                var climb = Mathf.Clamp01((x - RotateX) / (TakeoffEndX - RotateX));
                y = Mathf.Lerp(GroundY, TakeoffEndY, climb * Mathf.Sqrt(climb));
            }

            return new Vector3(x, y, 0f);
        }

        /// <summary>Keep climbing east until the slot recycles off-field.</summary>
        public static Vector3 Departed(float t)
        {
            return new Vector3(
                Mathf.Lerp(TakeoffEndX, DepartedEndX, DistanceFraction(t, 1f, 1.15f)),
                Mathf.Lerp(TakeoffEndY, DepartedEndY, DistanceFraction(t, 1.1f, 0.95f)),
                0f);
        }

        /// <summary>
        /// Horizontal ground speed along the circuit, metres per simulated second.
        /// Zero when the gear is off the pavement. Used for distance-based tire spin.
        /// </summary>
        public static float GroundSpeedMetresPerSecond(AircraftPhase phase, float progress)
        {
            var t = Mathf.Clamp01(progress);
            var duration = Mathf.Max(0.001f, PhaseSeconds(phase));
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                    if (t >= RotateProgress)
                        return 0f;
                    break;
                case AircraftPhase.Landing:
                    if (t < TouchdownProgress)
                        return 0f;
                    break;
                case AircraftPhase.TaxiIn:
                case AircraftPhase.TaxiOut:
                case AircraftPhase.Pushback:
                case AircraftPhase.AtStand:
                    return 0f;
                default:
                    return 0f;
            }

            const float eps = 0.0025f;
            var a = Mathf.Clamp01(t - eps);
            var b = Mathf.Clamp01(t + eps);
            if (b <= a)
                return 0f;
            Vector3 pa;
            Vector3 pb;
            if (phase == AircraftPhase.Takeoff)
            {
                pa = Takeoff(a);
                pb = Takeoff(b);
            }
            else
            {
                pa = Landing(a);
                pb = Landing(b);
            }

            var dx = pb.x - pa.x;
            var dz = pb.z - pa.z;
            var metres = Mathf.Sqrt(dx * dx + dz * dz);
            return metres / ((b - a) * duration);
        }

        /// <summary>
        /// Tire angular speed in degrees per simulated second from travelled distance
        /// and wheel radius. Stops naturally once <see cref="GroundSpeedMetresPerSecond"/>
        /// is zero (lift-off / flare).
        /// </summary>
        public static float TireAngularDegreesPerSecond(float groundSpeedMps, float radiusMetres)
        {
            if (groundSpeedMps <= 0.001f || radiusMetres <= 0.001f)
                return 0f;
            return (groundSpeedMps / (2f * Mathf.PI * radiusMetres)) * 360f;
        }

        /// <summary>
        /// Legacy relative factor kept for existing tests. Prefer
        /// <see cref="GroundSpeedMetresPerSecond"/> for presentation.
        /// </summary>
        public static float WheelSpeedFactor(AircraftPhase phase, float progress)
        {
            var speed = GroundSpeedMetresPerSecond(phase, progress);
            if (speed <= 0.001f)
                return 0f;
            // Normalize against a brisk rollout (~55 m/s) so older callers stay in range.
            return Mathf.Clamp(speed / 55f * 6f, 0f, 8f);
        }

        public const float ApproachPitchStartDegrees = -2.8f;
        public const float ApproachPitchEndDegrees = -3.4f;
        public const float FlarePitchDegrees = -5.5f;
        /// <summary>Main-gear-first hold just after touchdown (nose still slightly up).</summary>
        public const float TouchdownHoldPitchDegrees = -3.2f;
        public const float RotatePitchDegrees = -12f;
        public const float ClimbPitchDegrees = -10f;
        public const float DepartedPitchEndDegrees = -4.5f;

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
                    // Ease into rotation rather than a sharp pitch snap at RotateX.
                    var ease = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, climb * 1.6f));
                    var rotation = Mathf.Lerp(0f, RotatePitchDegrees, ease);
                    // Settle into climb attitude before crossing into Departed.
                    return Mathf.Lerp(rotation, ClimbPitchDegrees,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, climb)));
                }
                case AircraftPhase.Approach:
                    return Mathf.Lerp(ApproachPitchStartDegrees, ApproachPitchEndDegrees, t);
                case AircraftPhase.Landing:
                {
                    if (t < TouchdownProgress)
                    {
                        var flare = t / TouchdownProgress;
                        // Soft flare: hold approach attitude longer, then deepen.
                        var shaped = flare * flare;
                        return Mathf.Lerp(ApproachPitchEndDegrees, FlarePitchDegrees, shaped);
                    }

                    var since = (t - TouchdownProgress) / (1f - TouchdownProgress);
                    // Main-gear-first: hold a little nose-up, then settle level.
                    if (since < 0.08f)
                        return Mathf.Lerp(FlarePitchDegrees, TouchdownHoldPitchDegrees, since / 0.08f);
                    return Mathf.Lerp(TouchdownHoldPitchDegrees, 0f, Mathf.SmoothStep(0f, 1f,
                        Mathf.Min(1f, (since - 0.08f) / 0.2f)));
                }
                case AircraftPhase.Departed:
                    return Mathf.Lerp(ClimbPitchDegrees, DepartedPitchEndDegrees, t);
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
                if (Takeoff(mid).x < targetX)
                    lo = mid;
                else
                    hi = mid;
            }

            return (lo + hi) * 0.5f;
        }
    }
}
