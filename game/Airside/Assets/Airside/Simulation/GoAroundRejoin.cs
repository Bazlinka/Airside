namespace Airside.Simulation
{
    /// <summary>
    /// How far through smoothing the cut from a go-around's own racetrack back onto the
    /// ordinary approach queue position (ADR 0068). The two systems have no shared
    /// parameterisation to interpolate through physically — a fixed-radius circuit at
    /// <see cref="CircuitTraffic.CircuitHeightMetres"/> versus a queue-slot-pinned point on
    /// the glideslope (<see cref="ApproachHold.HoldingFinalProgress(int)"/>) — so this blends
    /// position only, over a short cosmetic window, rather than simulating a real rejoin.
    /// Not physically accurate, but a multi-hundred-metre instant jump reads as a bug, and a
    /// several-second slide reads as an aircraft banking hard to rejoin the approach.
    /// </summary>
    public static class GoAroundRejoin
    {
        /// <summary>How long after entering <c>HoldingForLanding</c> the blend runs.</summary>
        public const double BlendSeconds = 6.0;

        /// <summary>
        /// 0 at the instant the go-around ends, 1 once fully settled onto the pinned holding
        /// position — eased, not linear, so the aircraft appears to slow into place rather
        /// than stop dead. Clamped outside [0, BlendSeconds]; the caller stops blending
        /// altogether once this reaches 1 rather than keep evaluating it forever.
        /// </summary>
        public static double Blend01(double secondsSinceHoldingBegan)
        {
            if (secondsSinceHoldingBegan <= 0.0)
                return 0.0;
            if (secondsSinceHoldingBegan >= BlendSeconds)
                return 1.0;
            var t = secondsSinceHoldingBegan / BlendSeconds;
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
