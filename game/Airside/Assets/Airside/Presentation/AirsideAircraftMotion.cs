using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Turns the simulation's whole-second phase clock into continuous motion.
    ///
    /// The simulation deliberately ticks exactly one simulated second at a time, so
    /// <c>AircraftOperation.PhaseProgress</c> only ever changes 1/duration at a time.
    /// Sampling that straight from the renderer gives a 1 Hz staircase: 59 frames of
    /// nothing, then a jump. At 4x the jumps are four times longer, which is why the
    /// faster speed looked so much worse than the slower one.
    ///
    /// Progress is a pure function of time, so presentation can evaluate it at the
    /// fractional presentation clock instead. That is exact rather than interpolated:
    /// no lag, no extrapolation overshoot, and no per-aircraft sample history. The
    /// simulation still owns every transition — this only reads the clock it publishes.
    /// </summary>
    public static class AirsideAircraftMotion
    {
        /// <summary>
        /// Phase progress at a fractional presentation time, from the phase start second
        /// the simulation published. Clamped to 1, so presentation can never render an
        /// aircraft past the end of a phase the simulation has not left yet — a departure
        /// held for traffic sits at the hold-short point rather than sliding onto the
        /// runway ahead of its clearance.
        /// </summary>
        public static float PhaseProgress(double preciseSeconds, long phaseStartedAtSeconds, float phaseSeconds)
        {
            if (phaseSeconds <= 0f)
                return 1f;

            return Mathf.Clamp01((float)((preciseSeconds - phaseStartedAtSeconds) / phaseSeconds));
        }
    }
}
