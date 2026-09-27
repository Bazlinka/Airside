namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0126 — when an AI aircraft on stand has its fuel truck and baggage train alongside.
    /// The player's own turnaround is sequenced by <c>DeparturePrep</c>; AI turnarounds are not
    /// simulated, so this gives them a believable, deterministic window: the vehicles arrive a few
    /// minutes after the aircraft parks and leave well before it pushes. Pure.
    /// </summary>
    public static class ApronServiceSchedule
    {
        public const double FuelArrivesAfterSeconds = 4 * 60;
        public const double FuelLeavesBeforeSeconds = 14 * 60;
        public const double BaggageArrivesAfterSeconds = 90;
        public const double BaggageLeavesBeforeSeconds = 8 * 60;
        /// <summary>Without a booked departure the service simply runs this long.</summary>
        public const double UnbookedServiceSeconds = 25 * 60;

        public static bool FuelAlongside(double secondsOnStand, double? secondsToDeparture) =>
            Alongside(secondsOnStand, secondsToDeparture, FuelArrivesAfterSeconds, FuelLeavesBeforeSeconds);

        public static bool BaggageAlongside(double secondsOnStand, double? secondsToDeparture) =>
            Alongside(secondsOnStand, secondsToDeparture, BaggageArrivesAfterSeconds, BaggageLeavesBeforeSeconds);

        private static bool Alongside(double onStand, double? toDeparture, double arriveAfter, double leaveBefore)
        {
            if (onStand < arriveAfter)
                return false;
            return toDeparture.HasValue
                ? toDeparture.Value > leaveBefore
                : onStand < arriveAfter + UnbookedServiceSeconds;
        }
    }
}
