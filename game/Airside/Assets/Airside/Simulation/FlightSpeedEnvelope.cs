using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// What speeds an aircraft may fly and how fast it can change them. Planning values built from each type's
    /// own approach speed, not dispatch data. Minimum speed rises with bank (load factor 1/cos), maximum speed is the
    /// game's 250 kt CAS rule below 10,000 ft, and the rate of speed change depends on the flight-path angle: a
    /// climb eats acceleration and a descent resists slowing down (g * sin gamma).
    /// </summary>
    public static class FlightSpeedEnvelope
    {
        public const double Gravity = 9.80665;
        public const double KnotsToMetresPerSecond = 0.514444;
        /// <summary>Lowest sustained margin over the 1g stall speed in the landing configuration; touchdown sits near 1.1 to 1.2 Vs0, Vref at 1.3.</summary>
        public const double LandingMargin = 1.1;
        /// <summary>Clean wing stalls about 20 % faster than the landing configuration; the margin is larger.</summary>
        public const double CleanStallRatio = 1.2;
        public const double CleanMargin = 1.3;
        public const double MaxCasBelowTenThousandKnots = 250;

        /// <summary>1g stall speed, landing configuration. Vref is about 1.3 Vs0.</summary>
        public static double StallKnots(AircraftType type) => AircraftPerformance.For(type).ApproachKnots / 1.3;

        /// <summary>Slowest speed to fly, in knots; stall speed grows with the square root of the load factor.</summary>
        public static double MinimumKnots(AircraftType type, double bankDegrees, bool landingConfiguration)
        {
            var bank = Math.Min(Math.Abs(bankDegrees), 60.0) * Math.PI / 180.0;
            var load = 1.0 / Math.Cos(bank);
            var stall = StallKnots(type) * (landingConfiguration ? 1.0 : CleanStallRatio);
            return stall * (landingConfiguration ? LandingMargin : CleanMargin) * Math.Sqrt(load);
        }

        /// <summary>Fastest calibrated speed allowed at this altitude (feet above sea level).</summary>
        public static double MaximumCasKnots(AircraftType type, double altitudeFeet)
        {
            var cap = altitudeFeet <= 10000 ? MaxCasBelowTenThousandKnots : FlightOperatingProfile.For(type).ClimbCas + 20;
            return Math.Max(cap, MinimumKnots(type, 0, false));
        }

        private static bool IsJet(AircraftType type) => FlightOperatingProfile.For(type).CruiseMach > 0;

        /// <summary>Greatest acceleration in m/s^2 at <paramref name="pathAngleDegrees"/> (positive climbing).</summary>
        public static double MaxAccelerationMetresPerSecond2(AircraftType type, double pathAngleDegrees)
        {
            var level = IsJet(type) ? 1.1 : 0.8;
            return Math.Max(0.05, level - Gravity * Math.Sin(pathAngleDegrees * Math.PI / 180.0));
        }

        /// <summary>Greatest deceleration (positive number) in m/s^2; descending (negative angle) makes slowing harder.</summary>
        public static double MaxDecelerationMetresPerSecond2(AircraftType type, double pathAngleDegrees, bool drag)
        {
            var level = (IsJet(type) ? 0.7 : 0.6) + (drag ? 0.6 : 0.0);
            return Math.Max(0.05, level + Gravity * Math.Sin(pathAngleDegrees * Math.PI / 180.0));
        }

        /// <summary>Move <paramref name="current"/> toward <paramref name="target"/> without exceeding the type's capability.</summary>
        public static double Step(AircraftType type, double currentKnots, double targetKnots, double seconds,
            double pathAngleDegrees, bool drag)
        {
            var maxUp = MaxAccelerationMetresPerSecond2(type, pathAngleDegrees) / KnotsToMetresPerSecond * seconds;
            var maxDown = MaxDecelerationMetresPerSecond2(type, pathAngleDegrees, drag) / KnotsToMetresPerSecond * seconds;
            var delta = targetKnots - currentKnots;
            return currentKnots + Math.Min(maxUp, Math.Max(-maxDown, delta));
        }
    }
}
