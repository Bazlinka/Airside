using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Where a fleet arrival sits on final, and when the number-two compress
    /// (a parallel lane holding short of the flare) is allowed to fire.
    /// Fleet arrivals carry a ~0.14 m centreline drift — that is not a second
    /// aircraft, and compressing it made the last seconds of every landing crawl.
    /// </summary>
    public static class ApproachHold
    {
        public const float NumberTwoHoldProgress = 0.82f;
        public const float NumberTwoCreep = 0.08f;

        /// <summary>A real parallel final is metres off the centreline, not centimetres.</summary>
        public const float ParallelLaneMetres = 2f;

        public static float ApproachVisualProgress(float progress, float laneOffset)
        {
            var t = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            if (t <= NumberTwoHoldProgress || Math.Abs(laneOffset) < ParallelLaneMetres)
                return t;
            return NumberTwoHoldProgress + (t - NumberTwoHoldProgress) * NumberTwoCreep;
        }

        /// <summary>
        /// Short final on the assigned runway, not the land-side racetrack.
        /// Slot 0–2 stacks a short queue without leaving the gulf (05) or
        /// the north-east (23).
        /// </summary>
        public static double HoldingFinalProgress(string registration)
        {
            var slot = 0;
            if (!string.IsNullOrEmpty(registration))
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var ch in registration)
                        hash = hash * 31 + ch;
                    slot = Math.Abs(hash) % 3;
                }
            }

            return 0.62 + slot * 0.06;
        }

        public static long RemainingFinalSeconds(long approachSeconds, string registration)
        {
            var left = 1.0 - HoldingFinalProgress(registration);
            var seconds = (long)Math.Round(Math.Max(0, approachSeconds) * left);
            return seconds < 8 ? 8 : seconds;
        }
    }
}
