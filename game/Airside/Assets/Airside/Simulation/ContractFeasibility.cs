using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// ADR 0138 — can a contract be flown before its deadline? One flight is both legs, the
    /// destination turnaround, the player's prep at the current base and about 20 minutes of taxi and
    /// runway. The market only offers what one aircraft can fly with a quarter to spare, and Accept
    /// refuses what the airline can no longer finish. Pure.
    /// </summary>
    public static class ContractFeasibility
    {
        /// <summary>Taxi out, the runway, taxi in: a round figure for both ends of a flight.</summary>
        public const long GroundSeconds = 20 * 60;

        /// <summary>The market leaves this much spare: a contract needs 1.25× its minimum time.</summary>
        public const double Margin = 1.25;

        /// <summary>The tightest a single flight can be, from booking to parked back at Adelaide.</summary>
        public static long FlightSeconds(AircraftType type, Destination destination, PlayerBaseLevel baseLevel)
        {
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(destination);
            return 2 * LegTiming.AirborneSeconds(km, type) + AirlineOperations.DestinationTurnaroundSeconds
                   + DeparturePrep.TotalSeconds(type, baseLevel) + GroundSeconds;
        }

        /// <summary>Every flight of the contract with one aircraft, back to back.</summary>
        public static long MinimumSeconds(AircraftType type, Destination destination, int rotations,
            PlayerBaseLevel baseLevel) =>
            FlightSeconds(type, destination, baseLevel) * Math.Max(1, rotations);

        /// <summary>The shortest deadline the market may give this work: the minimum plus the margin, in whole hours.</summary>
        public static long FairDeadlineSeconds(AircraftType type, Destination destination, int rotations,
            PlayerBaseLevel baseLevel)
        {
            var needed = (long)Math.Ceiling(MinimumSeconds(type, destination, rotations, baseLevel) * Margin);
            return (needed + 3599) / 3600 * 3600;
        }

        /// <summary>True when the offer leaves the margin, or has no deadline.</summary>
        public static bool IsFair(RouteContractDefinition offer, PlayerBaseLevel baseLevel)
        {
            if (offer == null || !offer.HasDeadline)
                return true;
            if (!DestinationCatalogue.TryFind(offer.DestinationCode, out var destination))
                return false;
            return MinimumSeconds(offer.EligibleType, destination, offer.RequiredRotations, baseLevel) * Margin
                   <= offer.DeadlineSeconds;
        }

        /// <summary>
        /// Can the airline still fly <paramref name="rotationsLeft"/> flights in <paramref name="secondsLeft"/>?
        /// <paramref name="aircraft"/> eligible aircraft share them, and the first can start in
        /// <paramref name="firstFreeInSeconds"/>. No margin: this is the hard limit Accept checks.
        /// </summary>
        public static bool CanStillFinish(AircraftType type, Destination destination, int rotationsLeft,
            PlayerBaseLevel baseLevel, int aircraft, long firstFreeInSeconds, long secondsLeft)
        {
            if (rotationsLeft <= 0)
                return true;
            if (aircraft <= 0)
                return false;
            var perAircraft = (rotationsLeft + aircraft - 1) / aircraft;
            return Math.Max(0, firstFreeInSeconds) + FlightSeconds(type, destination, baseLevel) * perAircraft
                   <= secondsLeft;
        }
    }
}
