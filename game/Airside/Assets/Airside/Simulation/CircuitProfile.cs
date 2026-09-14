using System;

namespace Airside.Simulation
{
    /// <summary>
    /// The flown circuit as performance figures rather than guessed timings: where
    /// the aircraft is on the Adelaide 05/23 strip, how fast it is going there, and
    /// therefore how long each phase takes.
    ///
    /// Phase durations used to be picked first and the speeds fell out of them,
    /// which is how the takeoff roll ended up passing rotate at 179 kt and leaving
    /// the field at 257 kt. Here the stations and the reference speeds are the
    /// inputs and the durations are derived, so a speed can only be wrong if the
    /// figure it came from is wrong.
    ///
    /// Figures are typical ATR 42-600 values at a normal operating weight, not a
    /// performance manual: Vr 100 kt, V2 105 kt, Vapp/Vref 110 kt, touchdown 95 kt,
    /// initial climb 120 kt, climb-out 170 kt. Deliberately UnityEngine-free so the
    /// headless harness can check every one of them.
    /// </summary>
    public static class CircuitProfile
    {
        public const float KnotsToMetresPerSecond = 0.514444f;

        public static float Knots(float knots) => knots * KnotsToMetresPerSecond;
        public static float ToKnots(float metresPerSecond) => metresPerSecond / KnotsToMetresPerSecond;

        // --- reference speeds (knots) -------------------------------------------------

        /// <summary>Speed joining the long straight-in, still slowing to Vapp.</summary>
        public const float ApproachEntryKnots = 120f;

        /// <summary>Vapp / Vref, held down the glideslope to the flare.</summary>
        public const float ApproachKnots = 110f;

        /// <summary>Main wheels on, after the flare has scrubbed off Vapp.</summary>
        public const float TouchdownKnots = 95f;

        /// <summary>Vr — nose comes up here, not before.</summary>
        public const float RotateKnots = 100f;

        /// <summary>Initial climb, gear up, at the end of the takeoff phase.</summary>
        public const float InitialClimbKnots = 120f;

        /// <summary>Climb-out speed once clear of the field.</summary>
        public const float ClimbOutKnots = 170f;

        // --- stations along the runway centreline (metres, aircraft flies +X) ---------

        public const float ApproachStartX = -4200f;
        public const float ShortFinalX = -1850f;

        /// <summary>West threshold of the 3 100 m strip.</summary>
        public const float WestThresholdX = -1550f;

        /// <summary>
        /// Where the wheels arrive: on the 300 m touchdown-zone markings, which are
        /// painted there. Everything else on the approach is derived backwards from
        /// this so the aeroplane lands where the runway says it should.
        /// </summary>
        public const float TouchdownX = -1250f;

        /// <summary>
        /// Ground covered between flare entry and the wheels touching. The aircraft
        /// floats past the aim point while the sink is arrested; without this the
        /// "flare" is just the glideslope continued into the tarmac.
        ///
        /// 300 m is what buys a soft arrival. The trade-off is the threshold
        /// crossing: holding the wheels on the 300 m markings with this much float
        /// puts the aircraft over the fence at about 30 ft rather than the textbook
        /// 50. Crossing at 50 ft instead would need the float cut to ~180 m, and the
        /// sink could then only be arrested to about 430 ft/min — a firm arrival
        /// that reads as a thump. Landing on the paint with a soft touchdown is the
        /// better-looking half of that trade.
        /// </summary>
        public const float FlareFloatMetres = 300f;

        /// <summary>Sink rate at touchdown, about 60 ft/min — firm but not a thump.</summary>
        public const float TouchdownSinkMetresPerSecond = 0.30f;

        /// <summary>Rollout end, still on the 3 100 m strip.</summary>
        public const float RolloutEndX = -200f;

