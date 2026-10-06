using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>What a type fetches when sold: its list price times <see cref="ResaleFraction"/>; zero if unlisted.</summary>
        public static long ResaleValue(AircraftType type) =>
            type != null && AircraftAcquisition.TryFor(type, out var offer)
                ? (long)Math.Round(offer.Price * ResaleFraction)
                : 0L;

        /// <summary>
        /// Sells an aircraft based away from Adelaide. Same market as <see cref="SellAircraft"/>: a fraction
        /// of list price, and only while it is idle (a planned flight or a check in progress would be
        /// left pointing at a registration that no longer exists).
        /// </summary>
        public CommandResult SellOutstationAircraft(string registration)
        {
            var aircraft = _outstationFleet.Find(a => a.Registration == registration);
            if (aircraft == null)
                return CommandResult.Refused("No such aircraft.");
            if (aircraft.HasFlight)
                return CommandResult.Refused($"{aircraft.Registration} has a flight planned. Let it finish first.");
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
                return CommandResult.Refused($"{aircraft.Registration} is in its check.");
            if (!AircraftAcquisition.TryFor(aircraft.Type, out _))
                return CommandResult.Refused($"Nobody is buying {aircraft.Type.Name}s.");

            CareerState.RefundDispatch(ResaleValue(aircraft.Type));
            _repeatSchedules.RemoveAll(p => p.Registration == aircraft.Registration);
            _outstationFleet.Remove(aircraft);
            return CommandResult.Ok;
        }

        /// <summary>The fee for flying an outstation aircraft in to Adelaide: what one dispatch over that leg costs.</summary>
        public long RelocationCost(OutstationAircraft aircraft)
        {
            if (aircraft == null || !DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin))
                return 0L;
            return DispatchCost(aircraft.Type, origin.DistanceKmTo(Home));
        }

        /// <summary>
        /// Stands the player could still park an arriving aircraft of this type on at Adelaide: free, usable by
        /// the player's base, and not already owed to a player aircraft of the same class that is out and will
        /// need one when it returns. Zero or less means a ferry would land with nowhere to go.
        /// </summary>
        internal int PlayerStandsAvailableFor(AircraftType type)
        {
            if (type == null || CareerState == null || !PlayerBase.Supports(CareerState.BaseLevel, type))
                return 0;
            var free = 0;
            foreach (var stand in _stands)
            {
                if (!StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (!PlayerBase.CanUseStand(CareerState.BaseLevel, type, stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, null))
                    continue;
                free++;
            }

            var owed = 0;
            foreach (var other in _fleet)
                if (other.Airline.IsPlayer && !other.Type.IsRotorcraft && !HoldsStand(other)
                    && NeedsTerminalGate(other.Type) == NeedsTerminalGate(type))
                    owed++;
            return free - owed;
        }

        /// <summary>
        /// Read-only twin of <see cref="RelocateToAdelaide"/>: the first reason the move would be refused, in the
        /// same order, so a button is drawn actionable only when the command would be accepted.
        /// </summary>
        public bool CanRelocateToAdelaide(string registration, out string reason)
        {
            reason = string.Empty;
            var aircraft = _outstationFleet.Find(a => a.Registration == registration);
            if (aircraft == null || CareerState == null || PlayerAirline == null)
            {
                reason = "No such aircraft.";
                return false;
            }
            if (aircraft.HasFlight)
            {
                reason = $"{aircraft.Registration} has a flight planned. Let it finish first.";
                return false;
            }
            if (aircraft.InCheck(_processedTo.ElapsedSeconds))
            {
                reason = $"{aircraft.Registration} is in its check.";
                return false;
            }
            if (!PlayerBase.Supports(CareerState.BaseLevel, aircraft.Type))
            {
                var needed = PlayerBase.For(PlayerBase.RequiredLevel(aircraft.Type));
                reason = $"{Article.CapitalA(aircraft.Type.Name)} needs the {needed.Title}. Expand your Adelaide base first.";
                return false;
            }
            if (!DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin))
            {
                reason = "Unknown base.";
                return false;
            }
            var km = origin.DistanceKmTo(Home);
            // Range only: route bands rank destinations for filed services, and Adelaide, the home, has no band.
            if (!aircraft.Type.CanReach(km))
            {
                reason = $"{Article.CapitalA(aircraft.Type.Name)} cannot fly the {km:N0} km from {aircraft.BaseCode} to Adelaide.";
                return false;
            }
            var live = 0;
            foreach (var other in _fleet)
                if (other.Airline.IsPlayer)
                    live++;
            if (live >= CareerState.Base.FleetCapacity)
            {
                reason = $"The {CareerState.Base.Title} holds {CareerState.Base.FleetCapacity} aircraft. Expand your Adelaide base or sell one first.";
                return false;
            }
            if (PlayerStandsAvailableFor(aircraft.Type) <= 0)
            {
                reason = $"No stand is free at Adelaide for {Article.CapitalA(aircraft.Type.Name)}.";
                return false;
            }
            var cost = RelocationCost(aircraft);
            if (!CareerState.CanAfford(cost))
            {
                reason = $"Moving {aircraft.Registration} to Adelaide costs ${cost:N0}. You have ${CareerState.Funds:N0}.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Flies an outstation aircraft in to Adelaide to join the live fleet (ADR 0239). It arrives as a real
        /// inbound flight keeping its registration, flights, wear and logbook, and earns nothing on landing.
        /// </summary>
        public CommandResult RelocateToAdelaide(string registration)
        {
            if (!CanRelocateToAdelaide(registration, out var reason))
                return CommandResult.Refused(reason);

            var outstation = _outstationFleet.Find(a => a.Registration == registration);
            DestinationCatalogue.TryFind(outstation.BaseCode, out var origin);
            var km = origin.DistanceKmTo(Home);
            var cost = DispatchCost(outstation.Type, km);
            if (!CareerState.TryChargeDispatch(cost))
                return CommandResult.Refused($"Moving {registration} to Adelaide costs ${cost:N0}. You have ${CareerState.Funds:N0}.");

            var aircraft = new FleetAircraft(outstation.Registration, PlayerAirline, outstation.Type, default, _processedTo);
            aircraft.Owner = this;
            aircraft.CurrentDestination = origin;
            aircraft.IsFerry = true;
            aircraft.CompletedTrips = outstation.CompletedServices;
            aircraft.RotationsSinceCheck = outstation.RotationsSinceCheck;
            aircraft.RestoreHistory(new SimulationTime(outstation.JoinedAtSeconds), false, outstation.LifetimeRevenue,
                outstation.HistoryFlights, outstation.RouteHistory);
            aircraft.Restore(FleetState.Inbound, _processedTo,
                _processedTo.Advance(LegTiming.AirborneSeconds(km, outstation.Type)));

            _repeatSchedules.RemoveAll(p => p.Registration == outstation.Registration);
            _outstationFleet.Remove(outstation);
            _fleet.Add(aircraft);
            AdvanceCareer();
            return CommandResult.Ok;
        }
    }
}
