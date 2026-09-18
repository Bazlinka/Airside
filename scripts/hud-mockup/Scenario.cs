using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;

namespace Airside.Tools.HudMockup;

/// <summary>
/// A real airline played forward headlessly: three aircraft, a signed Kingscote contract
/// and a few hours of genuine rotations, stopped mid-turnaround so the mockups show live
/// prep, live money and a live movement board rather than authored placeholder strings.
/// </summary>
public sealed class Scenario
{
    private readonly List<FleetAircraft> _playerFleet = new();

    private Scenario(AirlineOperations operations, ManualSimulationClock clock, string selected,
        List<OperationsEventLine> events)
    {
        Operations = operations;
        Clock = clock;
        SelectedRegistration = selected;
        EventHistory = events;
        foreach (var aircraft in operations.FleetOf(operations.PlayerAirline))
            _playerFleet.Add(aircraft);
    }

    public AirlineOperations Operations { get; }
    public ManualSimulationClock Clock { get; }
    public SimulationTime Now => Clock.Now;
    public string SelectedRegistration { get; }
    public IReadOnlyList<OperationsEventLine> EventHistory { get; }
    public IReadOnlyList<FleetAircraft> PlayerFleet => _playerFleet;

    public FleetAircraft Selected
    {
        get
        {
            foreach (var aircraft in _playerFleet)
                if (aircraft.Registration == SelectedRegistration)
                    return aircraft;
            return _playerFleet.Count > 0 ? _playerFleet[0] : null;
        }
    }

    public static Scenario Play()
    {
        var clock = new ManualSimulationClock(new SimulationTime(0));
        var player = Airline.Player("Soak Air", "#1F3A93");
        var operations = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913), player);
        operations.Clock = AirlineClock.Default;

        AddPlayerAircraft(operations, player, "VH-SUN", AircraftType.Atr42);
        AddPlayerAircraft(operations, player, "VH-RGN", AircraftType.Dash8Q400);
        operations.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro);

        DestinationCatalogue.TryFind("KGC", out var kingscote);
        DestinationCatalogue.TryFind("PLO", out var portLincoln);
        var rotation = new[] { kingscote, kingscote, portLincoln };

        // A morning of real rotations, a minute at a time, keeping every free aircraft working.
        const long stopBooking = 4 * 3600;
        var index = 0;
        var t = 0L;
        for (; t <= stopBooking; t += 60)
        {
            clock.Set(new SimulationTime(t));
            operations.Update();
            foreach (var aircraft in operations.FleetOf(player))
            {
                if (aircraft.State != FleetState.AtStand || aircraft.Scheduled.HasValue)
                    continue;
                var destination = rotation[index++ % rotation.Length];
                operations.ScheduleDeparture(aircraft, destination,
                    clock.Now.Advance(DeparturePrep.LeadSeconds(aircraft.Type) + 120));
            }
        }

        // Then wait, without booking anything else, for an aircraft to come home and park.
        FleetAircraft subject = null;
        while (subject == null && t < 9 * 3600)
        {
            t += 60;
            clock.Set(new SimulationTime(t));
            operations.Update();
            subject = FindIdle(operations, player);
        }

        // The flight the mockups are about: booked, then run on 263 s, which is exactly far
        // enough into an ATR's 285 s turnaround for fuel and catering to be done and
        // boarding to be 82% through.
        if (subject != null)
        {
            operations.ScheduleDeparture(subject, kingscote, new SimulationTime(t + 8 * 60));
            clock.Set(new SimulationTime(t + 263));
            operations.Update();
        }

        return new Scenario(operations, clock,
            subject?.Registration ?? operations.FleetOf(player).First().Registration,
            RecentPlayerEvents(operations));
    }

    public string Describe()
    {
        var career = Operations.CareerState;
        var contract = career.ActiveContract;
        return $"Soak Air at {Operations.Clock.TimeText(Now)}: ${career.Funds:N0}, "
               + $"{career.Reliability}% reliability, {career.Tier} tier, "
               + $"{career.CompletedPlayerRotations} rotations flown"
               + (contract == null ? ", no active contract" : $", {contract.DefinitionId} at {contract.CompletedRotations}");
    }

    private static void AddPlayerAircraft(AirlineOperations operations, Airline player, string registration,
        AircraftType type)
    {
        foreach (var stand in operations.FreeStandsFor(type))
        {
            operations.AddAircraft(player, registration, type, stand);
            return;
        }
    }

    private static FleetAircraft FindIdle(AirlineOperations operations, Airline player)
    {
        foreach (var aircraft in operations.FleetOf(player))
            if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                return aircraft;
        return null;
    }

    /// <summary>The player's own movements, newest first — what the event-history strip shows.</summary>
    private static List<OperationsEventLine> RecentPlayerEvents(AirlineOperations operations)
    {
        var lines = new List<OperationsEventLine>();
        var recent = operations.RecentEvents;
        for (var i = recent.Count - 1; i >= 0 && lines.Count < 8; i--)
        {
            var e = recent[i];
            if (!e.Aircraft.Airline.IsPlayer)
                continue;
            lines.Add(new OperationsEventLine(operations.Clock.TimeText(e.At),
                $"{e.Aircraft.Registration} {Describe(e)}"));
        }

        return lines;
    }

    private static string Describe(FleetEvent e) => e.State switch
    {
        FleetState.TaxiOut => $"pushed back for {e.Aircraft.CurrentDestination?.Name ?? "its route"}",
        FleetState.TakingOff => $"departed for {e.Aircraft.CurrentDestination?.Name ?? "its route"}",
        FleetState.AtDestination => $"landed at {e.Aircraft.CurrentDestination?.Name ?? "its destination"}",
        FleetState.Inbound => "is on the way home",
        FleetState.Landing => "is landing at Adelaide",
        FleetState.TaxiIn => "is taxiing in",
        FleetState.AtStand => "is back on stand",
        _ => e.State.ToString()
    };
}
