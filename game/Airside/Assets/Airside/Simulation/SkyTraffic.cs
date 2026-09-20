using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// One airborne flight that is not operating at Adelaide: other airports to other
    /// airports. Deterministic in the injected clock; never reserves Adelaide resources.
    /// </summary>
    public readonly struct SkyFlight
    {
        public SkyFlight(string callsign, AircraftType type, Destination from, Destination to,
            double progress, double latitude, double longitude, double altitudeFeet, double headingDegrees)
        {
            Callsign = callsign;
            Type = type;
            From = from;
            To = to;
            Progress = progress;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeFeet = altitudeFeet;
            HeadingDegrees = headingDegrees;
        }

        public string Callsign { get; }
        public AircraftType Type { get; }
        public Destination From { get; }
        public Destination To { get; }
        public double Progress { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public double AltitudeFeet { get; }
        public double HeadingDegrees { get; }
    }

    /// <summary>
    /// Authored corridors that never land at Adelaide (ADR 0055). Snapshot is a pure
    /// function of simulation time, so frame rate and reload cannot change who is where.
    /// </summary>
    public static class SkyTraffic
    {
        /// <summary>
        /// How close to Adelaide (km, true) a corridor must pass before it is drawn in 3D.
        /// PER–MEL's great circle bottoms out ~230 km from the field, so this has to be
        /// that wide or the one corridor that actually overflies South Australia never appears.
        /// </summary>
        public const double VisibleRadiusKm = 260.0;

        /// <summary>True kilometres are compressed into this radius so cruise traffic fits the 10 km camera far clip.</summary>
        public const double DrawRadiusMetres = 7_500.0;

        /// <summary>
        /// Traffic inside this true range is drawn almost 1:1 so inbound and
        /// outbound legs move at a readable speed instead of the 35× crawl of a
        /// flat 260 km → 7.5 km map.
        /// </summary>
        public const double NearFieldKm = 8.0;

        /// <summary>Display metres used for <see cref="NearFieldKm"/>.</summary>
        public const double NearFieldMetres = 6_000.0;

        /// <summary>
        /// Planned Adelaide arrivals/departures closer than this are hidden so
        /// they do not sit on the live circuit — live inbound/outbound owns that.
        /// </summary>
        public const double CircuitClearKm = 1.2;

        /// <summary>Display height is compressed so cruise traffic reads from the airfield camera.</summary>
        public const double DisplayAltitudeScale = 0.28;

        public static readonly SkyRoute[] Routes =
        {
            new("QF", 400, 8 * 60, "MEL", "SYD", AircraftType.Boeing7378),
            new("QF", 401, 8 * 60, "SYD", "MEL", AircraftType.Boeing7378),
            new("VA", 210, 12 * 60, "BNE", "MEL", AircraftType.Boeing7378),
            new("VA", 211, 12 * 60, "MEL", "BNE", AircraftType.Boeing7378),
            new("QF", 9, 24 * 60, "PER", "MEL", AircraftType.Boeing78710),
            new("QF", 10, 24 * 60, "MEL", "PER", AircraftType.Boeing78710),
            new("QF", 7, 26 * 60, "PER", "SYD", AircraftType.Boeing78710),
            new("QF", 8, 26 * 60, "SYD", "PER", AircraftType.Boeing78710),
            new("JQ", 960, 22 * 60, "DRW", "MEL", AircraftType.Boeing7378),
            new("JQ", 961, 22 * 60, "MEL", "DRW", AircraftType.Boeing7378),
            new("NZ", 176, 20 * 60, "AKL", "MEL", AircraftType.AirbusA321Neo),
            new("NZ", 175, 20 * 60, "MEL", "AKL", AircraftType.AirbusA321Neo),
            new("VA", 860, 14 * 60, "BNE", "SYD", AircraftType.Boeing7378),
            new("JQ", 704, 14 * 60, "HBA", "MEL", AircraftType.Boeing7378),
            new("QF", 754, 14 * 60, "MEL", "HBA", AircraftType.Boeing7378),
            new("VA", 140, 16 * 60, "PER", "BNE", AircraftType.Boeing7378),
            new("VA", 141, 16 * 60, "BNE", "PER", AircraftType.Boeing7378),
            new("QF", 20, 22 * 60, "DRW", "SYD", AircraftType.Boeing78710),
            new("QF", 21, 22 * 60, "SYD", "DRW", AircraftType.Boeing78710),
            new("NZ", 104, 18 * 60, "AKL", "SYD", AircraftType.AirbusA321Neo),
            new("NZ", 103, 18 * 60, "SYD", "AKL", AircraftType.AirbusA321Neo),
            new("QF", 612, 14 * 60, "MEL", "CBR", AircraftType.Boeing7378),
            new("QF", 613, 14 * 60, "CBR", "MEL", AircraftType.Boeing7378),
            new("JQ", 770, 18 * 60, "OOL", "MEL", AircraftType.Boeing7378),
            new("JQ", 771, 18 * 60, "MEL", "OOL", AircraftType.Boeing7378),
        };

        /// <summary>
        /// One flight along an authored corridor at a known start time. Used for the
        /// published Adelaide day as well as the overflight snapshot.
        /// </summary>
        public static bool TryEnroute(string callsign, AircraftType type, Destination from, Destination to,
            double elapsedSeconds, double startSeconds, out SkyFlight flight)
        {
            flight = default;
            if (type == null || from.Code == null || to.Code == null)
                return false;
            var duration = LegTiming.AirborneSeconds(from.DistanceKmTo(to), type);
            if (duration <= 0)
                return false;
            var progress = (elapsedSeconds - startSeconds) / duration;
            if (progress < 0 || progress > 1)
                return false;
            FlightRoute.Point(from.Latitude, from.Longitude, to.Latitude, to.Longitude, progress,
                callsign, out var lat, out var lon);
            FlightRoute.Point(from.Latitude, from.Longitude, to.Latitude, to.Longitude,
                Math.Min(1.0, progress + 0.004), callsign, out var latAhead, out var lonAhead);
            var heading = FlightRoute.HeadingDegrees(lat, lon, latAhead, lonAhead);
            var profile = new EnrouteProfile(from.DistanceKmTo(to), duration, type);
            var altitude = profile.AltitudeFeetAt(elapsedSeconds - startSeconds);
            flight = new SkyFlight(callsign, type, from, to, progress, lat, lon, altitude, heading);
            return true;
        }

        public static IReadOnlyList<SkyFlight> At(SimulationTime now) => At(now.ElapsedSeconds, null);

        public static IReadOnlyList<SkyFlight> At(double elapsedSeconds) => At(elapsedSeconds, null);

        public static IReadOnlyList<SkyFlight> At(double elapsedSeconds, AirlineClock clock)
        {
            var flights = new List<SkyFlight>();
            foreach (var route in Routes)
            {
                if (!DestinationCatalogue.TryFind(route.FromCode, out var from)
                    || !DestinationCatalogue.TryFind(route.ToCode, out var to))
                    continue;

                var duration = LegTiming.AirborneSeconds(from.DistanceKmTo(to), route.Type);
                if (duration <= 0 || route.IntervalSeconds <= 0)
                    continue;

                var earliest = elapsedSeconds - duration;
                var firstStart = FloorTo((long)Math.Floor(earliest), route.IntervalSeconds);
                for (var start = firstStart; start <= elapsedSeconds; start += route.IntervalSeconds)
                {
                    if (start + duration <= elapsedSeconds || start > elapsedSeconds)
                        continue;
                    if (clock != null && QuietHourSkip(route, start, clock))
                        continue;
                    var number = route.FlightNumber + Math.Abs(start / route.IntervalSeconds) % 40;
                    if (TryEnroute($"{route.Airline}{number}", route.Type, from, to, elapsedSeconds, start,
                            out var flight))
                        flights.Add(flight);
                }
            }

            return flights;
        }

        /// <summary>
        /// Drop a deterministic slice of overflights in quiet Adelaide hours so the
        /// sky matches the banks. Peak hours keep every authored interval.
        /// </summary>
        private static bool QuietHourSkip(SkyRoute route, long startSeconds, AirlineClock clock)
        {
            var hour = clock.LocalAt(new SimulationTime(Math.Max(0, startSeconds))).Hour;
            var density = AdelaideHourProfile.Density(hour);
            if (density >= 0.99f)
                return false;
            var keep = Math.Max(0.18f, density);
            var hash = Math.Abs((startSeconds / Math.Max(1, route.IntervalSeconds) + route.FlightNumber) % 100);
            return hash >= keep * 100;
        }

        /// <summary>Closest true-kilometre approach of a great-circle corridor to Adelaide.</summary>
        public static double ClosestApproachKm(Destination from, Destination to)
        {
            var closest = double.MaxValue;
            for (var i = 0; i <= 200; i++)
            {
                GreatCircle(from.Latitude, from.Longitude, to.Latitude, to.Longitude, i / 200.0,
                    out var lat, out var lon);
                ToLocalMetres(lat, lon, out var east, out var north);
                var km = Math.Sqrt(east * east + north * north) / 1000.0;
                if (km < closest)
                    closest = km;
            }

            return closest;
        }

        public static bool TryWorldPosition(SkyFlight flight, out double x, out double y, out double z)
        {
            ToLocalMetres(flight.Latitude, flight.Longitude, out var east, out var north);
            var rangeKm = Math.Sqrt(east * east + north * north) / 1000.0;
            if (rangeKm > VisibleRadiusKm)
            {
                x = east;
                z = north;
                y = 0;
                return false;
            }

            ProjectLocal(east, north, out x, out z);
            y = DisplayAltitudeMetres(flight);
            return true;
        }

        /// <summary>
        /// Unity yaw that faces the compressed on-screen path, not the true
        /// heading. A flat 260 km disc squeezed into 7.5 km makes true heading
        /// crab against the drawn motion.
        /// </summary>
        public static float DisplayUnityYaw(SkyFlight flight)
        {
            if (TryWorldPosition(flight, out var x, out _, out var z)
                && TryAheadWorldPosition(flight, out var ax, out var az))
            {
                var dx = ax - x;
                var dz = az - z;
                if (dx * dx + dz * dz > 0.25)
                    return (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            }

            return RunwayWeather.UnityYawFromTrue((float)flight.HeadingDegrees);
        }

        /// <summary>True when a planned Adelaide movement is already over the live circuit.</summary>
        public static bool OccupiesTheCircuit(SkyFlight flight)
        {
            if (flight.From.Code != "ADL" && flight.To.Code != "ADL")
                return false;
            ToLocalMetres(flight.Latitude, flight.Longitude, out var east, out var north);
            return Math.Sqrt(east * east + north * north) / 1000.0 < CircuitClearKm;
        }

        /// <summary>
        /// Near-field almost 1:1, then the remaining 8–260 km true squeezed onto
        /// the last 1.5 km of draw radius so distant overflights stay in clip.
        /// </summary>
        public static void ProjectLocal(double eastMetres, double northMetres, out double x, out double z)
        {
            ToRunwayFrame(eastMetres, northMetres, out var along, out var across);
            var range = Math.Sqrt(along * along + across * across);
            var rangeKm = range / 1000.0;
            if (range < 1e-6)
            {
                x = 0;
                z = 0;
                return;
            }

            double displayRange;
            if (rangeKm <= NearFieldKm)
                displayRange = range * (NearFieldMetres / (NearFieldKm * 1000.0));
            else
            {
                var t = (rangeKm - NearFieldKm) / (VisibleRadiusKm - NearFieldKm);
                displayRange = NearFieldMetres + t * (DrawRadiusMetres - NearFieldMetres);
            }

            var scale = displayRange / range;
            x = along * scale;
            z = across * scale;
        }

        private static bool TryAheadWorldPosition(SkyFlight flight, out double x, out double z)
        {
            x = 0;
            z = 0;
            if (flight.From.Code == null || flight.To.Code == null)
                return false;
            var step = Math.Min(1.0, flight.Progress + 0.003);
            if (step <= flight.Progress)
                return false;
            FlightRoute.Point(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                step, flight.Callsign, out var lat, out var lon);
            var ahead = new SkyFlight(flight.Callsign, flight.Type, flight.From, flight.To, step,
                lat, lon, flight.AltitudeFeet, flight.HeadingDegrees);
            return TryWorldPosition(ahead, out x, out _, out z);
        }

        /// <summary>
        /// World +X is runway 05, not east. Convert true east/north so overflights
        /// sit over the gulf and the hills the same way the field is drawn.
        /// </summary>
        public static void ToRunwayFrame(double eastMetres, double northMetres, out double along, out double across)
        {
            var heading = RunwayWeather.Heading05 * Math.PI / 180.0;
            var sin = Math.Sin(heading);
            var cos = Math.Cos(heading);
            along = eastMetres * sin + northMetres * cos;
            across = eastMetres * cos - northMetres * sin;
        }

        /// <summary>
        /// Compressed display height that still separates turboprops, narrowbodies
        /// and widebodies instead of stacking everyone on a 400 m floor.
        /// </summary>
        public static double DisplayAltitudeMetres(SkyFlight flight)
        {
            var cruise = flight.AltitudeFeet / EnrouteProfile.FeetPerMetre * DisplayAltitudeScale;
            var band = flight.Type?.Id switch
            {
                "A359" or "B78X" => 220.0,
                "B38M" or "A21N" => 120.0,
                _ => 0.0
            };
            var jitter = 0.0;
            if (!string.IsNullOrEmpty(flight.Callsign))
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var ch in flight.Callsign)
                        hash = hash * 31 + ch;
                    jitter = Math.Abs(hash % 7) * 18.0;
                }
            }

            var height = 260.0 + band + jitter + cruise;
            if (flight.To.Code != "ADL")
                return height;

            ToLocalMetres(flight.Latitude, flight.Longitude, out var east, out var north);
            var rangeKm = Math.Sqrt(east * east + north * north) / 1000.0;
            var blend = Math.Max(0.0, Math.Min(1.0, (NearFieldKm * 2.0 - rangeKm) / (NearFieldKm * 2.0)));
            var circuit = 90.0 + band * 0.15;
            return height * (1.0 - blend) + circuit * blend;
        }

        public static void ToLocalMetres(double latitude, double longitude, out double eastMetres, out double northMetres)
        {
            const double metresPerDegreeLat = 110_946.0;
            var adl = DestinationCatalogue.Adelaide;
            northMetres = (latitude - adl.Latitude) * metresPerDegreeLat;
            eastMetres = (longitude - adl.Longitude) * metresPerDegreeLat
                         * Math.Cos(adl.Latitude * Math.PI / 180.0);
        }

        private static long FloorTo(long value, long interval)
        {
            if (interval <= 0)
                return 0;
            var n = value / interval;
            if (value < 0 && value % interval != 0)
                n--;
            return n * interval;
        }

        private static void GreatCircle(double lat1, double lon1, double lat2, double lon2, double t,
            out double lat, out double lon)
        {
            var phi1 = lat1 * Math.PI / 180.0;
            var lam1 = lon1 * Math.PI / 180.0;
            var phi2 = lat2 * Math.PI / 180.0;
            var lam2 = lon2 * Math.PI / 180.0;
            var x1 = Math.Cos(phi1) * Math.Cos(lam1);
            var y1 = Math.Cos(phi1) * Math.Sin(lam1);
            var z1 = Math.Sin(phi1);
            var x2 = Math.Cos(phi2) * Math.Cos(lam2);
            var y2 = Math.Cos(phi2) * Math.Sin(lam2);
            var z2 = Math.Sin(phi2);
            var dot = Math.Max(-1.0, Math.Min(1.0, x1 * x2 + y1 * y2 + z1 * z2));
            var omega = Math.Acos(dot);
            double x, y, z;
            if (omega < 1e-9)
            {
                x = x1 + (x2 - x1) * t;
                y = y1 + (y2 - y1) * t;
                z = z1 + (z2 - z1) * t;
            }
            else
            {
                var s = Math.Sin(omega);
                var a = Math.Sin((1 - t) * omega) / s;
                var b = Math.Sin(t * omega) / s;
                x = a * x1 + b * x2;
                y = a * y1 + b * y2;
                z = a * z1 + b * z2;
            }

            lat = Math.Atan2(z, Math.Sqrt(x * x + y * y)) * 180.0 / Math.PI;
            lon = Math.Atan2(y, x) * 180.0 / Math.PI;
        }
    }

    public readonly struct SkyRoute
    {
        public SkyRoute(string airline, int flightNumber, long intervalSeconds, string fromCode, string toCode,
            AircraftType type)
        {
            Airline = airline;
            FlightNumber = flightNumber;
            IntervalSeconds = intervalSeconds;
            FromCode = fromCode;
            ToCode = toCode;
            Type = type;
        }

        public string Airline { get; }
        public int FlightNumber { get; }
        public long IntervalSeconds { get; }
        public string FromCode { get; }
        public string ToCode { get; }
        public AircraftType Type { get; }
    }
}
