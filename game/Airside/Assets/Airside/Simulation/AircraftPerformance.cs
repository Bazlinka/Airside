using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Typical operating performance for one fleet type. These are planning values,
    /// not dispatch data: real V-speeds move with weight, flap, pressure, temperature
    /// and runway condition. Keeping the whole profile together prevents a 737 from
    /// inheriting an ATR's rotate point, approach speed or climb.
    /// </summary>
    public readonly struct AircraftPerformanceProfile
    {
        public AircraftPerformanceProfile(
            float approachEntryKnots, float approachKnots, float touchdownKnots,
            float rotateKnots, float initialClimbKnots, float climbOutKnots,
            float takeoffRollMetres, float initialClimbRateMetresPerSecond,
            float climbOutRateMetresPerSecond, double maxCruiseFeet,
            double climbFeetPerMinute, double descentFeetPerMinute,
            float taxiStraightKnots, float taxiApronKnots)
        {
            ApproachEntryKnots = approachEntryKnots;
            ApproachKnots = approachKnots;
            TouchdownKnots = touchdownKnots;
            RotateKnots = rotateKnots;
            InitialClimbKnots = initialClimbKnots;
            ClimbOutKnots = climbOutKnots;
            TakeoffRollMetres = takeoffRollMetres;
            InitialClimbRateMetresPerSecond = initialClimbRateMetresPerSecond;
            ClimbOutRateMetresPerSecond = climbOutRateMetresPerSecond;
            MaxCruiseFeet = maxCruiseFeet;
            ClimbFeetPerMinute = climbFeetPerMinute;
            DescentFeetPerMinute = descentFeetPerMinute;
            TaxiStraightKnots = taxiStraightKnots;
            TaxiApronKnots = taxiApronKnots;
        }

        public float ApproachEntryKnots { get; }
        public float ApproachKnots { get; }
        public float TouchdownKnots { get; }
        public float RotateKnots { get; }
        public float InitialClimbKnots { get; }
        public float ClimbOutKnots { get; }
        public float TakeoffRollMetres { get; }
        public float InitialClimbRateMetresPerSecond { get; }
        public float ClimbOutRateMetresPerSecond { get; }
        public double MaxCruiseFeet { get; }
        public double ClimbFeetPerMinute { get; }
        public double DescentFeetPerMinute { get; }
        public float TaxiStraightKnots { get; }
        public float TaxiApronKnots { get; }

        public float RotateX => CircuitProfile.TakeoffStartX + TakeoffRollMetres;
        public float TakeoffEndX => RotateX + 950f;
        public float DepartedEndX => TakeoffEndX + 2350f;
        public float TakeoffEndHeight =>
            (TakeoffEndX - RotateX) * (InitialClimbRateMetresPerSecond / CircuitProfile.Knots(InitialClimbKnots));
        public float DepartedEndHeight => TakeoffEndHeight
            + (DepartedEndX - TakeoffEndX) * (ClimbOutRateMetresPerSecond / CircuitProfile.Knots(ClimbOutKnots));

        public float ApproachExactSeconds => CircuitProfile.SegmentSeconds(
            CircuitProfile.ShortFinalX - CircuitProfile.ApproachStartX,
            CircuitProfile.Knots(ApproachEntryKnots), CircuitProfile.Knots(ApproachKnots));
        public float FinalGlideExactSeconds => CircuitProfile.SegmentSeconds(
            CircuitProfile.FlareStartX - CircuitProfile.ShortFinalX,
            CircuitProfile.Knots(ApproachKnots), CircuitProfile.Knots(ApproachKnots));
        public float FlareExactSeconds => CircuitProfile.SegmentSeconds(
            CircuitProfile.FlareFloatMetres,
            CircuitProfile.Knots(ApproachKnots), CircuitProfile.Knots(TouchdownKnots));
        public float RolloutExactSeconds => CircuitProfile.SegmentSeconds(
            CircuitProfile.RolloutEndX - CircuitProfile.TouchdownX,
            CircuitProfile.Knots(TouchdownKnots), CircuitProfile.Knots(CircuitProfile.RunwayExitKnots));
        public float LandingExactSeconds => FinalGlideExactSeconds + FlareExactSeconds + RolloutExactSeconds;
        public float TakeoffRollExactSeconds => CircuitProfile.SegmentSeconds(
            TakeoffRollMetres, 0f, CircuitProfile.Knots(RotateKnots));
        public float InitialClimbExactSeconds => CircuitProfile.SegmentSeconds(
            TakeoffEndX - RotateX, CircuitProfile.Knots(RotateKnots), CircuitProfile.Knots(InitialClimbKnots));
        public float TakeoffExactSeconds => TakeoffRollExactSeconds + InitialClimbExactSeconds;
        public float DepartedExactSeconds => CircuitProfile.SegmentSeconds(
            DepartedEndX - TakeoffEndX, CircuitProfile.Knots(InitialClimbKnots), CircuitProfile.Knots(ClimbOutKnots));

        public long ApproachSeconds => (long)Math.Round(ApproachExactSeconds);
        public long LandingSeconds => (long)Math.Round(LandingExactSeconds);
        public long TakeoffSeconds => (long)Math.Round(TakeoffExactSeconds);
        public long DepartedSeconds => (long)Math.Round(DepartedExactSeconds);
        public float FlareProgress => FinalGlideExactSeconds / LandingExactSeconds;
        public float TouchdownProgress => (FinalGlideExactSeconds + FlareExactSeconds) / LandingExactSeconds;
        public float RotateProgress => TakeoffRollExactSeconds / TakeoffExactSeconds;

        public float AirspeedKnots(AircraftPhase phase, float progress)
        {
            var t = Clamp01(progress);
            switch (phase)
            {
                case AircraftPhase.Approach:
                    return Lerp(ApproachEntryKnots, ApproachKnots, t);
                case AircraftPhase.Landing:
                    if (t < FlareProgress)
                        return ApproachKnots;
                    if (t < TouchdownProgress)
                        return Lerp(ApproachKnots, TouchdownKnots, Local(t, FlareProgress, TouchdownProgress));
                    return Lerp(TouchdownKnots, CircuitProfile.RunwayExitKnots, Local(t, TouchdownProgress, 1f));
                case AircraftPhase.Takeoff:
                    if (t < RotateProgress)
                        return Lerp(0f, RotateKnots, Local(t, 0f, RotateProgress));
                    return Lerp(RotateKnots, InitialClimbKnots, Local(t, RotateProgress, 1f));
                case AircraftPhase.Departed:
                    return Lerp(InitialClimbKnots, ClimbOutKnots, t);
                default:
                    return 0f;
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Local(float value, float from, float to) =>
            to <= from ? 0f : Clamp01((value - from) / (to - from));
    }

    public static class AircraftPerformance
    {
        // ATR published V2 minimum 112 KCAS, Vref 104 KIAS and optimum climb 160 KCAS.
        public static readonly AircraftPerformanceProfile Atr42 = new(
            120f, 110f, 95f, 104f, 120f, 160f, 900f, 6.6f, 6.1f,
            25000, 1200, 1500, 22f, 14f);

        // Saab/Q400/737 figures are representative normal-weight planning values.
        // They intentionally remain inside the range pilots calculate for each flight.
        public static readonly AircraftPerformanceProfile Saab340 = new(
            120f, 108f, 94f, 105f, 120f, 155f, 980f, 6.0f, 5.6f,
            25000, 1100, 1400, 20f, 13f);

        public static readonly AircraftPerformanceProfile Dash8Q400 = new(
            135f, 125f, 110f, 116f, 135f, 185f, 1150f, 8.0f, 7.2f,
            25000, 1450, 1700, 24f, 15f);

        public static readonly AircraftPerformanceProfile Boeing7378 = new(
            155f, 145f, 132f, 145f, 165f, 210f, 1650f, 12.0f, 10.0f,
            41000, 2100, 1900, 20f, 10f);

        public static AircraftPerformanceProfile For(AircraftType type)
        {
            if (type == null)
                return Atr42;
            return type.Id switch
            {
                "SF34" => Saab340,
                "DH8D" => Dash8Q400,
                "B38M" => Boeing7378,
                _ => Atr42
            };
        }
    }
}
