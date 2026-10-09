using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Body pitch through each flight phase, per aircraft type. Pitch is the flight-path angle plus the
    /// angle of attack, so a 737 on a 3 degree glideslope sits about 2 degrees nose-up, flares to roughly
    /// 5, rotates at about 3 degrees a second to a 15 degree climb, while an ATR keeps a flatter attitude.
    /// Values are the codebase's convention: negative is nose-up. Planning values, not type-rating data.
    /// Independent of Unity so the headless harness can check them.
    /// </summary>
    public readonly struct AircraftAttitude
    {
        public readonly float ApproachStart, ApproachEnd, Flare, TouchdownHold, Rotate, Climb, DepartedEnd;

        public AircraftAttitude(float approachStart, float approachEnd, float flare, float touchdownHold,
            float rotate, float climb, float departedEnd)
        {
            ApproachStart = approachStart; ApproachEnd = approachEnd; Flare = flare;
            TouchdownHold = touchdownHold; Rotate = rotate; Climb = climb; DepartedEnd = departedEnd;
        }

        // The original ATR 42 figures; AirsideFlightPath's constants mirror these.
        public static readonly AircraftAttitude Atr42 = new(-2.4f, -3.0f, -6.5f, -4.0f, -9f, -7.5f, -4f);
        public static readonly AircraftAttitude Saab340 = new(-2.4f, -3.2f, -6.5f, -4.0f, -9f, -8f, -5f);
        public static readonly AircraftAttitude Dash8 = new(-2.0f, -3.0f, -6.0f, -4.0f, -10f, -11f, -6.5f);
        // Narrowbody and regional jets: flatter approach (higher speed, lower AoA), a firm 5 degree
        // touchdown attitude, a 3 deg/s rotation to a 15 degree climb easing to ~9 as speed builds.
        public static readonly AircraftAttitude NarrowJet = new(-0.8f, -2.6f, -5.0f, -4.2f, -13f, -15f, -9f);
        public static readonly AircraftAttitude Airbus320 = new(-1.0f, -3.4f, -5.2f, -4.4f, -13f, -15f, -9f);
        public static readonly AircraftAttitude Widebody = new(-0.8f, -2.4f, -5.0f, -4.2f, -11f, -12.5f, -7.5f);

        public static AircraftAttitude For(AircraftType type)
        {
            if (type == null) return Atr42;
            return type.Id switch
            {
                "SF34" => Saab340,
                "DH8D" => Dash8,
                "A320" or "A21N" or "A223" => Airbus320,
                "B738" or "B38M" or "E190" => NarrowJet,
                "A359" or "A339" or "B789" or "B78X" => Widebody,
                _ => Atr42
            };
        }

        public float PitchDegrees(AircraftPhase phase, float progress, AircraftPerformanceProfile profile)
        {
            var t = Clamp01(progress);
            var rotateProgress = profile.RotateProgress;
            var flareProgress = profile.FlareProgress;
            var touchdownProgress = profile.TouchdownProgress;
            switch (phase)
            {
                case AircraftPhase.Takeoff:
                {
                    if (t <= rotateProgress) return 0f;
                    var climb = Local(t, rotateProgress, 1f);
                    var ease = Smooth(Math.Min(1f, climb * 2.4f));
                    var rotation = Lerp(0f, Rotate, ease);
                    return Lerp(rotation, Climb, Smooth(Local(climb, 0.65f, 1f)));
                }
                case AircraftPhase.Approach:
                    return Lerp(ApproachStart, ApproachEnd, t);
                case AircraftPhase.Landing:
                {
                    if (t < flareProgress) return ApproachEnd;
                    if (t < touchdownProgress)
                        return Lerp(ApproachEnd, Flare, Smooth(Local(t, flareProgress, touchdownProgress)));
                    var since = Local(t, touchdownProgress, 1f);
                    if (since < 0.08f) return Lerp(Flare, TouchdownHold, since / 0.08f);
                    return Lerp(TouchdownHold, 0f, Smooth(Math.Min(1f, (since - 0.08f) / 0.2f)));
                }
                case AircraftPhase.Departed:
                    return Lerp(Climb, DepartedEnd, t);
                case AircraftPhase.Circuit:
                    // Hold the go-around's level-off attitude, then settle toward the approach attitude
                    // over the last quarter so the hand-over to Approach does not step.
                    return Lerp(Climb * 0.45f, ApproachStart, Smooth(Local(t, 0.75f, 1f)));
                case AircraftPhase.GoAround:
                    return t < 0.28f ? Lerp(ApproachEnd, Climb, Smooth(t / 0.28f)) : Climb * 0.45f;
                default:
                    return 0f;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Local(float t, float from, float to) => to <= from ? 0f : Clamp01((t - from) / (to - from));
    }
}
