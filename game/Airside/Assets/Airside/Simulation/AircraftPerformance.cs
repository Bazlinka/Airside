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
            float taxiStraightKnots, float taxiApronKnots, float runwayExitKnots,
            float noseToMainGearMetres)
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
            RunwayExitKnots = runwayExitKnots;
            NoseToMainGearMetres = noseToMainGearMetres;
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
        public float RunwayExitKnots { get; }
        /// <summary>Longitudinal distance from nose gear to main gear, used to steer the
        /// main gear (not the nose) through taxiway turns instead of every type sharing one
        /// generic offset. Sources are recorded in
        /// docs/data/AIRCRAFT_SPECIFICATIONS.md's ground taxi section.</summary>
        public float NoseToMainGearMetres { get; }

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
            CircuitProfile.Knots(TouchdownKnots), CircuitProfile.Knots(RunwayExitKnots));
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
                    return Lerp(TouchdownKnots, RunwayExitKnots, Local(t, TouchdownProgress, 1f));
                case AircraftPhase.Takeoff:
                    if (t < RotateProgress)
                        return Lerp(0f, RotateKnots, Local(t, 0f, RotateProgress));
                    return Lerp(RotateKnots, InitialClimbKnots, Local(t, RotateProgress, 1f));
                case AircraftPhase.Departed:
                    return Lerp(InitialClimbKnots, ClimbOutKnots, t);
                case AircraftPhase.Circuit:
                    return ApproachEntryKnots;
                case AircraftPhase.GoAround:
                    return Lerp(ApproachKnots, InitialClimbKnots, t < 0.28f ? t / 0.28f : 1f);
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
        // Wheelbase: not currently steering-corrected (only the terminal-gate jets are —
        // see AdelaideGround.GateTaxiOut/In), so left at 0 rather than an unsourced guess.
        public static readonly AircraftPerformanceProfile Atr42 = new(
            120f, 110f, 95f, 104f, 120f, 160f, 900f, 6.6f, 6.1f,
            25000, 1200, 1500, 22f, 14f, 12f, 0f);

        // Saab/Q400/737 figures are representative normal-weight planning values.
        // They intentionally remain inside the range pilots calculate for each flight.
        public static readonly AircraftPerformanceProfile Saab340 = new(
            120f, 108f, 94f, 105f, 120f, 155f, 980f, 6.0f, 5.6f,
            25000, 1100, 1400, 20f, 13f, 12f, 0f);

        public static readonly AircraftPerformanceProfile Dash8Q400 = new(
            135f, 125f, 110f, 116f, 135f, 185f, 1150f, 8.0f, 7.2f,
            25000, 1450, 1700, 24f, 15f, 14f, 0f);

        public static readonly AircraftPerformanceProfile EmbraerE190 = new(
            150f, 134f, 123f, 140f, 160f, 205f, 1450f, 11.0f, 9.5f,
            41000, 2000, 1900, 20f, 10f, 15f, 14.65f);

        public static readonly AircraftPerformanceProfile AirbusA220300 = new(
            150f, 136f, 125f, 142f, 162f, 208f, 1500f, 11.5f, 9.8f,
            41000, 2100, 1950, 20f, 10f, 15f, 14.86f);

        public static readonly AircraftPerformanceProfile AirbusA320200 = new(
            153f, 139f, 127f, 144f, 164f, 209f, 1600f, 11.0f, 9.5f,
            39800, 1950, 1850, 20f, 10f, 15f, 12.64f);

        public static readonly AircraftPerformanceProfile Boeing737800 = new(
            155f, 143f, 131f, 145f, 165f, 210f, 1700f, 12.0f, 10.0f,
            41000, 2100, 1900, 20f, 10f, 15f, 15.60f);

        // AIR-005's runtime nose/main gear centres are 15.30 m apart. Gate taxi paths
        // steer those exact rendered pivots, so the motion profile must match the kit
        // rather than the former unverified 17.68 m planning value.
        public static readonly AircraftPerformanceProfile Boeing7378 = new(
            155f, 145f, 132f, 145f, 165f, 210f, 1650f, 12.0f, 10.0f,
            41000, 2100, 1900, 20f, 10f, 15f, 15.30f);

        // Representative normal-weight A321neo values. Like the other jets these are
        // visual-planning values; crews calculate actual speeds for each departure.
        // Wheelbase: Airbus A321 Aircraft Characteristics (aircraft.airbus.com).
        public static readonly AircraftPerformanceProfile AirbusA321Neo = new(
            155f, 140f, 128f, 145f, 165f, 210f, 1800f, 11.0f, 9.5f,
            39800, 1900, 1800, 20f, 10f, 15f, 16.90f);

        // Wheelbase: Airbus A350-900/-1000 Aircraft Characteristics (aircraft.airbus.com).
        public static readonly AircraftPerformanceProfile AirbusA350900 = new(
            165f, 150f, 138f, 158f, 180f, 225f, 2050f, 11.5f, 10.0f,
            43000, 1800, 1800, 20f, 8f, 14f, 28.66f);

        // Wheelbase: Boeing 787 Airplane Characteristics for Airport Planning
        // (787_Rev_P.pdf) — its 68.30 m length figure matches this project's own
        // recorded AIR-010 runtime-model envelope exactly, corroborating the source.
        public static readonly AircraftPerformanceProfile Boeing78710 = new(
            166f, 151f, 139f, 159f, 181f, 226f, 2200f, 11.0f, 9.5f,
            43000, 1800, 1800, 20f, 8f, 14f, 28.88f);

        public static readonly AircraftPerformanceProfile AirbusA330900 = new(
            164f, 148f, 137f, 157f, 179f, 224f, 2100f, 11.5f, 10.0f,
            41450, 1800, 1800, 20f, 8f, 14f, 25.38f);

        public static readonly AircraftPerformanceProfile Boeing7879 = new(
            165f, 149f, 138f, 158f, 180f, 225f, 2150f, 11.0f, 9.5f,
            43000, 1800, 1800, 20f, 8f, 14f, 25.83f);

        public static AircraftPerformanceProfile For(AircraftType type)
        {
            if (type == null)
                return Atr42;
            return type.Id switch
            {
                "SF34" => Saab340,
                "DH8D" => Dash8Q400,
                "E190" => EmbraerE190,
                "A223" => AirbusA220300,
                "A320" => AirbusA320200,
                "B738" => Boeing737800,
                "B38M" => Boeing7378,
                "A21N" => AirbusA321Neo,
                "A359" => AirbusA350900,
                "B78X" => Boeing78710,
                "A339" => AirbusA330900,
                "B789" => Boeing7879,
                _ => Atr42
            };
        }
    }
}
