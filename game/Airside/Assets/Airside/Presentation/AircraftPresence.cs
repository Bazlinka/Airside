using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Physical presence is independent of activity lists and camera cycling.</summary>
    public static class AircraftPresence
    {
        public const long DepartureActivityWindowSeconds = 2 * 3600;

        public static bool IsActive(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null) return false;
            if (aircraft.MaintenanceJob?.ActiveLeg(aircraft.Type) != null) return true;
            if (aircraft.State == FleetState.AtStand)
                return aircraft.Scheduled.HasValue && !aircraft.Scheduled.Value.Cancelled
                    && aircraft.Scheduled.Value.PublishedAt.ElapsedSeconds - now.ElapsedSeconds <= DepartureActivityWindowSeconds;
            if (aircraft.State == FleetState.AtDestination)
                return aircraft.StateEndsAt.HasValue
                    && aircraft.StateEndsAt.Value.ElapsedSeconds - now.ElapsedSeconds <= DepartureActivityWindowSeconds;
            return aircraft.State != FleetState.Maintenance;
        }

        public static bool HasJourneyPose(FleetState state, bool hasDestination,
            bool hasDeadline, bool hasDestinationRunway) => hasDestination
            && (hasDeadline && (state is FleetState.Outbound or FleetState.Inbound)
                || state == FleetState.AtDestination && hasDestinationRunway);

        /// <summary>
        /// Keep route models throughout the camera's scene volume. Twice the far depth
        /// conservatively encloses the view's corners; normal renderer frustum tests decide
        /// actual visibility. An explicitly watched aircraft is never removed by this budget.
        /// </summary>
        public static bool RouteInRange(double dx, double dy, double dz, double farClip, bool watched)
        {
            if (watched) return true;
            var radius = Math.Max(1, farClip) * 2;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }
    }
}
