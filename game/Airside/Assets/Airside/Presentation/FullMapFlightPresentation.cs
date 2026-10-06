using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Pure runtime seams for full-map rows, inspection transitions and field headings.</summary>
    public static class FullMapFlightPresentation
    {
        public readonly struct FieldFlight
        {
            public FieldFlight(FleetAircraft aircraft, double latitude, double longitude, Destination from, Destination to)
            { Aircraft = aircraft; Latitude = latitude; Longitude = longitude; From = from; To = to; }
            public FleetAircraft Aircraft { get; }
            public string Registration => Aircraft.Registration;
            public double Latitude { get; }
            public double Longitude { get; }
            public Destination From { get; }
            public Destination To { get; }
        }

        // A cancelled first departure is not a flight. Off-field rows still require a current trip.
        public static bool TryFieldFlight(FleetAircraft aircraft, Destination home, bool hasLocation,
            double latitude, double longitude, out FieldFlight row)
        {
            row = default;
            if (aircraft.IsOffMap || !hasLocation || double.IsNaN(latitude) || double.IsNaN(longitude)
                || double.IsInfinity(latitude) || double.IsInfinity(longitude)) return false;
            var destination = aircraft.CurrentDestination;
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue)
            {
                if (aircraft.Scheduled.Value.Cancelled) return false;
                destination = aircraft.Scheduled.Value.Destination;
            }
            if (!destination.HasValue && aircraft.Scheduled.HasValue && !aircraft.Scheduled.Value.Cancelled)
                destination = aircraft.Scheduled.Value.Destination;
            if (!destination.HasValue) return false;
            var arriving = aircraft.State is FleetState.HoldingForLanding or FleetState.GoAround
                or FleetState.Landing or FleetState.AwaitingStand or FleetState.TaxiIn;
            row = new FieldFlight(aircraft, latitude, longitude,
                arriving ? destination.Value : home, arriving ? home : destination.Value);
            return true;
        }

        public readonly struct Interaction
        {
            public Interaction(string planningId, string selectedId, string inspectorId)
            { PlanningId = planningId; SelectedId = selectedId; InspectorId = inspectorId; }
            public string PlanningId { get; }
            public string SelectedId { get; }
            public string InspectorId { get; }
            public Interaction Inspect(string id) => new(PlanningId, id, id);
            public Interaction Plan(string id) => new(id, id ?? SelectedId, null);
            // Explicit selection exits the old inspection even if the map remains open.
            public Interaction Select(string id, bool isPlayer) => new(isPlayer ? id : PlanningId, id, null);
        }

        /// <summary>Rotate the actor's field forward through geography and the same map projection.</summary>
        public static float FieldHeading(AustraliaMapLens lens, float areaWidth, float areaHeight,
            double worldX, double worldZ, double forwardX, double forwardZ)
        {
            var length = Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (length < 0.000001) return 0f;
            YpadFrame.ToLatLon(worldX, worldZ, out var latitude, out var longitude);
            // A kilometre avoids float projection cancellation at the world-map zoom.
            YpadFrame.ToLatLon(worldX + forwardX / length * 1000, worldZ + forwardZ / length * 1000,
                out var aheadLatitude, out var aheadLongitude);
            lens.Project(0, 0, areaWidth, areaHeight, longitude, latitude, out var x, out var y);
            lens.Project(0, 0, areaWidth, areaHeight, aheadLongitude, aheadLatitude, out var aheadX, out var aheadY);
            return (float)(Math.Atan2(aheadY - y, aheadX - x) * 180 / Math.PI + 90);
        }
    }
}
