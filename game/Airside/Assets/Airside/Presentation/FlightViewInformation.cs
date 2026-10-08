using System;
using System.Globalization;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Read-only flight-view information from the watched aircraft and its actual pose.</summary>
    public static class FlightViewInformation
    {
        public static float TrueHeading(double forwardX, double forwardZ)
        {
            YpadFrame.FromEastNorth(1, 0, out var eastX, out var eastZ);
            YpadFrame.FromEastNorth(0, 1, out var northX, out var northZ);
            var degrees = Math.Atan2(forwardX * eastX + forwardZ * eastZ,
                forwardX * northX + forwardZ * northZ) * 180 / Math.PI;
            return (float)((degrees + 360) % 360);
        }

        public static void Fill(FlightViewHudData data, FleetAircraft aircraft, Destination home,
            double now, double renderX, double renderZ, double originX, double originZ,
            double fieldHeightMetres, double forwardX, double forwardZ,
            double groundKnots, double verticalMetresPerSecond, EnrouteProfile? profile = null, double elapsed = 0)
        {
            YpadFrame.ToLatLon(renderX + originX, renderZ + originZ, out var latitude, out var longitude);
            var position = new Destination("POS", "Aircraft", "", latitude, longitude);
            var nearest = home;
            var nearestKm = position.DistanceKmTo(home);
            foreach (var candidate in DestinationCatalogue.All)
            {
                var km = position.DistanceKmTo(candidate);
                if (km < nearestKm) { nearest = candidate; nearestKm = km; }
            }
            data.Registration = aircraft.Registration;
            data.Aircraft = aircraft.Type.Name;
            data.Phase = AircraftStatus.TagPhase(aircraft, new SimulationTime((long)Math.Max(0, now)));
            data.Speed = FormattableString.Invariant($"{Math.Max(0, groundKnots):0} kt");
            var altitude=fieldHeightMetres+FlightAtmosphere.FieldElevationMetres;
            var cas=FlightAtmosphere.CalibratedKnots(Math.Max(0,groundKnots),altitude);
            var mach=FlightAtmosphere.Mach(Math.Max(0,groundKnots),altitude);
            data.Airspeed=FormattableString.Invariant($"{cas:0} kt / M{mach:0.00}");
            data.VerticalSpeed = FormattableString.Invariant($"{verticalMetresPerSecond * 196.85:+0;-0;0} ft/min");
            data.Altitude = FormattableString.Invariant($"{Math.Max(0, fieldHeightMetres) * 3.28084:0} ft");
            data.Heading = ((int)Math.Round(TrueHeading(forwardX, forwardZ)) % 360).ToString("000", CultureInfo.InvariantCulture) + "° T";
            data.Distance = FormattableString.Invariant($"{position.DistanceKmTo(home):0.0} km");
            data.Location = nearestKm < 3 ? "Near " + nearest.Name
                : FormattableString.Invariant($"{nearestKm:0} km from {nearest.Name}");
            data.Location += FormattableString.Invariant($"  ·  {Math.Abs(latitude):0.00}°{(latitude < 0 ? "S" : "N")} {Math.Abs(longitude):0.00}°{(longitude < 0 ? "W" : "E")}");
            data.Remaining = "—";
            data.Arrival = "—";
            data.JourneyProgress = -1f;
            var destination = aircraft.CurrentDestination;
            if (aircraft.State == FleetState.AtStand)
                destination = aircraft.Scheduled.HasValue && !aircraft.Scheduled.Value.Cancelled
                    ? aircraft.Scheduled.Value.Destination : null;
            var returning = aircraft.State is FleetState.Inbound or FleetState.HoldingForLanding
                or FleetState.GoAround or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;
            data.Route = destination.HasValue
                ? returning ? destination.Value.Code + " → " + home.Code : home.Code + " → " + destination.Value.Code
                : "At " + home.Name;
            data.Journey = aircraft.State switch
            {
                FleetState.AtStand => "At stand · awaiting departure",
                FleetState.TaxiOut => "Taxiing to runway",
                FleetState.HoldingShort => "Holding short · awaiting clearance",
                FleetState.TakingOff => "Takeoff · departure in progress",
                FleetState.HoldingForLanding => "Holding · awaiting runway",
                FleetState.GoAround => "Go-around · rejoining approach",
                FleetState.Landing => "Landing · runway sequence",
                FleetState.AwaitingStand => "Runway vacated · awaiting stand",
                FleetState.TaxiIn => "Taxiing to stand",
                FleetState.AtDestination => "Turnaround at destination",
                _ => "En route · arrival estimate unavailable"
            };
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
                data.Journey = aircraft.Scheduled.Value.Cancelled ? "Departure cancelled"
                    : "Departure in " + SelectionCardText.Remaining(aircraft.Scheduled.Value.DepartAt.ElapsedSeconds - now);
            if (aircraft.State == FleetState.AtDestination && destination.HasValue)
                data.Route = "At " + destination.Value.Name + " · next leg to " + home.Code;
            if (destination.HasValue && (aircraft.State is FleetState.Outbound or FleetState.Inbound))
            {
                var target = returning ? home : destination.Value;
                data.Remaining = FormattableString.Invariant($"{position.DistanceKmTo(target):0} km direct");
                if (profile.HasValue && aircraft.StateEndsAt.HasValue)
                {
                    data.Arrival = SelectionCardText.Remaining(aircraft.StateEndsAt.Value.ElapsedSeconds - now);
                    data.JourneyProgress = (float)profile.Value.DistanceFractionAt(elapsed);
                    data.Journey = $"{profile.Value.PhaseAt(elapsed)} · {data.JourneyProgress * 100:0}% of leg · arrival area estimate";
                }
            }
        }
    }
}
