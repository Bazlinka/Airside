using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Player toasts for a flight that has landed away, and for one that is home and free to send again (ADR 0164).</summary>
    public static class FlightNotices
    {
        public static string LandedAtDestination(string registration, string destination)
        {
            var where = string.IsNullOrEmpty(destination) ? "its destination" : destination;
            return $"{registration} has landed at {where}.";
        }

        /// <summary>
        /// Whether a player toast for an event in this state belongs to the return flight (number + 1).
        /// The AtDestination event is the OUTBOUND flight touching down at the outstation, so it keeps
        /// the outbound number; only events from the flight home onward use the return number.
        /// </summary>
        public static bool IsReturnLegEvent(FleetState state) =>
            state is FleetState.Inbound or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn or FleetState.AtStand;

        public static string ReturnedHomeAwaitingDispatch(string registration) =>
            $"{registration} has returned home and is awaiting dispatch.";
    }
}
