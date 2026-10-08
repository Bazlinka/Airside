using System;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>
        /// Read-only twin of <see cref="BuyAircraft"/> and <see cref="BuyAircraftAtOutstation"/>: the first reason a
        /// purchase of <paramref name="type"/> delivered to <paramref name="baseCode"/> would be refused, in the
        /// same order and words, or null when it would be accepted. The Fleet market draws a card as buyable only
        /// when this is null. <paramref name="ignoreFunds"/> skips the cash check, so the market can tell "locked"
        /// from "locked behind money".
        /// </summary>
        public string PurchaseRefusal(AircraftType type, string baseCode, bool ignoreFunds = false)
        {
            if (PlayerAirline == null || CareerState == null)
                return "No player airline.";
            var atHome = string.IsNullOrEmpty(baseCode) || baseCode == Home.Code;
            // The outstation command asks about the base before the type, and words the type refusal its own way.
            if (!atHome && !CareerState.HasOutstationBase(baseCode))
                return "Open that base first.";
            if (type == null || !AircraftAcquisition.TryFor(type, out var offer))
                return atHome ? "That type is not for sale." : "That aircraft is not for sale.";

            var costText = $"{Article.CapitalA(type.Name)} costs ${offer.Price:N0}. You have ${CareerState.Funds:N0}.";
            if (atHome)
            {
                var owned = 0;
                foreach (var aircraft in _fleet)
                    if (aircraft.Airline.IsPlayer)
                        owned++;
                if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                    return $"Your fleet is full at {AircraftAcquisition.MaxPlayerAircraft} aircraft.";
                if (owned >= CareerState.Base.FleetCapacity)
                    return $"The {CareerState.Base.Title} holds {CareerState.Base.FleetCapacity} aircraft. Expand your Adelaide base first.";
                if (!PlayerBase.Supports(CareerState.BaseLevel, type))
                {
                    var needed = PlayerBase.For(PlayerBase.RequiredLevel(type));
                    return $"{Article.CapitalA(type.Name)} needs the {needed.Title}. Expand your Adelaide base first.";
                }
                if (CareerState.Tier < offer.RequiredTier)
                    return $"{Article.CapitalA(type.Name)} needs the {offer.RequiredTier} tier.";
                if (CareerState.Reliability < offer.RequiredReliability)
                    return $"{Article.CapitalA(type.Name)} needs {offer.RequiredReliability}% reliability.";
                if (CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                    return $"{Article.CapitalA(type.Name)} needs {offer.RequiredRotations} completed flight{(offer.RequiredRotations == 1 ? "" : "s")}.";
                if (!ignoreFunds && !CareerState.CanAfford(offer.Price))
                    return costText;
                return null;
            }

            if (type.IsRotorcraft)
                return "A helicopter flies from the Adelaide helipad; it cannot be based at an outstation.";
            if (PlayerFleetCount() >= AircraftAcquisition.MaxPlayerAircraft)
                return "Your fleet is full.";
            var based = 0;
            foreach (var aircraft in _outstationFleet)
                if (aircraft.BaseCode == baseCode)
                    based++;
            if (based >= OutstationCapacity)
                return $"{baseCode} holds {OutstationCapacity} aircraft.";
            if (!PlayerBase.Supports(CareerState.BaseLevel, type) || CareerState.Tier < offer.RequiredTier
                || CareerState.Reliability < offer.RequiredReliability
                || CareerState.CompletedPlayerRotations < offer.RequiredRotations)
                return "You don't meet this aircraft's requirements yet. See Fleet for what it needs.";
            if (!HasOutstationRoute(type, baseCode))
                return $"{Article.CapitalA(type.Name)} at {baseCode} would have nowhere in range to fly. Choose a longer-range type.";
            if (!ignoreFunds && !CareerState.CanAfford(offer.Price))
                return costText;
            return null;
        }
    }
}
