using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0151 — how a constant-speed turboprop and a turbofan actually behave, as pure
    /// functions for the presentation passes to drive.
    ///
    /// The earlier model (ADR 0148) treated shaft speed as the throttle: a parked engine turned
    /// at 420 rpm and takeoff power at 1400. A real turboprop does the opposite. Its governor
    /// holds the propeller at an almost constant speed and power is carried by <em>blade pitch</em>,
    /// so the visible difference between taxi and takeoff is the angle of the blades and the
    /// density of the disc, not how fast it turns. The blades are also feathered — edged into the
    /// airflow — whenever the engine is shut down, which is the single most recognisable thing
    /// about a parked turboprop and the thing the game was missing.
    ///
    /// Everything here is presentation only: it never feeds simulation timing or outcomes.
    /// </summary>
    public static class AirsidePropellerDynamics
    {
        // ---------------------------------------------------------------- shaft speed (Np)

        /// <summary>100% Np in rpm. Between a PW127's 1200 and a CT7's 1394 — one visual value.</summary>
        public const float GovernedRpm = 1200f;

        /// <summary>Starter motoring speed before the turbine lights: slow enough to count blades.</summary>
        public const float MotoringRpm = 120f;

        /// <summary>
        /// Engine fraction (<see cref="EngineState.Left"/>) at which the turbine lights and the
        /// propeller accelerates from motoring to ground idle. Below it the starter is turning it.
        /// </summary>
        public const float LightOffFraction = 0.42f;

        /// <summary>Fraction by which light-off is complete and the governor has the propeller.</summary>
        public const float GovernorCaptureFraction = 0.78f;

        // Np as a fraction of governed speed. A turboprop idles well down the range and is
        // governed from taxi onward; cruise is deliberately reduced (a Q400's 850 rpm setting).
        public const float GroundIdleNp = 0.63f;
        public const float TaxiNp = 0.67f;
        public const float TakeoffNp = 1f;
        public const float ClimbNp = 0.97f;
        public const float CruiseNp = 0.86f;
        public const float ApproachNp = 0.92f;

        /// <summary>Governed shaft speed for a phase, as a fraction of <see cref="GovernedRpm"/>.</summary>
        public static float NpFractionForPhase(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Takeoff => TakeoffNp,
            AircraftPhase.Departed => ClimbNp,
            AircraftPhase.Approach or AircraftPhase.Landing => ApproachNp,
            AircraftPhase.GoAround => TakeoffNp,
            AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => TaxiNp,
            AircraftPhase.AtStand => GroundIdleNp,
            _ => CruiseNp
        };

        /// <summary>
        /// Shaft speed in rpm for one engine at <paramref name="engineFraction"/> of its start
        /// sequence. Below light-off the starter motors it; through light-off it accelerates to
        /// ground idle; past governor capture the governor holds the phase speed.
        /// </summary>
        public static float EngineRpm(float engineFraction, float operatingNpFraction)
        {
            var running = Mathf.Clamp01(engineFraction);
            if (running <= 0.001f)
                return 0f;
            if (running < LightOffFraction)
                return MotoringRpm * (running / LightOffFraction);

            var idleRpm = GovernedRpm * GroundIdleNp;
            if (running < GovernorCaptureFraction)
            {
                // Light-off: the turbine takes over from the starter and the propeller accelerates
                // hard. Eased so the acceleration builds rather than stepping.
                var t = (running - LightOffFraction) / (GovernorCaptureFraction - LightOffFraction);
                return Mathf.Lerp(MotoringRpm, idleRpm, t * t * (3f - 2f * t));
            }

            var captured = (running - GovernorCaptureFraction) / (1f - GovernorCaptureFraction);
            return Mathf.Lerp(idleRpm, GovernedRpm * Mathf.Max(GroundIdleNp, operatingNpFraction),
                Mathf.Clamp01(captured));
        }

        /// <summary>True while the starter is still turning the propeller and the turbine has not lit.</summary>
        public static bool Motoring(float engineFraction) =>
            engineFraction > 0.001f && engineFraction < LightOffFraction;

        /// <summary>
        /// Governor hunting: a real Np needle never sits perfectly still. A fraction of a percent,
        /// enough that the disc is not mathematically frozen, far too little to read as a wobble.
        /// </summary>
        public static float GovernorHunt(float seconds, float key)
        {
            var wave = Mathf.Sin(seconds * 1.7f + key) * 0.6f + Mathf.Sin(seconds * 4.3f + key * 2.1f) * 0.4f;
            return 1f + wave * 0.004f;
        }

        // ---------------------------------------------------------------- power (torque)

        /// <summary>
        /// Shaft power 0..1 for a phase. This is what the blade pitch, the exhaust and the engine
        /// note follow — under a governor it is <em>not</em> recoverable from shaft speed.
        /// </summary>
        public static float PowerFractionForPhase(AircraftPhase phase, float progress01 = 1f)
        {
            var progress = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                    // Power comes up over the first part of the roll rather than arriving whole.
                    return Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(progress / 0.12f));
                case AircraftPhase.Departed:
                    return 0.88f;
                case AircraftPhase.GoAround:
                    return 1f;
                case AircraftPhase.Approach:
                    return 0.3f;
                case AircraftPhase.Landing:
                    // Flight idle down the approach, then reverse, then idle for the rollout.
                    return AirsideFlightPath.HasTouchedDown(progress) ? ReverseBlend(progress) * 0.7f + 0.12f : 0.16f;
                case AircraftPhase.TaxiIn:
                case AircraftPhase.TaxiOut:
                    return 0.14f;
                case AircraftPhase.Pushback:
                    return 0.08f;
                case AircraftPhase.AtStand:
                    return 0.06f;
                default:
                    return 0.62f;
            }
        }

        /// <summary>
        /// 0..1 reverse-thrust (beta) demand over the landing rollout: selected at touchdown,
        /// held while the speed comes off, out of reverse by taxi speed.
        /// </summary>
        public static float ReverseBlend(float landingProgress)
        {
            var progress = Mathf.Clamp01(landingProgress);
            var touchdown = AirsideFlightPath.TouchdownProgress;
            if (progress < touchdown || touchdown >= 1f)
                return 0f;
            var rollout = (progress - touchdown) / (1f - touchdown);
            // In over about a second of rollout, out again by 55% of it.
            var selected = Mathf.Clamp01(rollout / 0.07f);
            var stowed = 1f - Mathf.Clamp01((rollout - 0.3f) / 0.25f);
            return Mathf.Min(selected, stowed);
        }

        // ---------------------------------------------------------------- blade pitch

        /// <summary>Feathered: blades edged into the airflow. What a parked turboprop looks like.</summary>
        public const float FeatherDegrees = 76f;
        /// <summary>Start locks / fine pitch — the flattest the blades go for a start or a run-down.</summary>
        public const float StartFineDegrees = 3f;
        public const float GroundIdleDegrees = 10f;
        public const float TakeoffDegrees = 21f;
        /// <summary>The coarsest normal setting: high power at cruise speed.</summary>
        public const float CruiseDegrees = 39f;
        /// <summary>Beta range: the blades go negative and blow the air forward.</summary>
        public const float ReverseDegrees = -17f;

        /// <summary>
        /// How much coarser the blades go between standing still and cruise speed. A propeller
        /// blade meets the air at the sum of its own rotation and the aircraft's forward speed, so
        /// at speed it needs a far bigger angle to keep biting. This is why a turboprop's blades
        /// visibly coarsen through the takeoff roll and sit coarsest of all in the cruise, despite
        /// cruise using less power than takeoff.
        /// </summary>
        public const float AdvanceCoarseningDegrees = 22f;

        /// <summary>
        /// The blade meshes are lofted with roughly this much twist already in them (34° at the
        /// root to 8° at the tip), so the animation rotates each blade by the <em>difference</em>
        /// from here rather than by the absolute angle.
        /// </summary>
        public const float AuthoredPitchDegrees = 21f;

        /// <summary>
        /// 0 standing still … 1 cruise speed, as the blades see it. Presentation only: it is taken
        /// from the phase rather than from a true airspeed, which the visual passes do not carry.
        /// </summary>
        public static float Advance01ForPhase(AircraftPhase phase, float progress01 = 1f)
        {
            var progress = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case AircraftPhase.AtStand:
                case AircraftPhase.Pushback:
                case AircraftPhase.TaxiIn:
                case AircraftPhase.TaxiOut:
                    return 0f;
                case AircraftPhase.Takeoff:
                    // Speed builds through the roll, so the blades coarsen as it goes.
                    return Mathf.Lerp(0f, 0.42f, Mathf.Clamp01(progress / AirsideFlightPath.RotateProgress));
                case AircraftPhase.Departed:
                    return 0.72f;
                case AircraftPhase.GoAround:
                    return 0.45f;
                case AircraftPhase.Approach:
                    return 0.52f;
                case AircraftPhase.Landing:
                    return AirsideFlightPath.HasTouchedDown(progress)
                        ? Mathf.Lerp(0.42f, 0f, Mathf.Clamp01(
                            (progress - AirsideFlightPath.TouchdownProgress)
                            / Mathf.Max(0.0001f, 1f - AirsideFlightPath.TouchdownProgress) / 0.5f))
                        : 0.48f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Blade angle in degrees. Feathered when the engine is stopped, fine while it is being
        /// started or run down, then off the fine stop — coarsening both with power and with
        /// forward speed — and negative in reverse.
        /// </summary>
        public static float BladePitchDegrees(float engineFraction, float powerFraction,
            float advance01, float reverse01)
        {
            var running = Mathf.Clamp01(engineFraction);
            if (running <= 0.001f)
                return FeatherDegrees;
            if (running < LightOffFraction)
            {
                // Unfeathering into the start: the blades swing flat before the turbine lights.
                var t = Mathf.Clamp01(running / (LightOffFraction * 0.8f));
                return Mathf.Lerp(FeatherDegrees, StartFineDegrees, t * t * (3f - 2f * t));
            }

            var governed = Mathf.Clamp01((running - LightOffFraction) / (GovernorCaptureFraction - LightOffFraction));
            var loaded = Mathf.Lerp(GroundIdleDegrees, TakeoffDegrees, Mathf.Clamp01(powerFraction))
                + AdvanceCoarseningDegrees * Mathf.Clamp01(advance01);
            var pitch = Mathf.Lerp(StartFineDegrees, Mathf.Min(loaded, CruiseDegrees + 6f), governed);
            return Mathf.Lerp(pitch, ReverseDegrees, Mathf.Clamp01(reverse01));
        }

        /// <summary>The animated rotation of a blade about its own radial axis, in degrees.</summary>
        public static float BladePitchOffsetDegrees(float engineFraction, float powerFraction,
            float advance01, float reverse01) =>
            BladePitchDegrees(engineFraction, powerFraction, advance01, reverse01) - AuthoredPitchDegrees;

        // ---------------------------------------------------------------- windmilling

        /// <summary>
        /// A dead engine's propeller is dragged round by the airflow. Feathered it barely turns;
        /// in fine pitch it spins up freely. Presentation only — it makes a failed or cold engine
        /// read as dead machinery rather than as a frozen model.
        /// </summary>
        public static float WindmillRpm(float airspeedMetresPerSecond, float pitchDegrees)
        {
            var speed = Mathf.Abs(airspeedMetresPerSecond);
            if (speed < 2f)
                return 0f;
            // Feathered blades present almost no face to the airflow.
            var bite = Mathf.Clamp01(1f - Mathf.Abs(pitchDegrees) / FeatherDegrees);
            return speed * 4.2f * bite * bite;
        }

        // ---------------------------------------------------------------- the disc

        /// <summary>
        /// How much of the blur disc a viewer sees. Face-on it is the whole propeller; edge-on a
        /// real disc all but disappears, because there is almost nothing in the line of sight.
        /// Cheap, and it removes most of the transparent fill from side-on aircraft.
        /// </summary>
        public static float DiscViewFade(float cosAngleToAxis)
        {
            var face = Mathf.Abs(Mathf.Clamp(cosAngleToAxis, -1f, 1f));
            return Mathf.Lerp(0.12f, 1f, face * face * (3f - 2f * face));
        }

        /// <summary>
        /// Coarse blades have more of themselves in the line of sight than fine ones, so the disc
        /// thickens with pitch as well as with speed — the visible difference between taxi and takeoff.
        /// </summary>
        public static float DiscPitchDensity(float pitchDegrees)
        {
            var coarse = Mathf.Clamp01((Mathf.Abs(pitchDegrees) - GroundIdleDegrees)
                / (CruiseDegrees - GroundIdleDegrees));
            return Mathf.Lerp(0.78f, 1.22f, coarse);
        }

        // ---------------------------------------------------------------- turbofan

        // N1 as a fraction. A jet idles far lower than people expect, and approach idle is lower
        // still — which is exactly why the spool-up on a go-around takes so long.
        public const float JetIdleN1 = 0.21f;
        public const float JetTaxiN1 = 0.26f;
        public const float JetApproachN1 = 0.45f;
        public const float JetTakeoffN1 = 0.95f;
        public const float JetClimbN1 = 0.88f;
        public const float JetCruiseN1 = 0.82f;
        public const float JetReverseN1 = 0.72f;

        public static float JetN1ForPhase(AircraftPhase phase, float progress01 = 1f)
        {
            var progress = Mathf.Clamp01(progress01);
            return phase switch
            {
                AircraftPhase.Takeoff => Mathf.Lerp(JetIdleN1, JetTakeoffN1, Mathf.Clamp01(progress / 0.14f)),
                AircraftPhase.Departed => JetClimbN1,
                AircraftPhase.GoAround => JetTakeoffN1,
                AircraftPhase.Approach => JetApproachN1,
                AircraftPhase.Landing => AirsideFlightPath.HasTouchedDown(progress)
                    ? Mathf.Lerp(JetIdleN1, JetReverseN1, ReverseBlend(progress))
                    : JetIdleN1 + 0.06f,
                AircraftPhase.TaxiIn or AircraftPhase.TaxiOut or AircraftPhase.Pushback => JetTaxiN1,
                AircraftPhase.AtStand => JetIdleN1,
                _ => JetCruiseN1
            };
        }

        /// <summary>
        /// Seconds of spool lag for a change in N1. Accelerating out of the idle range is the
        /// slowest thing a turbofan does; small changes at high power are almost immediate.
        /// </summary>
        public static float JetSpoolSeconds(float fromN1, float toN1)
        {
            if (toN1 <= fromN1)
                return 5.5f;
            var lowEnd = 1f - Mathf.Clamp01((fromN1 - JetIdleN1) / 0.4f);
            return Mathf.Lerp(1.6f, 7.5f, lowEnd);
        }

        /// <summary>A parked fan turns in the wind rather than standing still.</summary>
        public const float FanWindmillRpm = 45f;

        /// <summary>
        /// A shut-down, feathered propeller is not locked: it drifts on the breeze. A handful of
        /// rpm, but a parked turboprop with dead-still blades reads as a photograph.
        /// </summary>
        public const float FeatheredDriftRpm = 6f;
    }
}
