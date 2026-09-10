using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Presentation-only flight geometry. Simulation timing stays authoritative —
    /// these curves only decide where the model sits inside a phase.
    ///
    /// Two rules drive the shapes here:
    ///
    /// 1. Every phase is built from a speed profile, never a smoothstep on position.
    ///    Smoothstep starts and ends at zero velocity, which reads as the aircraft
    ///    stopping dead at rotation, at the flare and at the runway threshold.
    /// 2. Distances are sized against the phase durations in
    ///    <see cref="AircraftOperation"/> so that an aircraft in the air moves at
    ///    roughly ten times taxi speed. The previous distances made a landing
    ///    rollout slower than a taxi, which is why nothing read as an aircraft.
    /// </summary>
    public static class AirsideFlightPath
    {
        /// <summary>Simulated seconds each phase lasts, mirroring AircraftOperation.</summary>
        public static float PhaseSeconds(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Approach => 20f,
            AircraftPhase.Landing => 12f,
            AircraftPhase.TaxiIn => 25f,
            AircraftPhase.AtStand => 45f,
            AircraftPhase.Pushback => 12f,
            AircraftPhase.TaxiOut => 25f,
            AircraftPhase.Takeoff => 15f,
            _ => DepartureFlyOutSeconds
        };

        /// <summary>
        /// Departed has no simulated duration, so presentation gives it one. It matches
        /// AirportSimulation.DepartureResetSeconds, the window before the slot respawns.
        /// </summary>
        public const float DepartureFlyOutSeconds = 6f;

        public const float GroundY = 0.7f;

        // Runway axis landmarks. The taxi network meets the runway at x = -24.
        public const float RunwayEntryX = -24f;
        public const float RotateX = 28f;
        public const float TakeoffEndX = 96f;
        public const float TakeoffEndY = 22f;
        public const float DepartedEndX = 246f;
        public const float DepartedEndY = 62f;

        // Arrivals join far enough out that they fly in from the distance instead of
        // popping into existence just off the runway end.
        public const float ApproachStartX = -430f;
        public const float ApproachStartY = 78f;
        public const float ShortFinalX = -160f;
        public const float ShortFinalY = 12f;
        public const float TouchdownX = -42f;
        public const float TouchdownProgress = 0.8f;

        /// <summary>
        /// Hold short of the runway, on the A1 chord between the runway entry and the
        /// Alpha junction. Taxi-out used to stop on the runway centreline itself, so a
        /// departure waiting for clearance sat in the path of the next landing. The
        /// simulation owns this distance — it is also where an arrival counts as vacated.
        /// </summary>
        public const float HoldShortZ = AirportTaxiNetwork.RunwayHoldingPositionZ;

        public static readonly float HoldShortX = HoldShortXOnChord();

        /// <summary>
        /// Share of the takeoff phase spent turning onto the runway. Sized so the arc is
        /// walked at roughly the taxi speed the aircraft arrives with.
        /// </summary>
        public const float LineupProgress = 0.4f;

        // Line-up turn: a constant-radius arc from the hold-short point onto the
        // centreline. An arc keeps the tangent rotating smoothly, so the nose sweeps
        // through the turn instead of snapping 143 degrees at the phase boundary.
        private static readonly float LineupRadius = HoldShortZ / 1.8f;
        private static readonly float LineupCentreX = HoldShortX + 0.6f * LineupRadius;
        private static readonly float LineupCentreZ = HoldShortZ - 0.8f * LineupRadius;
        private const float LineupStartAngle = 2.2142975f;  // 126.87 degrees
        private const float LineupSweep = 2.4980915f;       // 143.13 degrees

        /// <summary>Where the line-up arc puts the aircraft on the centreline.</summary>
        public static readonly float LineupEndX = LineupCentreX;

        // Roll speeds are chosen so the roll enters at the taxi speed the line-up arc
        // leaves with, then accelerates all the way to the climb. The roll covers far
        // more ground than the arc in less time, so its start ratio has to be small.
        private const float RollStartSpeed = 0.066f;
        private const float RollEndSpeed = 1f;

        // Braking profile for the landing rollout, ending at taxi speed so the handover
        // into TaxiIn does not lurch.
        private const float RolloutStartSpeed = 1f;
        private const float RolloutEndSpeed = 0.15f;

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

        /// <summary>Long straight-in final, descending at near-constant speed.</summary>
        public static Vector3 Approach(float t, float laneOffset)
        {
            var s = DistanceFraction(t, 1.06f, 0.94f);
            return new Vector3(
                Mathf.Lerp(ApproachStartX, ShortFinalX, s),
                Mathf.Lerp(ApproachStartY, ShortFinalY, s),
                Mathf.Lerp(laneOffset * 4f, laneOffset, s));
        }

        /// <summary>
        /// Descent to the flare, touchdown, then a braked rollout to the runway exit.
        /// Horizontal speed carries through touchdown; only the sink rate is arrested,
        /// which is what a flare actually does. The rollout brakes to taxi speed so the
        /// handover into TaxiIn is continuous.
        /// </summary>
        public static Vector3 Landing(float t, float laneOffset)
        {
            var u = Mathf.Clamp01(t);
            if (u < TouchdownProgress)
            {
                var f = u / TouchdownProgress;
                // Vertical speed reaches zero exactly at the wheels-down point.
                var sink = 1f - (1f - f) * (1f - f);
                return new Vector3(
                    Mathf.Lerp(ShortFinalX, TouchdownX, DistanceFraction(f, 1.05f, 0.95f)),
                    Mathf.Lerp(ShortFinalY, GroundY, sink),
                    Mathf.Lerp(laneOffset, 0f, f));
            }

            var r = (u - TouchdownProgress) / (1f - TouchdownProgress);
            var rollout = DistanceFraction(r, RolloutStartSpeed, RolloutEndSpeed);
            return new Vector3(Mathf.Lerp(TouchdownX, RunwayEntryX, rollout), GroundY, 0f);
        }

        /// <summary>
        /// Line-up turn off the hold-short point, then an accelerating roll, rotate and
        /// climb. One continuous speed ramp along the runway axis after the turn, so the
        /// aircraft never slows at rotation.
        /// </summary>
        public static Vector3 Takeoff(float t)
        {
            var u = Mathf.Clamp01(t);
            if (u < LineupProgress)
            {
                // Arc length is linear in angle, so a linear sweep is a constant speed.
                var angle = LineupStartAngle + u / LineupProgress * LineupSweep;
                return new Vector3(
                    LineupCentreX + LineupRadius * Mathf.Cos(angle),
                    GroundY,
                    LineupCentreZ + LineupRadius * Mathf.Sin(angle));
            }

            var r = (u - LineupProgress) / (1f - LineupProgress);
            var x = Mathf.Lerp(LineupEndX, TakeoffEndX, DistanceFraction(r, RollStartSpeed, RollEndSpeed));
            var y = GroundY;
            if (x > RotateX)
            {
                // Climb rate builds from zero at rotate instead of snapping to a slope.
                var climb = Mathf.Clamp01((x - RotateX) / (TakeoffEndX - RotateX));
                y = Mathf.Lerp(GroundY, TakeoffEndY, climb * Mathf.Sqrt(climb));
            }

            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Climb-out after the runway. The aircraft used to teleport to a fixed point and
        /// freeze there in frame; it now keeps flying the departure track until the slot
        /// is recycled.
        /// </summary>
        public static Vector3 Departed(float t)
        {
            return new Vector3(
                Mathf.Lerp(TakeoffEndX, DepartedEndX, DistanceFraction(t, 1f, 1.3f)),
                Mathf.Lerp(TakeoffEndY, DepartedEndY, DistanceFraction(t, 1.2f, 0.9f)),
                0f);
        }

        /// <summary>
        /// Wheel speed relative to taxi speed. Zero once the wheels leave the ground, so
        /// tires stop instead of freewheeling in the air, and the takeoff roll and the
        /// landing rollout spin up and wind down with the aircraft rather than sitting at
        /// one fixed rate for the whole phase.
        /// </summary>
        public static float WheelSpeedFactor(AircraftPhase phase, float progress)
        {
            var t = Mathf.Clamp01(progress);
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                    if (t >= RotateProgress)
                        return 0f;
                    return t < LineupProgress
                        ? 1f
                        : Mathf.Lerp(1f, 6f, (t - LineupProgress) / (RotateProgress - LineupProgress));
                case AircraftPhase.Landing:
                    return t < TouchdownProgress
                        ? 0f
                        : Mathf.Lerp(6f, 0.6f, (t - TouchdownProgress) / (1f - TouchdownProgress));
                case AircraftPhase.Pushback:
                    return 0.4f;
                case AircraftPhase.TaxiIn:
                case AircraftPhase.TaxiOut:
                    return 1f;
                default:
                    return 0f;
            }
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
                    if (t < LineupProgress || x <= RotateX)
                        return 0f;
                    var climb = Mathf.Clamp01((x - RotateX) / (TakeoffEndX - RotateX));
                    return Mathf.Lerp(0f, -10f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, climb * 2.4f)));
                }
                case AircraftPhase.Approach:
                    return Mathf.Lerp(-2.5f, -3.5f, t);
                case AircraftPhase.Landing:
                    return t < TouchdownProgress
                        ? Mathf.Lerp(-2.5f, -4.2f, t / TouchdownProgress)
                        : Mathf.Lerp(-4.2f, 0f, Mathf.SmoothStep(0f, 1f,
                            Mathf.Min(1f, (t - TouchdownProgress) / 0.09f)));
                case AircraftPhase.Departed:
                    return Mathf.Lerp(-9f, -4f, t);
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Hold short sits on the A1 chord from the runway entry (-24, 0) to the Alpha
        /// junction (-12, 9), at the point where it clears the runway edge.
        /// </summary>
        private static float HoldShortXOnChord()
        {
            var entry = AirportTaxiNetwork.RunwayEnd;
            var junction = AirportTaxiNetwork.Junction;
            var span = junction.Z - entry.Z;
            var f = Mathf.Approximately(span, 0f) ? 0f : (HoldShortZ - entry.Z) / span;
            return Mathf.Lerp(entry.X, junction.X, f);
        }

        private static float SolveTakeoffProgressForX(float targetX)
        {
            var lo = LineupProgress;
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
