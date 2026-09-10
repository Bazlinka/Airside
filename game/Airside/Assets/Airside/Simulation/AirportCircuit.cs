namespace Airside.Simulation
{
    /// <summary>
    /// Bare-field circuit: land, roll, take off, leave the field, next arrival.
    /// Taxi, stand and pushback are skipped so the loop stays on the runway.
    /// </summary>
    public static class AirportCircuit
    {
        /// <summary>
        /// Production default is true. EditMode tests that still exercise the
        /// stand / taxi loop set this false in their fixture SetUp.
        /// </summary>
        public static bool SkipGroundTaxi = true;

        public const long ApproachSeconds = 48;
        public const long LandingSeconds = 20;
        public const long TaxiSkipSeconds = 1;
        public const long TakeoffSeconds = 28;
        public const long DepartureFlyOutSeconds = 22;

        public static bool IsSkippedGroundPhase(AircraftPhase phase) =>
            SkipGroundTaxi
            && phase is AircraftPhase.TaxiIn
                or AircraftPhase.AtStand
                or AircraftPhase.Pushback
                or AircraftPhase.TaxiOut;

        public static long DurationSeconds(AircraftPhase phase)
        {
            if (IsSkippedGroundPhase(phase))
                return TaxiSkipSeconds;

            return phase switch
            {
                AircraftPhase.Approach => SkipGroundTaxi ? ApproachSeconds : 20,
                AircraftPhase.Landing => SkipGroundTaxi ? LandingSeconds : 12,
                AircraftPhase.TaxiIn => 25,
                AircraftPhase.AtStand => 45,
                AircraftPhase.Pushback => 12,
                AircraftPhase.TaxiOut => 25,
                AircraftPhase.Takeoff => SkipGroundTaxi ? TakeoffSeconds : 15,
                _ => long.MaxValue
            };
        }
    }
}
