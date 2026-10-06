using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>What a player aircraft is doing, in terms every base shares (ADR 0239).</summary>
    public enum PlayerFleetKind
    {
        /// <summary>Idle at its base with nothing booked.</summary>
        Parked,
        /// <summary>At its base with a flight booked that has not left yet.</summary>
        Booked,
        /// <summary>Moving on or around the Adelaide airfield: taxiing, on the runway, in the circuit.</summary>
        Moving,
        /// <summary>Away from its base: en route, turning round at the far end, or on a network service.</summary>
        Airborne,
        /// <summary>Being moved between bases; it earns nothing and counts no flight on arrival.</summary>
        Ferry,
        /// <summary>Grounded for its routine check.</summary>
        InCheck
    }

    /// <summary>Where an outstation aircraft is in its timed round trip.</summary>
    public enum OutstationPhase { Parked, Outbound, Turnaround, Inbound }

    /// <summary>
    /// One player aircraft, whichever system holds it. Exactly one of <see cref="Live"/> and
    /// <see cref="Outstation"/> is set. Read-only: commands still go through
    /// <see cref="AirlineOperations"/>. Carries structured data only; wording belongs to Presentation.
    /// </summary>
    public sealed class PlayerFleetEntry
    {
        public string Registration { get; internal set; } = string.Empty;
        public AircraftType Type { get; internal set; }

        /// <summary>Position in <see cref="PlayerFleet.Entries"/>: Adelaide aircraft in fleet order, then outstations.</summary>
        public int Order { get; internal set; }

        /// <summary>The destination code of the base the aircraft belongs to ("ADL", "MEL", …).</summary>
        public string BaseCode { get; internal set; } = string.Empty;

        public bool IsOutstation { get; internal set; }
        public FleetAircraft Live { get; internal set; }
        public OutstationAircraft Outstation { get; internal set; }
        public PlayerFleetKind Kind { get; internal set; }

        /// <summary>The far end of the booked or current flight; empty when there is none.</summary>
        public string DestinationCode { get; internal set; } = string.Empty;

        /// <summary>
        /// The next moment worth showing: departure time while booked, arrival or return while flying,
        /// the end of a check. Zero when nothing is pending.
        /// </summary>
        public long NextAtSeconds { get; internal set; }

        /// <summary>A check is overdue: the aircraft has flown its interval.</summary>
        public bool CheckDue { get; internal set; }

        /// <summary>One rotation left, or none: worth planning the check now.</summary>
        public bool CheckSoon { get; internal set; }

        public bool InCheck { get; internal set; }
        public long CheckUntilSeconds { get; internal set; }
        public int RotationsUntilCheck { get; internal set; }

        public int Flights { get; internal set; }
        public long LifetimeRevenue { get; internal set; }
        public long JoinedAtSeconds { get; internal set; }

        /// <summary>What a sale would pay now; zero when the aircraft cannot be sold.</summary>
        public long ResaleValue { get; internal set; }

        public bool CanSell { get; internal set; }

        /// <summary>Airborne, moving or on a ferry: anything the player would call "flying".</summary>
        public bool IsFlying => Kind == PlayerFleetKind.Moving || Kind == PlayerFleetKind.Airborne
                                || Kind == PlayerFleetKind.Ferry;
    }

    /// <summary>
    /// The unified view of the player's fleet (ADR 0239): Adelaide aircraft and outstation aircraft in one
    /// list, so every screen counts and describes them the same way.
    /// </summary>
    public static class PlayerFleet
    {
        /// <summary>Appends every player aircraft: Adelaide aircraft first in fleet order, then outstations.</summary>
        public static void Entries(AirlineOperations operations, SimulationTime now, List<PlayerFleetEntry> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (operations == null || operations.PlayerAirline == null)
                return;

            foreach (var aircraft in operations.Fleet)
                if (aircraft.Airline.IsPlayer)
                    into.Add(ForLive(operations, aircraft, now));
            foreach (var aircraft in operations.OutstationFleet)
                into.Add(ForOutstation(aircraft, now));
            for (var i = 0; i < into.Count; i++)
                into[i].Order = i;
        }

        /// <summary>The entry for one registration, or null when the player has no such aircraft.</summary>
        public static PlayerFleetEntry Find(AirlineOperations operations, SimulationTime now, string registration)
        {
            if (operations == null || string.IsNullOrEmpty(registration))
                return null;
            foreach (var aircraft in operations.Fleet)
                if (aircraft.Airline.IsPlayer && aircraft.Registration == registration)
                    return ForLive(operations, aircraft, now);
            foreach (var aircraft in operations.OutstationFleet)
                if (aircraft.Registration == registration)
                    return ForOutstation(aircraft, now);
            return null;
        }

        private static PlayerFleetEntry ForLive(AirlineOperations operations, FleetAircraft aircraft, SimulationTime now)
        {
            var entry = new PlayerFleetEntry
            {
                Registration = aircraft.Registration,
                Type = aircraft.Type,
                BaseCode = operations.Home.Code,
                Live = aircraft,
                Flights = aircraft.CompletedTrips,
                LifetimeRevenue = aircraft.LifetimeRevenue,
                JoinedAtSeconds = aircraft.JoinedAirlineAt.ElapsedSeconds,
                CheckDue = Maintenance.IsOverdue(aircraft),
                CheckSoon = Maintenance.IsDueSoon(aircraft),
                RotationsUntilCheck = Maintenance.RotationsUntilDue(aircraft),
                InCheck = Maintenance.InCheck(aircraft, now)
            };
            if (entry.InCheck && aircraft.CheckUntil.HasValue)
                entry.CheckUntilSeconds = aircraft.CheckUntil.Value.ElapsedSeconds;

            if (aircraft.Scheduled.HasValue)
            {
                entry.DestinationCode = aircraft.Scheduled.Value.Destination.Code;
                entry.NextAtSeconds = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
            }
            else if (aircraft.CurrentDestination.HasValue)
                entry.DestinationCode = aircraft.CurrentDestination.Value.Code;
            if (!aircraft.Scheduled.HasValue && aircraft.StateEndsAt.HasValue)
                entry.NextAtSeconds = aircraft.StateEndsAt.Value.ElapsedSeconds;

            if (entry.InCheck)
                entry.Kind = PlayerFleetKind.InCheck;
            else if (aircraft.IsFerry)
                entry.Kind = PlayerFleetKind.Ferry;
            else
                switch (aircraft.State)
                {
                    case FleetState.AtStand:
                        entry.Kind = aircraft.Scheduled.HasValue ? PlayerFleetKind.Booked : PlayerFleetKind.Parked;
                        break;
                    case FleetState.Outbound:
                    case FleetState.AtDestination:
                    case FleetState.Inbound:
                        entry.Kind = PlayerFleetKind.Airborne;
                        break;
                    default:
                        entry.Kind = PlayerFleetKind.Moving;
                        break;
                }

            entry.CanSell = operations.CanResell(aircraft) && aircraft.State == FleetState.AtStand
                            && !aircraft.Scheduled.HasValue && !entry.InCheck;
            if (operations.CanResell(aircraft))
                entry.ResaleValue = AirlineOperations.ResaleValue(aircraft.Type);
            return entry;
        }

        private static PlayerFleetEntry ForOutstation(OutstationAircraft aircraft, SimulationTime now)
        {
            var seconds = now.ElapsedSeconds;
            var entry = new PlayerFleetEntry
            {
                Registration = aircraft.Registration,
                Type = aircraft.Type,
                BaseCode = aircraft.BaseCode,
                IsOutstation = true,
                Outstation = aircraft,
                Flights = aircraft.CompletedServices,
                LifetimeRevenue = aircraft.LifetimeRevenue,
                JoinedAtSeconds = aircraft.JoinedAtSeconds,
                CheckDue = aircraft.CheckDue,
                CheckSoon = aircraft.RotationsSinceCheck >= Maintenance.IntervalRotations - 1,
                RotationsUntilCheck = Maintenance.IntervalRotations - aircraft.RotationsSinceCheck,
                InCheck = aircraft.InCheck(seconds)
            };
            if (entry.InCheck)
                entry.CheckUntilSeconds = aircraft.CheckUntilSeconds;

            if (aircraft.HasFlight)
            {
                entry.DestinationCode = aircraft.DestinationCode;
                entry.NextAtSeconds = seconds < aircraft.DepartAtSeconds
                    ? aircraft.DepartAtSeconds
                    : aircraft.ReturnAtSeconds;
            }

            if (entry.InCheck)
            {
                entry.Kind = PlayerFleetKind.InCheck;
                entry.NextAtSeconds = aircraft.CheckUntilSeconds;
            }
            else if (!aircraft.HasFlight)
                entry.Kind = PlayerFleetKind.Parked;
            else
                entry.Kind = seconds < aircraft.DepartAtSeconds ? PlayerFleetKind.Booked : PlayerFleetKind.Airborne;

            entry.CanSell = !aircraft.HasFlight && !entry.InCheck && AircraftAcquisition.TryFor(aircraft.Type, out _);
            if (AircraftAcquisition.TryFor(aircraft.Type, out _))
                entry.ResaleValue = AirlineOperations.ResaleValue(aircraft.Type);
            return entry;
        }

        /// <summary>
        /// Where an outstation aircraft is, worked out from its booked times alone (nothing is saved for it):
        /// parked at base, then out along the great circle, a 45-minute turnaround at the far end, and back.
        /// <paramref name="progress"/> is 0..1 along the current leg; false when either city is unknown.
        /// </summary>
        public static bool TryPosition(OutstationAircraft aircraft, SimulationTime now, out double latitude,
            out double longitude, out OutstationPhase phase, out double progress)
        {
            latitude = 0;
            longitude = 0;
            phase = OutstationPhase.Parked;
            progress = 0;
            if (aircraft == null || !DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin))
                return false;
            if (!aircraft.HasFlight || now.ElapsedSeconds < aircraft.DepartAtSeconds
                || !DestinationCatalogue.TryFind(aircraft.DestinationCode, out var destination))
            {
                latitude = origin.Latitude;
                longitude = origin.Longitude;
                return true;
            }

            // A service is two airborne legs and a turnaround: ScheduleOutstationService sets
            // duration = 2 * airborne + 45 minutes, so the leg length is recovered from the record.
            var airborne = Math.Max(1L, (aircraft.ReturnAtSeconds - aircraft.DepartAtSeconds - TurnaroundSeconds) / 2);
            var elapsed = now.ElapsedSeconds - aircraft.DepartAtSeconds;
            if (elapsed < airborne)
            {
                phase = OutstationPhase.Outbound;
                progress = elapsed / (double)airborne;
                FlightRoute.GreatCircle(origin.Latitude, origin.Longitude, destination.Latitude,
                    destination.Longitude, progress, out latitude, out longitude);
            }
            else if (elapsed < airborne + TurnaroundSeconds)
            {
                phase = OutstationPhase.Turnaround;
                progress = (elapsed - airborne) / (double)TurnaroundSeconds;
                latitude = destination.Latitude;
                longitude = destination.Longitude;
            }
            else
            {
                phase = OutstationPhase.Inbound;
                progress = Math.Min(1.0, (elapsed - airborne - TurnaroundSeconds) / (double)airborne);
                FlightRoute.GreatCircle(destination.Latitude, destination.Longitude, origin.Latitude,
                    origin.Longitude, progress, out latitude, out longitude);
            }

            return true;
        }

        /// <summary>The turnaround an outstation service allows at the far end (see ScheduleOutstationService).</summary>
        public const long TurnaroundSeconds = 45 * 60;

        /// <summary>"2 at MEL, 1 at SYD" for the aircraft based away from Adelaide; empty when there are none.</summary>
        public static string OutstationSummary(AirlineOperations operations)
        {
            if (operations == null || operations.OutstationFleet.Count == 0)
                return string.Empty;
            var parts = new List<string>();
            foreach (var code in AirlineOperations.OutstationCandidates)
            {
                var count = 0;
                foreach (var aircraft in operations.OutstationFleet)
                    if (aircraft.BaseCode == code)
                        count++;
                if (count > 0)
                    parts.Add(count + " at " + code);
            }

            return string.Join(", ", parts);
        }
    }
}
