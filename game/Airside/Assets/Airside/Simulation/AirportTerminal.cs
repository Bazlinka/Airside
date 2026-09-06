namespace Airside.Simulation
{
    /// <summary>
    /// Foundation for the modular terminal: passenger-processing capacity
    /// separate from airfield stand geometry. Check-in desks cap how many
    /// scheduled flights per day the terminal can process; the baseline matches
    /// the two-stand schedule cap (12/day) so it does not bind until the
    /// airfield grows past it. A single buildable expansion (the first terminal
    /// module beyond the core check-in hall) raises the cap to match a
    /// three-stand airfield (18/day). Desk count is reconstructed by replaying
    /// the <c>expand-checkin</c> command — no save-schema field.
    /// </summary>
    public sealed class AirportTerminal
    {
        public const int BaselineCheckInDesks = 2;
        public const int MaximumCheckInDesks = 3;
        public const int FlightsPerDeskPerDayCap = 6;
        public const long CheckInHallExpansionCost = 6000;

        public int CheckInDesks { get; private set; } = BaselineCheckInDesks;

        /// <summary>Scheduled flights per day the terminal can process.</summary>
        public int PassengerCapacityPerDay => CheckInDesks * FlightsPerDeskPerDayCap;

        public bool HasExpandedCheckIn => CheckInDesks > BaselineCheckInDesks;

        public bool CanExpand => CheckInDesks < MaximumCheckInDesks;

        public bool ExpandCheckIn()
        {
            if (!CanExpand)
                return false;

            CheckInDesks++;
            return true;
        }
    }
}
