using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Read-only flight-view telemetry from the network service's persisted timetable.</summary>
    public readonly struct OutstationJourney
    {
        public OutstationJourney(double latitude, double longitude, double heading, double altitudeFeet,
            double speedKnots, OutstationPhase phase, double progress)
        {
            Latitude = latitude; Longitude = longitude; Heading = heading;
            AltitudeFeet = altitudeFeet; SpeedKnots = speedKnots; Phase = phase; Progress = progress;
        }
        public double Latitude { get; }
        public double Longitude { get; }
        public double Heading { get; }
        public double WorldYaw
        {
            get
            {
                var radians = Heading * Math.PI / 180;
                YpadFrame.ToWorld(Latitude, Longitude, out var x, out var z);
                YpadFrame.ToWorld(Latitude + Math.Cos(radians) * .0001,
                    Longitude + Math.Sin(radians) * .0001 / Math.Cos(Latitude * Math.PI / 180), out var aheadX, out var aheadZ);
                return Math.Atan2(aheadX - x, aheadZ - z) * 180 / Math.PI;
            }
        }
        public double AltitudeFeet { get; }
        public double SpeedKnots { get; }
        public OutstationPhase Phase { get; }
        public double Progress { get; }
        public bool Airborne => Phase == OutstationPhase.Outbound || Phase == OutstationPhase.Inbound;

        public static bool TryFor(OutstationAircraft aircraft, SimulationTime now, out OutstationJourney journey)
            => TryFor(aircraft, (double)now.ElapsedSeconds, out journey);

        public static bool TryFor(OutstationAircraft aircraft, double secondsNow, out OutstationJourney journey)
        {
            journey = default;
            var now = new SimulationTime((long)Math.Max(0, secondsNow));
            if (!PlayerFleet.TryPosition(aircraft, now, out var lat, out var lon, out var phase, out var progress)
                || !DestinationCatalogue.TryFind(aircraft.BaseCode, out var home)) return false;
            if (phase == OutstationPhase.Parked || phase == OutstationPhase.Turnaround)
            {
                journey = new OutstationJourney(lat, lon, 0, 0, 0, phase, progress);
                return true;
            }
            if (!DestinationCatalogue.TryFind(aircraft.DestinationCode, out var far)) return false;
            var from = phase == OutstationPhase.Inbound ? far : home;
            var to = phase == OutstationPhase.Inbound ? home : far;
            var seconds = Math.Max(1, (aircraft.ReturnAtSeconds - aircraft.DepartAtSeconds - PlayerFleet.TurnaroundSeconds) / 2);
            var legStart = phase == OutstationPhase.Inbound ? aircraft.DepartAtSeconds + seconds + PlayerFleet.TurnaroundSeconds
                : aircraft.DepartAtSeconds;
            progress = Math.Clamp((secondsNow - legStart) / seconds, 0, 1);
            FlightRoute.GreatCircle(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                progress, out lat, out lon);
            var profile = new EnrouteProfile(home.DistanceKmTo(far), seconds, aircraft.Type);
            // Look slightly ahead rather than toward the end point at an almost-finished leg.
            FlightRoute.GreatCircle(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                Math.Min(1, progress + .0001), out var aheadLat, out var aheadLon);
            var heading = FlightRoute.HeadingDegrees(lat, lon, aheadLat, aheadLon);
            journey = new OutstationJourney(lat, lon, heading, profile.AltitudeFeetAt(progress * seconds),
                profile.GroundSpeedKnotsAt(progress * seconds), phase, progress);
            return true;
        }
    }
}
