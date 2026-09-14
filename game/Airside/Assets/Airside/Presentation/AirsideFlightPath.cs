using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Where the aircraft sits on the Adelaide 05/23 strip at any point in a phase.
    ///
    /// Every station, height and speed comes from <see cref="CircuitProfile"/>, which
    /// holds the ATR 42 reference figures and derives the phase durations from them.
    /// This file only turns those into positions: each segment is flown at constant
    /// acceleration between its entry and exit speed, so the instantaneous speed at
    /// any progress is the scheduled one rather than whatever a hand-picked duration
    /// happened to produce.
    ///
    /// The approach is a true 3° glideslope. The flare is a real round-out: it
    /// begins at 30 ft, right as the threshold goes under, and floats 300 m past
    /// the aim point onto the touchdown-zone markings while the sink is arrested
    /// from 584 ft/min to about 60.
    /// </summary>
    public static class AirsideFlightPath
    {
        public static float PhaseSeconds(AircraftPhase phase) =>
            AirportCircuit.DurationSeconds(phase) >= long.MaxValue / 2
                ? DepartureFlyOutSeconds
                : AirportCircuit.DurationSeconds(phase);

        public static float DepartureFlyOutSeconds => CircuitProfile.DepartedSeconds;

        /// <summary>Height of the wheels-on-tarmac plane in world space.</summary>
        public const float GroundY = 0.72f;

        public static float WestThresholdX => -AirsideBareField.RunwayHalfLength;
        public static float EastThresholdX => AirsideBareField.RunwayHalfLength;

        public static float ApproachStartX => CircuitProfile.ApproachStartX;
        public static float ApproachStartY => GroundY + CircuitProfile.ApproachStartHeight;
        public static float ShortFinalX => CircuitProfile.ShortFinalX;
        public static float ShortFinalY => GroundY + CircuitProfile.ShortFinalHeight;

        /// <summary>Where the glideslope meets the ground — aimed at, not landed on.</summary>
        public static float AimPointX => CircuitProfile.AimPointX;

        /// <summary>Where the wheels arrive, having floated past the aim point.</summary>
        public static float TouchdownX => CircuitProfile.TouchdownX;

        public static float FlareStartX => CircuitProfile.FlareStartX;

        /// <summary>Fraction of the landing phase at which the wheels touch.</summary>
        public static float TouchdownProgress => CircuitProfile.TouchdownProgress;

        /// <summary>Fraction of the landing phase at which the round-out begins.</summary>
        public static float FlareProgress => CircuitProfile.FlareProgress;

        public static bool HasTouchedDown(float landingProgress) =>
            Mathf.Clamp01(landingProgress) >= TouchdownProgress;

        public static float RolloutEndX => CircuitProfile.RolloutEndX;
        public static float RunwayEntryX => RolloutEndX;

        /// <summary>No taxi line-up. Takeoff begins on the centreline.</summary>
        public const float LineupProgress = 0f;
        /// <summary>Runway 05 takeoff start at the threshold (real YPAD layout).</summary>
        public static float TakeoffStartX => CircuitProfile.TakeoffStartX;
        public static float LineupEndX => TakeoffStartX;
        public static float HoldShortX => RolloutEndX;
        public const float HoldShortZ = 0f;

        public static float RotateX => CircuitProfile.RotateX;
        public static float TakeoffEndX => CircuitProfile.TakeoffEndX;
        public static float TakeoffEndY => GroundY + CircuitProfile.TakeoffEndHeight;
        public static float DepartedEndX => CircuitProfile.DepartedEndX;
        public static float DepartedEndY => GroundY + CircuitProfile.DepartedEndHeight;

        /// <summary>Fraction of the takeoff phase at which the nose comes up.</summary>
        public static float RotateProgress => CircuitProfile.RotateProgress;

        private static float Mps(float knots) => CircuitProfile.Knots(knots);

        /// <summary>
        /// Fraction of a segment's distance covered by progress <paramref name="t"/>
        /// when accelerating uniformly from <paramref name="startSpeed"/> to
        /// <paramref name="endSpeed"/>. Speeds may be in any consistent unit — only
        /// their ratio matters.
        /// </summary>
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

        /// <summary>Progress within a sub-segment of a phase, 0..1.</summary>
        private static float Local(float t, float from, float to) =>
            to <= from ? 0f : Mathf.Clamp01((t - from) / (to - from));

        /// <summary>Long straight-in on the 3° glideslope, slowing from 120 kt to Vapp.</summary>
        public static Vector3 Approach(float t, float laneOffset)
        {
            var s = DistanceFraction(t, Mps(CircuitProfile.ApproachEntryKnots), Mps(CircuitProfile.ApproachKnots));
            var x = Mathf.Lerp(ApproachStartX, ShortFinalX, s);
            return new Vector3(
                x,
                GroundY + CircuitProfile.GlideslopeHeight(x),
                Mathf.Lerp(laneOffset * 8f, laneOffset, s));
        }

        /// <summary>
        /// Final glide on the slope, a real flare, then the braked rollout.
        ///
        /// The flare is a cubic Hermite on height: it leaves the glideslope at exactly
        /// the descent rate it was flying, and arrives at the tarmac at
        /// <see cref="CircuitProfile.TouchdownSinkMetresPerSecond"/>. Both height and
        /// sink fall monotonically through it, so the aircraft never sinks faster
        /// mid-flare than it did on the slope.
        /// </summary>
        public static Vector3 Landing(float t, float laneOffset)
        {
            var u = Mathf.Clamp01(t);
            var vApp = Mps(CircuitProfile.ApproachKnots);
            var vTouchdown = Mps(CircuitProfile.TouchdownKnots);

            if (u < FlareProgress)
            {
                var f = Local(u, 0f, FlareProgress);
                var x = Mathf.Lerp(ShortFinalX, FlareStartX, f);
                return new Vector3(
                    x,
                    GroundY + CircuitProfile.GlideslopeHeight(x),
                    Mathf.Lerp(laneOffset, laneOffset * 0.35f, f));
            }

            if (u < TouchdownProgress)
            {
                var f = Local(u, FlareProgress, TouchdownProgress);
                var x = Mathf.Lerp(FlareStartX, TouchdownX, DistanceFraction(f, vApp, vTouchdown));
                return new Vector3(
                    x,
                    GroundY + FlareHeight(f),
                    Mathf.Lerp(laneOffset * 0.35f, 0f, f));
            }

            var r = Local(u, TouchdownProgress, 1f);
            var rollout = DistanceFraction(r, vTouchdown, 0f);
            return new Vector3(Mathf.Lerp(TouchdownX, RolloutEndX, rollout), GroundY, 0f);
        }

        /// <summary>
        /// Height above the tarmac through the flare, as a function of flare progress.
        /// Cubic Hermite matching the glideslope descent rate on entry and the
        /// touchdown sink rate on arrival.
        /// </summary>
        public static float FlareHeight(float flareProgress)
        {
            var f = Mathf.Clamp01(flareProgress);
            var seconds = CircuitProfile.FlareExactSeconds;
            var h0 = CircuitProfile.FlareHeightMetres;
            // Tangents are dh/df, so the per-second rates are scaled by the segment length.
            var m0 = -CircuitProfile.ApproachDescentRate * seconds;
            var m1 = -CircuitProfile.TouchdownSinkMetresPerSecond * seconds;

            var f2 = f * f;
            var f3 = f2 * f;
            var h00 = 2f * f3 - 3f * f2 + 1f;
            var h10 = f3 - 2f * f2 + f;
            var h11 = f3 - f2;
            return Mathf.Max(0f, h0 * h00 + m0 * h10 + m1 * h11);
        }

        /// <summary>Hold on the rollout end — taxi is not drawn.</summary>
        public static Vector3 OnRunwayHold(float unused = 0f) =>
            new(RolloutEndX, GroundY, 0f);

        /// <summary>
        /// Ground roll to Vr, then rotate and climb away. The roll is a genuine
        /// 900 m acceleration to 100 kt rather than the 179 kt it used to reach.
        /// </summary>
        /// <summary>
        /// The demo circuit takes off from where it stopped; fleet departures from the
        /// real 05 threshold. Same distances either way — only the whole curve moves.
        /// </summary>
        public static float CircuitTakeoffOffsetX => RolloutEndX - TakeoffStartX;

        public static Vector3 Takeoff(float t) => Takeoff(t, CircuitTakeoffOffsetX);

        public static Vector3 Takeoff(float t, float offsetX) => TakeoffAtThreshold(t) + new Vector3(offsetX, 0f, 0f);

        private static Vector3 TakeoffAtThreshold(float t)
        {
            var u = Mathf.Clamp01(t);
            var vRotate = Mps(CircuitProfile.RotateKnots);
            var vClimb = Mps(CircuitProfile.InitialClimbKnots);

            if (u < RotateProgress)
            {
                var f = Local(u, 0f, RotateProgress);
                var x = Mathf.Lerp(TakeoffStartX, RotateX, DistanceFraction(f, 0f, vRotate));
                return new Vector3(x, GroundY, 0f);
            }

            var c = Local(u, RotateProgress, 1f);
            var cx = Mathf.Lerp(RotateX, TakeoffEndX, DistanceFraction(c, vRotate, vClimb));
            // Rotation takes a moment, so the climb eases in rather than snapping to
            // the full gradient the instant the nose comes up. The exponent sets how
            // hot the climb is by the end of the phase: 1.5 would finish at 1 790
            // ft/min, far too much for an ATR, while 1.15 arrives at about 1 370 —
            // just above the nominal 1 300 and still starting from a flat rotation.
            var climbShape = Mathf.Pow(c, 1.15f);
            return new Vector3(cx, GroundY + CircuitProfile.TakeoffEndHeight * climbShape, 0f);
        }

        /// <summary>Accelerating climb-out to 170 kt until the slot recycles off-field.</summary>
        public static Vector3 Departed(float t) => Departed(t, CircuitTakeoffOffsetX);

        public static Vector3 Departed(float t, float offsetX) => DepartedAtThreshold(t) + new Vector3(offsetX, 0f, 0f);

        private static Vector3 DepartedAtThreshold(float t)
        {
            var s = DistanceFraction(t, Mps(CircuitProfile.InitialClimbKnots), Mps(CircuitProfile.ClimbOutKnots));
            return new Vector3(
                Mathf.Lerp(TakeoffEndX, DepartedEndX, s),
                Mathf.Lerp(TakeoffEndY, DepartedEndY, s),
                0f);
        }

        /// <summary>
        /// Scheduled airspeed at this point in the circuit, in knots. Delegates to
        /// <see cref="CircuitProfile"/> so the figure the HUD shows is the one the
        /// headless tests check.
        /// </summary>
        public static float AirspeedKnots(AircraftPhase phase, float progress) =>
            CircuitProfile.AirspeedKnots(phase, progress);

        /// <summary>
        /// Speed over the tarmac, metres per second. Zero whenever the wheels are not
        /// on it, so tyre spin stops at lift-off and does not start before touchdown.
        /// </summary>
        public static float GroundSpeedMetresPerSecond(AircraftPhase phase, float progress)
        {
            var t = Mathf.Clamp01(progress);
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
                default:
                    return 0f;
            }

            return Mps(AirspeedKnots(phase, t));
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

        // Body attitudes, degrees. Negative is nose-up in this codebase.
        public const float ApproachPitchStartDegrees = -2.4f;
        public const float ApproachPitchEndDegrees = -3.0f;

        /// <summary>Nose-up through the round-out; an ATR touches down around 6° body angle.</summary>
        public const float FlarePitchDegrees = -6.5f;

        /// <summary>Main-gear-first hold just after touchdown (nose still slightly up).</summary>
        public const float TouchdownHoldPitchDegrees = -4.0f;

        /// <summary>Body angle once rotated — an ATR lifts off around 8-9°.</summary>
        public const float RotatePitchDegrees = -9f;

        /// <summary>Established initial climb attitude.</summary>
        public const float ClimbPitchDegrees = -7.5f;

        /// <summary>Lowered as the climb-out accelerates toward 170 kt.</summary>
        public const float DepartedPitchEndDegrees = -4f;

        public static float PitchDegrees(AircraftPhase phase, float progress)
        {
            var t = Mathf.Clamp01(progress);
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                {
                    if (t <= RotateProgress)
                        return 0f;
                    var climb = Local(t, RotateProgress, 1f);
                    // Ease into rotation rather than a sharp pitch snap at Vr.
                    var ease = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, climb * 2.4f));
                    var rotation = Mathf.Lerp(0f, RotatePitchDegrees, ease);
                    // Settle into climb attitude before crossing into Departed.
                    return Mathf.Lerp(rotation, ClimbPitchDegrees,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, climb)));
                }
                case AircraftPhase.Approach:
                    return Mathf.Lerp(ApproachPitchStartDegrees, ApproachPitchEndDegrees, t);
                case AircraftPhase.Landing:
                {
                    if (t < FlareProgress)
                        return ApproachPitchEndDegrees;
                    if (t < TouchdownProgress)
                    {
                        // Progressive round-out: the nose comes up as the sink is arrested.
                        var flare = Local(t, FlareProgress, TouchdownProgress);
                        return Mathf.Lerp(ApproachPitchEndDegrees, FlarePitchDegrees,
                            Mathf.SmoothStep(0f, 1f, flare));
                    }

                    var since = Local(t, TouchdownProgress, 1f);
                    // Main-gear-first: hold a little nose-up, then let the nose down.
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
    }
}
