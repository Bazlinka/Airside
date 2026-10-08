using System;

namespace Airside.Presentation
{
    /// <summary>Continuous presentation of an existing inbound delay, independent of runway clearance.</summary>
    public static class ArrivalHoldingTrack
    {
        public const double MinimumHeight = 1500.0;
        public const double ClimbMetresPerSecond = 5.0;

        public static bool Required(bool established, bool inbound, bool hasEta, double finalMetres) =>
            established && inbound && (!hasEta || finalMetres > ArrivalApproach.ShowMetres);

        /// <summary>Start tangent to the flown final; climb once, then orbit without resetting height.</summary>
        public static void Offset(double seconds, double speed, double entryHeight,
            double forwardX, double forwardZ, out double x, out double y, out double z)
        {
            var elapsed = Math.Max(0.0, seconds);
            var metresPerSecond = Math.Max(1.0, speed);
            // At most about 25 degrees bank, including the fastest arrival types.
            var radius = Math.Max(1800.0, metresPerSecond * metresPerSecond / 4.57);
            var angle = elapsed * metresPerSecond / radius;
            var along = radius * Math.Sin(angle);
            var across = radius * (1.0 - Math.Cos(angle));
            x = forwardX * along - forwardZ * across;
            z = forwardZ * along + forwardX * across;
            y = Math.Min(Math.Max(0.0, MinimumHeight - entryHeight), elapsed * ClimbMetresPerSecond);
        }
    }
}
