using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed class RouteProposal
    {
        public RouteProposal(string id, string airline, string destination, int flightsPerDay,
            long incomePerFlight, int reputationRequired, SimulationTime offeredAt, SimulationTime expiresAt)
        {
            Id = id;
            Airline = airline;
            Destination = destination;
            FlightsPerDay = flightsPerDay;
            IncomePerFlight = incomePerFlight;
            ReputationRequired = reputationRequired;
            OfferedAt = offeredAt;
            ExpiresAt = expiresAt;
        }

        public string Id { get; }
        public string Airline { get; }
        public string Destination { get; }
        public int FlightsPerDay { get; }
        public long IncomePerFlight { get; }
        public int ReputationRequired { get; }
        public SimulationTime OfferedAt { get; }
        public SimulationTime ExpiresAt { get; }

        public long SecondsRemaining(SimulationTime now) =>
            Math.Max(0, ExpiresAt.ElapsedSeconds - now.ElapsedSeconds);
    }

    public sealed class AcceptedRoute
    {
        public AcceptedRoute(string airline, string destination, int flightsPerDay, long incomePerFlight)
        {
            Airline = airline;
            Destination = destination;
            FlightsPerDay = flightsPerDay;
            IncomePerFlight = incomePerFlight;
        }

        public string Airline { get; }
        public string Destination { get; }
        public int FlightsPerDay { get; }
        public long IncomePerFlight { get; }
    }

    /// <summary>
    /// Airlines proposing scheduled services to the airport. One proposal stands at a
    /// time; it expires if the player does not accept it. Accepting a route adds a
    /// recurring payment for every completed flight.
    ///
    /// Proposal content is a pure function of the simulated timeline (not the random
    /// source), so it is deterministic, identical under live play and offline
    /// catch-up, and does not disturb the gameplay random sequence.
    /// </summary>
    public sealed class AirportRoutes
    {
        public const long FirstOfferAfterSeconds = 25;
        public const long OfferIntervalSeconds = 130;
        public const long OfferWindowSeconds = 80;

        private static readonly string[] Airlines =
        {
            "Coastline Regional", "Emu Air", "Southern Cross Link", "Gulf Connect", "Redgum Air"
        };

        private static readonly string[] Destinations =
        {
            "Adelaide", "Whyalla", "Mount Gambier", "Ceduna", "Broken Hill", "Melbourne"
        };

        private readonly List<AcceptedRoute> _accepted = new();
        private int _generated;
        private SimulationTime _nextOfferAt;

        public AirportRoutes(SimulationTime now)
        {
            _nextOfferAt = now.Advance(FirstOfferAfterSeconds);
        }

        public RouteProposal Pending { get; private set; }
        public IReadOnlyList<AcceptedRoute> Accepted => _accepted;
        public long IncomePerFlight { get; private set; }
        public int OffersMade => _generated;

        public void Update(SimulationTime now)
        {
            if (Pending != null && now.CompareTo(Pending.ExpiresAt) >= 0)
                Pending = null;

            // Replay every offer window due by `now`, so a large time step lands in
            // the same state as second-by-second stepping.
            while (Pending == null && now.CompareTo(_nextOfferAt) >= 0)
            {
                var offeredAt = _nextOfferAt;
                Pending = Generate(offeredAt);
                _nextOfferAt = offeredAt.Advance(OfferIntervalSeconds);

                if (now.CompareTo(Pending.ExpiresAt) >= 0)
                    Pending = null;
            }
        }

        /// <summary>
        /// Accept the standing proposal at the given reputation. Fails when there is
        /// no proposal, or the airport's reputation is below what the route requires.
        /// The per-flight payment locks in a reputation bonus at acceptance time.
        /// </summary>
        public bool Accept(SimulationTime now, int reputationScore, long reputationBonus = 0)
        {
            if (Pending == null || reputationScore < Pending.ReputationRequired)
                return false;

            var income = Pending.IncomePerFlight + Math.Max(0, reputationBonus);
            _accepted.Add(new AcceptedRoute(Pending.Airline, Pending.Destination, Pending.FlightsPerDay, income));
            IncomePerFlight += income;
            Pending = null;
            return true;
        }

        private RouteProposal Generate(SimulationTime now)
        {
            _generated++;
            var n = _generated;
            var airline = Airlines[n % Airlines.Length];
            var destination = Destinations[(n * 3 + 1) % Destinations.Length];
            var flightsPerDay = 1 + (n % 3);
            var incomePerFlight = 150 + (n % 5) * 60;
            var reputationRequired = 25 + (n % 4) * 15; // 25, 40, 55, 70, …
            return new RouteProposal(
                $"PROP-{n}", airline, destination, flightsPerDay, incomePerFlight, reputationRequired,
                now, now.Advance(OfferWindowSeconds));
        }
    }
}
