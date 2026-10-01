using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Planning performance for one helicopter type (ADR 0207). Like <see cref="AircraftPerformanceProfile"/>
    /// these are representative normal-weight values, not dispatch data. A helicopter has no runway, rotate
    /// speed or rollout: it lifts to a hover, turns on the spot, accelerates away along its departure line, and
    /// arrives the other way round, so its profile is a different shape.
    /// </summary>
    public readonly struct RotorcraftPerformanceProfile
    {
        public RotorcraftPerformanceProfile(
            float hoverHeightMetres, float liftSeconds, float hoverHoldSeconds, float pedalTurnSeconds,
            float accelClimbSeconds, float climbOutKnots, float climbOutHeightMetres,
            float cruiseKnots, double cruiseFeet, float approachKnots, float approachHeightMetres,
            float decelSeconds, float landingTurnSeconds, float settleSeconds,
            float airTaxiKnots, float padClearSeconds, float rotorDiameterMetres)
        {
            HoverHeightMetres = hoverHeightMetres;
            LiftSeconds = liftSeconds;
            HoverHoldSeconds = hoverHoldSeconds;
            PedalTurnSeconds = pedalTurnSeconds;
            AccelClimbSeconds = accelClimbSeconds;
            ClimbOutKnots = climbOutKnots;
            ClimbOutHeightMetres = climbOutHeightMetres;
            CruiseKnots = cruiseKnots;
            CruiseFeet = cruiseFeet;
            ApproachKnots = approachKnots;
            ApproachHeightMetres = approachHeightMetres;
            DecelSeconds = decelSeconds;
            LandingTurnSeconds = landingTurnSeconds;
            SettleSeconds = settleSeconds;
            AirTaxiKnots = airTaxiKnots;
            PadClearSeconds = padClearSeconds;
            RotorDiameterMetres = rotorDiameterMetres;
        }

        /// <summary>Skid height in the hover, well clear of the pad lights and crew.</summary>
        public float HoverHeightMetres { get; }
        public float LiftSeconds { get; }
        public float HoverHoldSeconds { get; }
        public float PedalTurnSeconds { get; }
        public float AccelClimbSeconds { get; }
        public float ClimbOutKnots { get; }
        /// <summary>Height above the pad where the climb-out hands over to the en-route leg.</summary>
        public float ClimbOutHeightMetres { get; }
        public float CruiseKnots { get; }
        /// <summary>Typical cruise height on the short legs a helicopter flies (well under 3 000 ft).</summary>
        public double CruiseFeet { get; }
        public float ApproachKnots { get; }
        /// <summary>Height above the pad where the approach begins its deceleration.</summary>
        public float ApproachHeightMetres { get; }
        public float DecelSeconds { get; }
        public float LandingTurnSeconds { get; }
        public float SettleSeconds { get; }
        /// <summary>Speed of a hover-taxi between pad spots.</summary>
        public float AirTaxiKnots { get; }
        /// <summary>Time after touch-down or lift-off before another movement may use the pad.</summary>
        public float PadClearSeconds { get; }
        public float RotorDiameterMetres { get; }

        public float TakeoffSeconds => LiftSeconds + HoverHoldSeconds + PedalTurnSeconds + AccelClimbSeconds;
        public float LandingSeconds => DecelSeconds + LandingTurnSeconds + SettleSeconds;
        public float ClimbOutMetresPerSecond => CircuitProfile.Knots(ClimbOutKnots);
        public float ApproachMetresPerSecond => CircuitProfile.Knots(ApproachKnots);
        public float CruiseMetresPerSecond => CircuitProfile.Knots(CruiseKnots);

        /// <summary>Distance flown along the departure line while accelerating from the hover (a linear ramp).</summary>
        public float ClimbOutDistanceMetres => 0.5f * ClimbOutMetresPerSecond * AccelClimbSeconds;

        /// <summary>Distance from the pad at which the approach starts its deceleration (a linear ramp to zero).</summary>
        public float ApproachDistanceMetres => 0.5f * ApproachMetresPerSecond * DecelSeconds;
    }

    /// <summary>Where a helicopter is, in the frame of its pad spot and its flight line (ADR 0207).</summary>
    public readonly struct HelicopterPose
    {
        public HelicopterPose(float alongMetres, float heightMetres, float turn01, float pitchDegrees,
            float speedMetresPerSecond, float verticalMetresPerSecond, float rotorLoad01)
        {
            AlongMetres = alongMetres;
            HeightMetres = heightMetres;
            Turn01 = turn01;
            PitchDegrees = pitchDegrees;
            SpeedMetresPerSecond = speedMetresPerSecond;
            VerticalMetresPerSecond = verticalMetresPerSecond;
            RotorLoad01 = rotorLoad01;
        }

        /// <summary>Metres along the flight line from the pad spot; negative while still on the approach.</summary>
        public float AlongMetres { get; }
        public float HeightMetres { get; }

        /// <summary>
        /// How far round its turn the nose has come: 0 is the parked heading and 1 the flight-line heading on
        /// take-off; on landing 0 is the flight-line heading and 1 the parked heading.
        /// </summary>
        public float Turn01 { get; }

        /// <summary>Nose attitude, positive nose-down (the way a helicopter leans to accelerate).</summary>
        public float PitchDegrees { get; }
        public float SpeedMetresPerSecond { get; }
        public float VerticalMetresPerSecond { get; }

        /// <summary>Power the rotor is working at, 0 (flat pitch) to 1: drives rotor sound and blade load.</summary>
        public float RotorLoad01 { get; }
    }

    public static class RotorcraftPerformance
    {
        // Bell 412EP: 122 kt maximum cruise (Bell), best rate-of-climb speed about 70 kt. Planning values:
        // a calm 6 s lift to a 3.5 m hover, a 5 s pedal turn, then a 22 s accelerating climb to 80 kt.
        public static readonly RotorcraftPerformanceProfile Bell412 = new(
            hoverHeightMetres: 3.5f, liftSeconds: 6f, hoverHoldSeconds: 3f, pedalTurnSeconds: 5f,
            accelClimbSeconds: 22f, climbOutKnots: 80f, climbOutHeightMetres: 110f,
            cruiseKnots: 110f, cruiseFeet: 1500, approachKnots: 70f, approachHeightMetres: 90f,
            decelSeconds: 30f, landingTurnSeconds: 6f, settleSeconds: 7f,
            airTaxiKnots: 8f, padClearSeconds: 12f, rotorDiameterMetres: 14.02f);

        public static RotorcraftPerformanceProfile For(AircraftType type) => Bell412;
    }

    /// <summary>
    /// The shape of a helicopter movement as pure functions of time (ADR 0207): take-off from the pad spot
    /// and landing onto it. Presentation turns the along-line distance and turn fraction into a world pose;
    /// the simulation only needs the durations.
    /// </summary>
    public static class HelicopterFlight
    {
        public static HelicopterPose TakeoffPose(RotorcraftPerformanceProfile profile, float secondsIntoTakeoff)
        {
            var t = Math.Max(0f, secondsIntoTakeoff);
            var hover = profile.HoverHeightMetres;
            var lift = profile.LiftSeconds;
            var hold = profile.HoverHoldSeconds;
            var turn = profile.PedalTurnSeconds;
            var accel = profile.AccelClimbSeconds;

            if (t < lift)
            {
                var u = t / lift;
                var height = hover * Smooth(u);
                var vertical = hover * SmoothSlope(u) / lift;
                return new HelicopterPose(0f, height, 0f, 0f, 0f, vertical, 0.35f + 0.55f * u);
            }

            t -= lift;
            if (t < hold)
                return new HelicopterPose(0f, hover, 0f, 0f, 0f, 0f, 0.85f);

            t -= hold;
            if (t < turn)
                return new HelicopterPose(0f, hover, Smooth(t / turn), 0f, 0f, 0f, 0.85f);

            t -= turn;
            if (t >= accel)
                return CruiseStart(profile);

            var a = t / accel;
            var speed = profile.ClimbOutMetresPerSecond * a;
            var along = 0.5f * profile.ClimbOutMetresPerSecond * accel * a * a;
            var climbHeight = hover + (profile.ClimbOutHeightMetres - hover) * Smooth(a);
            var climbRate = (profile.ClimbOutHeightMetres - hover) * SmoothSlope(a) / accel;
            // Nose forward to build speed, easing level as the climb settles.
            var pitch = 9f * (float)Math.Sin(Math.PI * Math.Min(1f, a * 1.15f)) * (1f - 0.5f * a);
            return new HelicopterPose(along, climbHeight, 1f, pitch, speed, climbRate, 1f);
        }

        /// <summary>The state the climb-out hands over to the en-route leg in.</summary>
        public static HelicopterPose CruiseStart(RotorcraftPerformanceProfile profile) =>
            new(profile.ClimbOutDistanceMetres, profile.ClimbOutHeightMetres, 1f, 3f,
                profile.ClimbOutMetresPerSecond, 0f, 0.8f);

        public static HelicopterPose LandingPose(RotorcraftPerformanceProfile profile, float secondsIntoLanding)
        {
            var t = Math.Max(0f, secondsIntoLanding);
            var hover = profile.HoverHeightMetres;
            var decel = profile.DecelSeconds;
            var turn = profile.LandingTurnSeconds;
            var settle = profile.SettleSeconds;

            if (t < decel)
            {
                var u = t / decel;
                var speed = profile.ApproachMetresPerSecond * (1f - u);
                var remaining = profile.ApproachDistanceMetres * (1f - u) * (1f - u);
                var height = hover + (profile.ApproachHeightMetres - hover) * (1f - Smooth(u));
                var vertical = -(profile.ApproachHeightMetres - hover) * SmoothSlope(u) / decel;
                // Level, then nose-up in the flare to shed the last of the speed.
                var flare = Math.Max(0f, (u - 0.45f) / 0.55f);
                var pitch = 3f * (1f - u) - 13f * (float)Math.Sin(Math.PI * Math.Min(1f, flare));
                return new HelicopterPose(-remaining, height, 0f, pitch, speed, vertical, 0.7f + 0.2f * u);
            }

            t -= decel;
            if (t < turn)
                return new HelicopterPose(0f, hover, Smooth(t / turn), 0f, 0f, 0f, 0.85f);

            t -= turn;
            if (t < settle)
            {
                var u = t / settle;
                return new HelicopterPose(0f, hover * (1f - Smooth(u)), 1f, 0f, 0f,
                    -hover * SmoothSlope(u) / settle, 0.85f - 0.35f * u);
            }

            return new HelicopterPose(0f, 0f, 1f, 0f, 0f, 0f, 0.5f);
        }

        /// <summary>
        /// The fully shut-down and idling states a helicopter sits in on its pad, so sound and rotor spool can
        /// share the pose type: flat pitch, no speed.
        /// </summary>
        public static HelicopterPose OnPad(float rotorLoad01) => new(0f, 0f, 1f, 0f, 0f, 0f, rotorLoad01);

        /// <summary>Smooth ease 0..1 (cubic).</summary>
        public static float Smooth(float x)
        {
            var t = x < 0f ? 0f : x > 1f ? 1f : x;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Slope of <see cref="Smooth"/>.</summary>
        public static float SmoothSlope(float x)
        {
            var t = x < 0f ? 0f : x > 1f ? 1f : x;
            return 6f * t * (1f - t);
        }
    }

    /// <summary>Weather and pad rules for helicopter movements (ADR 0207).</summary>
    public static class RotorcraftRules
    {
        /// <summary>Surface wind above which a civil helicopter stays on the pad.</summary>
        public const int CivilWindLimitKnots = 40;

        /// <summary>Surface wind above which even a rescue helicopter will not lift.</summary>
        public const int RescueWindLimitKnots = 55;

        /// <summary>
        /// A storm grounds every helicopter (lightning, gusts, hail). Fog holds a civil helicopter (it flies
        /// visually) but not a rescue one (it flies on instruments to a hospital pad). Hard wind grounds civil
        /// flying first, rescue last.
        /// </summary>
        public static bool MayMove(WeatherKind weather, int windKnots, bool rescue)
        {
            if (weather == WeatherKind.Storm)
                return false;
            if (!rescue && weather == WeatherKind.Fog)
                return false;
            return windKnots <= (rescue ? RescueWindLimitKnots : CivilWindLimitKnots);
        }

        /// <summary>Seconds the pad is held by a take-off: lift-off to clear of the hover, plus a gap.</summary>
        public static long PadSecondsForTakeoff(RotorcraftPerformanceProfile profile) =>
            (long)Math.Ceiling(profile.LiftSeconds + profile.HoverHoldSeconds + profile.PedalTurnSeconds
                               + profile.PadClearSeconds);

        /// <summary>Seconds the pad is held by a landing: the final hover and settle, plus a gap.</summary>
        public static long PadSecondsForLanding(RotorcraftPerformanceProfile profile) =>
            (long)Math.Ceiling(profile.LandingSeconds + profile.PadClearSeconds);
    }
}