        /// <summary>
        /// Where a runway 05 departure is lined up and starts its roll: just past the
        /// threshold, entered from the F6 holding point (real YPAD layout). The roll,
        /// initial climb and climb-out distances are unchanged from the mid-field start
        /// they replaced; only the start moved.
        /// </summary>
        public const float TakeoffStartX = AdelaideLayout.TakeoffStartX;

        public const float RotateX = TakeoffStartX + 900f;
        public const float TakeoffEndX = RotateX + 950f;
        public const float DepartedEndX = TakeoffEndX + 2350f;

        // --- vertical profile ---------------------------------------------------------

        /// <summary>Standard 3° glideslope, flown from the approach start to the flare.</summary>
        public const float GlideslopeDegrees = 3f;

        /// <summary>
        /// Height above the touchdown plane at which the flare begins — about 30 ft,
        /// which is where a turboprop of this size is rounded out.
        /// </summary>
        public const float FlareHeightMetres = 9f;

        /// <summary>Initial climb rate, ~1 300 ft/min.</summary>
        public const float InitialClimbRateMetresPerSecond = 6.6f;

        /// <summary>Climb-out rate once accelerating away, ~1 200 ft/min.</summary>
        public const float ClimbOutRateMetresPerSecond = 6.1f;

        public static float GlideslopeTangent =>
            (float)Math.Tan(GlideslopeDegrees * Math.PI / 180.0);

        /// <summary>
        /// Where the flare starts: the point on the glideslope that is
        /// <see cref="FlareHeightMetres"/> above the touchdown plane. About 172 m
        /// short of the aim point, giving a flare of roughly three seconds.
        /// </summary>
        /// <summary>Where the round-out begins, one float short of the wheels arriving.</summary>
        public static float FlareStartX => TouchdownX - FlareFloatMetres;

        /// <summary>
        /// Where the glideslope meets the ground — what the aircraft is aimed at, and
        /// deliberately short of where it lands. Derived so the slope passes through
        /// <see cref="FlareHeightMetres"/> exactly at <see cref="FlareStartX"/>.
        /// </summary>
        public static float AimPointX => FlareStartX + FlareHeightMetres / GlideslopeTangent;

        /// <summary>Height above the ground at station <paramref name="x"/> on the glideslope.</summary>
        public static float GlideslopeHeight(float x) =>
            x >= AimPointX ? 0f : (AimPointX - x) * GlideslopeTangent;

        /// <summary>
        /// Height crossing the threshold. About 30 ft here rather than the textbook
        /// 50 — the cost of landing on the paint with a 300 m float. See
        /// <see cref="FlareFloatMetres"/>.
        /// </summary>
        public static float ThresholdCrossingHeight => GlideslopeHeight(WestThresholdX);

        public static float ApproachStartHeight => GlideslopeHeight(ApproachStartX);
        public static float ShortFinalHeight => GlideslopeHeight(ShortFinalX);

        /// <summary>Height gained between rotate and the end of the takeoff phase.</summary>
        public static float TakeoffEndHeight =>
            (TakeoffEndX - RotateX) * (InitialClimbRateMetresPerSecond / Knots(InitialClimbKnots));

        /// <summary>Height at the point the slot recycles off-field.</summary>
        public static float DepartedEndHeight =>
            TakeoffEndHeight
            + (DepartedEndX - TakeoffEndX) * (ClimbOutRateMetresPerSecond / Knots(ClimbOutKnots));

        /// <summary>Rate of descent flying the glideslope at Vapp, metres per second.</summary>
        public static float ApproachDescentRate => Knots(ApproachKnots) * GlideslopeTangent;

        // --- segment timing -----------------------------------------------------------

        /// <summary>
        /// Seconds to cover <paramref name="distance"/> accelerating uniformly from
        /// <paramref name="entrySpeed"/> to <paramref name="exitSpeed"/> (m/s). This is
        /// the only place a duration comes from.
        /// </summary>
        public static float SegmentSeconds(float distance, float entrySpeed, float exitSpeed)
        {
            var mean = (entrySpeed + exitSpeed) * 0.5f;
            if (mean <= 0.0001f || distance <= 0f)
                return 0f;
            return distance / mean;
        }

