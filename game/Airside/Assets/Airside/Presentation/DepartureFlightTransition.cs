using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Deterministic presentation heights and a position/velocity-continuous route handoff.</summary>
    public static class DepartureFlightTransition
    {
        public const double JoinSeconds = 120;

        public static double TakeoffHeight(AircraftPerformanceProfile profile, double progress)
        {
            var u = Math.Clamp((progress - profile.RotateProgress) / (1 - profile.RotateProgress), 0, 1);
            var seconds = profile.TakeoffSeconds * (1 - profile.RotateProgress);
            return Hermite(0, profile.TakeoffEndHeight, 0,
                profile.InitialClimbRateMetresPerSecond, seconds, u);
        }

        public static double DepartedHeight(AircraftPerformanceProfile profile, double progress) =>
            Hermite(profile.TakeoffEndHeight, profile.DepartedEndHeight,
                profile.InitialClimbRateMetresPerSecond, profile.ClimbOutRateMetresPerSecond,
                profile.DepartedSeconds, Math.Clamp(progress, 0, 1));

        public static bool UsesLocalClimbout(AircraftPerformanceProfile performance, double elapsed) =>
            elapsed <= performance.DepartedSeconds;

        public static double JoinDuration(AircraftPerformanceProfile performance, EnrouteProfile profile) =>
            Math.Min(JoinSeconds, Math.Max(1, profile.LegSeconds - performance.DepartedSeconds - 360));

        public static double OutboundHeight(AircraftPerformanceProfile performance, EnrouteProfile profile, double elapsed)
        {
            if (UsesLocalClimbout(performance, elapsed))
                return DepartedHeight(performance, elapsed / performance.DepartedSeconds);
            var exit = performance.DepartedSeconds;
            var routeHeight = profile.AltitudeFeetAt(elapsed) / EnrouteProfile.FeetPerMetre;
            var heightDelta = performance.DepartedEndHeight - profile.AltitudeFeetAt(exit) / EnrouteProfile.FeetPerMetre;
            var rateDelta = performance.ClimbOutRateMetresPerSecond
                - profile.VerticalSpeedFeetPerMinuteAt(exit) / (60 * EnrouteProfile.FeetPerMetre);
            return routeHeight + RouteOffset(elapsed - exit, JoinDuration(performance, profile), heightDelta, rateDelta);
        }

        /// <summary>Offset starts at the field exit with its velocity and fades to the timed route.
        /// Apply independently to world X/Y/Z. No frame history, save or schedule changes.</summary>
        public static double RouteOffset(double sinceExit, double seconds, double positionDelta, double velocityDelta)
        {
            var u = Math.Clamp(sinceExit / Math.Max(1, seconds), 0, 1);
            return (1 - 3*u*u + 2*u*u*u) * positionDelta
                + (u*u*u - 2*u*u + u) * Math.Max(1, seconds) * velocityDelta;
        }

        private static double Hermite(double from, double to, double startRate, double endRate,
            double seconds, double u) =>
            (2*u*u*u - 3*u*u + 1)*from + (-2*u*u*u + 3*u*u)*to
            + (u*u*u - 2*u*u + u)*seconds*startRate + (u*u*u - u*u)*seconds*endRate;
    }
}
