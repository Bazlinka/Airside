namespace Airside.Simulation
{
    /// <summary>Where a new player is in their first round trip.</summary>
    public enum GuideStep
    {
        PlanFirstFlight,
        WaitForDeparture,
        Departing,
        Away,
        Landing,
        ChooseStand,
        TaxiingIn,
        Complete
    }

    /// <summary>
    /// First-session guidance (PROJECT_PLAN step 3): walks a new player through one
    /// full trip. Derived from fleet state alone — no guide flags to save — so it is
    /// right after Continue and quietly finishes once any player aircraft has flown a
    /// trip.
    /// </summary>
    public static class FirstFlightGuide
    {
        public static GuideStep For(AirlineOperations operations, out FleetAircraft aircraft)
        {
            aircraft = null;
            if (operations?.PlayerAirline == null)
                return GuideStep.Complete;

            foreach (var candidate in operations.FleetOf(operations.PlayerAirline))
            {
                if (candidate.CompletedTrips > 0)
                    return GuideStep.Complete;
                aircraft ??= candidate;
            }

            if (aircraft == null)
                return GuideStep.Complete;

            return aircraft.State switch
            {
                FleetState.AtStand => aircraft.Scheduled.HasValue ? GuideStep.WaitForDeparture : GuideStep.PlanFirstFlight,
                FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff => GuideStep.Departing,
                FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound => GuideStep.Away,
                FleetState.HoldingForLanding or FleetState.Landing or FleetState.GoAround => GuideStep.Landing,
                FleetState.AwaitingStand => GuideStep.ChooseStand,
                FleetState.TaxiIn => GuideStep.TaxiingIn,
                _ => GuideStep.Complete
            };
        }
    }
}