        public static float ApproachExactSeconds =>
            SegmentSeconds(ShortFinalX - ApproachStartX, Knots(ApproachEntryKnots), Knots(ApproachKnots));

        public static float FinalGlideExactSeconds =>
            SegmentSeconds(FlareStartX - ShortFinalX, Knots(ApproachKnots), Knots(ApproachKnots));

        public static float FlareExactSeconds =>
            SegmentSeconds(FlareFloatMetres, Knots(ApproachKnots), Knots(TouchdownKnots));

        public static float RolloutExactSeconds =>
            SegmentSeconds(RolloutEndX - TouchdownX, Knots(TouchdownKnots), 0f);

        public static float LandingExactSeconds =>
            FinalGlideExactSeconds + FlareExactSeconds + RolloutExactSeconds;

        public static float TakeoffRollExactSeconds =>
            SegmentSeconds(RotateX - TakeoffStartX, 0f, Knots(RotateKnots));

        public static float InitialClimbExactSeconds =>
            SegmentSeconds(TakeoffEndX - RotateX, Knots(RotateKnots), Knots(InitialClimbKnots));

        public static float TakeoffExactSeconds =>
            TakeoffRollExactSeconds + InitialClimbExactSeconds;

        public static float DepartedExactSeconds =>
            SegmentSeconds(DepartedEndX - TakeoffEndX, Knots(InitialClimbKnots), Knots(ClimbOutKnots));

        // --- whole seconds the simulation clock runs on --------------------------------
        // The clock ticks in whole seconds, so each phase is the nearest second to its
        // derived duration. The residual stretch is under 1% and is absorbed evenly
        // across the phase rather than dumped on one segment.

        public static long ApproachSeconds => (long)Math.Round(ApproachExactSeconds);
        public static long LandingSeconds => (long)Math.Round(LandingExactSeconds);
        public static long TakeoffSeconds => (long)Math.Round(TakeoffExactSeconds);
        public static long DepartedSeconds => (long)Math.Round(DepartedExactSeconds);

        /// <summary>Fraction of the landing phase spent from its start to touchdown.</summary>
        public static float TouchdownProgress =>
            (FinalGlideExactSeconds + FlareExactSeconds) / LandingExactSeconds;

        /// <summary>Fraction of the landing phase at which the flare begins.</summary>
        public static float FlareProgress => FinalGlideExactSeconds / LandingExactSeconds;

        /// <summary>Fraction of the takeoff phase at which the nose comes up.</summary>
        public static float RotateProgress => TakeoffRollExactSeconds / TakeoffExactSeconds;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Local(float t, float from, float to) =>
            to <= from ? 0f : Clamp01((t - from) / (to - from));

        /// <summary>
        /// Scheduled airspeed in knots at this point in the circuit — the single
        /// source of truth. The HUD readout, the tyre spin and the path curves all
        /// read this, so the number on screen is the number being flown.
        ///
        /// Lives here rather than in the flight path so it is covered by the headless
        /// harness: the speeds are the whole point of this model, and a figure the
        /// player can read should not be the one part nothing can test.
        /// </summary>
        public static float AirspeedKnots(AircraftPhase phase, float progress)
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
                    return Lerp(TouchdownKnots, 0f, Local(t, TouchdownProgress, 1f));
                case AircraftPhase.Takeoff:
                    if (t < RotateProgress)
                        return Lerp(0f, RotateKnots, Local(t, 0f, RotateProgress));
                    return Lerp(RotateKnots, InitialClimbKnots, Local(t, RotateProgress, 1f));
                case AircraftPhase.Departed:
                    return Lerp(InitialClimbKnots, ClimbOutKnots, t);
                default:
                    // Skipped ground phases: the aircraft is stopped on the rollout end.
                    return 0f;
            }
        }
    }
}
