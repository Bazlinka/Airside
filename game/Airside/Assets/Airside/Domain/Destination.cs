using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>A real airport an airline can fly to, identified by IATA code.</summary>
    public readonly struct Destination : IEquatable<Destination>
    {
        public Destination(string code, string name, string state, double latitude, double longitude)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A destination code is required.", nameof(code));

            Code = code;
            Name = name;
            State = state;
            Latitude = latitude;
            Longitude = longitude;
        }

        public string Code { get; }
        public string Name { get; }
        public string State { get; }
        public double Latitude { get; }
        public double Longitude { get; }

        private const double EarthRadiusKm = 6371.0;

        /// <summary>Great-circle distance in kilometres (haversine).</summary>
        public double DistanceKmTo(Destination other)
        {
            var lat1 = ToRadians(Latitude);
            var lat2 = ToRadians(other.Latitude);
            var dLat = lat2 - lat1;
            var dLon = ToRadians(other.Longitude - Longitude);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        public bool Equals(Destination other) => string.Equals(Code, other.Code, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is Destination other && Equals(other);
        public override int GetHashCode() => Code == null ? 0 : StringComparer.Ordinal.GetHashCode(Code);
        public override string ToString() => $"{Name} ({Code})";
    }

    /// <summary>
    /// Australia-wide destination set (ADR 0045). Coordinates are the aerodrome
    /// reference points to two or three decimals — enough for leg distance.
    /// </summary>
    public static class DestinationCatalogue
    {
        public static readonly Destination Adelaide = new("ADL", "Adelaide", "SA", -34.945, 138.531);

        public static readonly IReadOnlyList<Destination> Australia = new[]
        {
            Adelaide,
            new Destination("KGC", "Kingscote", "SA", -35.714, 137.521),
            new Destination("PLO", "Port Lincoln", "SA", -34.605, 135.880),
            new Destination("WYA", "Whyalla", "SA", -33.059, 137.514),
            new Destination("MGB", "Mount Gambier", "SA", -37.746, 140.785),
            new Destination("CED", "Ceduna", "SA", -32.131, 133.710),
            new Destination("CPD", "Coober Pedy", "SA", -29.040, 134.721),
            new Destination("MQL", "Mildura", "VIC", -34.229, 142.086),
            new Destination("BHQ", "Broken Hill", "NSW", -32.001, 141.472),
            new Destination("MEL", "Melbourne", "VIC", -37.673, 144.843),
            new Destination("CBR", "Canberra", "ACT", -35.307, 149.195),
            new Destination("SYD", "Sydney", "NSW", -33.946, 151.177),
            new Destination("HBA", "Hobart", "TAS", -42.836, 147.510),
            new Destination("ASP", "Alice Springs", "NT", -23.807, 133.902),
            new Destination("BNE", "Brisbane", "QLD", -27.384, 153.117),
            new Destination("OOL", "Gold Coast", "QLD", -28.164, 153.505),
            new Destination("CNS", "Cairns", "QLD", -16.886, 145.755),
            new Destination("DRW", "Darwin", "NT", -12.415, 130.877),
            new Destination("PER", "Perth", "WA", -31.940, 115.967),
        };

        /// <summary>International airports used by current Adelaide traffic.</summary>
        public static readonly IReadOnlyList<Destination> International = new[]
        {
            new Destination("AKL", "Auckland", "New Zealand", -37.008, 174.792),
            new Destination("CHC", "Christchurch", "New Zealand", -43.489, 172.532),
            new Destination("DPS", "Denpasar (Bali)", "Indonesia", -8.748, 115.167),
            new Destination("SIN", "Singapore", "Singapore", 1.364, 103.991),
            new Destination("HKG", "Hong Kong", "Hong Kong", 22.308, 113.918),
        };

        public static IEnumerable<Destination> All
        {
            get
            {
                foreach (var destination in Australia)
                    yield return destination;
                foreach (var destination in International)
                    yield return destination;
            }
        }

        public static bool TryFind(string code, out Destination destination)
        {
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = default;
            return false;
        }
    }

    /// <summary>
    /// Real-length leg timing. Airborne time is great-circle distance at cruise; the
    /// fixed allowance covers climb, descent and the approach that cruise ignores.
    /// Ground time at either end is simulated separately, so it is not in here.
    /// </summary>
    public static class LegTiming
    {
        public const long ClimbDescentAllowanceSeconds = 10 * 60;

        public static long AirborneSeconds(double distanceKm, AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var cruise = distanceKm / type.CruiseKmh * 3600.0;
            return ClimbDescentAllowanceSeconds + (long)Math.Round(cruise);
        }
    }
}
