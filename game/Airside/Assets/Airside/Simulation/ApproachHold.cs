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
        /// Number-one on short final. Later slots stack back along the
        /// approach so arrivals queue in wait order, not by registration hash.
        /// </summary>
        public const float FirstHoldProgress = 0.80f;
        public const float HoldSlotStep = 0.10f;
        public const float LastHoldProgress = 0.36f;

        /// <summary>
        /// Short final on the assigned runway, not the land-side racetrack.
        /// Slot 0 is next to land; later slots sit further out.
        /// </summary>
        public static double HoldingFinalProgress(int queueSlot)
        {
            var slot = queueSlot < 0 ? 0 : queueSlot;
            var t = FirstHoldProgress - slot * HoldSlotStep;
            return t < LastHoldProgress ? LastHoldProgress : t;
        }

        /// <summary>
        /// Cleared aircraft fly the last piece of final (slot 0). Prefer
        /// <see cref="HoldingFinalProgress(int)"/> while they are still holding.
        /// </summary>
        public static double HoldingFinalProgress(string registration) =>
            HoldingFinalProgress(0);

        public static long RemainingFinalSeconds(long approachSeconds, int queueSlot)
        {
            var left = 1.0 - HoldingFinalProgress(queueSlot);
            var seconds = (long)Math.Round(Math.Max(0, approachSeconds) * left);
            return seconds < 8 ? 8 : seconds;
        }

        public static long RemainingFinalSeconds(long approachSeconds, string registration) =>
            RemainingFinalSeconds(approachSeconds, 0);
    }
}
