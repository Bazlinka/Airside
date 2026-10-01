using System;
using System.Collections.Generic;

namespace Airside.Domain
{
    /// <summary>A real airport an airline can fly to, identified by IATA code.</summary>
    public readonly struct Destination : IEquatable<Destination>
    {
        public Destination(string code, string name, string state, double latitude, double longitude,
            string country = "AU")
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A destination code is required.", nameof(code));

            Code = code;
            Name = name;
            State = state ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            Country = string.IsNullOrEmpty(country) ? "AU" : country;
        }

        public string Code { get; }
        public string Name { get; }

        /// <summary>Australian state or territory ("SA", "VIC"); empty for an overseas airport (ADR 0140).</summary>
        public string State { get; }

        /// <summary>ISO 3166 country code: "AU", "NZ", "JP" (ADR 0140).</summary>
        public string Country { get; }

        public bool IsAustralian => Country == "AU";

        /// <summary>The country's name: "Australia", "New Zealand", "Japan".</summary>
        public string CountryName => Countries.Name(Country);

        /// <summary>Where it is, for a label: the state at home, the country overseas.</summary>
        public string Region => IsAustralian ? State : CountryName;
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

    /// <summary>Country names for the ISO codes the catalogue uses (ADR 0140).</summary>
    public static class Countries
    {
        public static string Name(string iso) => iso switch
        {
            "AU" => "Australia",
            "NZ" => "New Zealand",
            "FJ" => "Fiji",
            "NC" => "New Caledonia",
            "PG" => "Papua New Guinea",
            "ID" => "Indonesia",
            "SG" => "Singapore",
            "MY" => "Malaysia",
            "TH" => "Thailand",
            "VN" => "Vietnam",
            "PH" => "Philippines",
            "HK" => "Hong Kong",
            "CN" => "China",
            "KR" => "South Korea",
            "JP" => "Japan",
            "US" => "United States",
            "QA" => "Qatar",
            "AE" => "United Arab Emirates",
            _ => iso ?? string.Empty
        };
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

        /// <summary>
        /// Overseas airports: those Adelaide traffic flies today, and (ADR 0140) the wider
        /// Asia-Pacific and the US west coast. Coordinates match OurAirports to under 2 km.
        /// </summary>
        public static readonly IReadOnlyList<Destination> International = new[]
        {
            new Destination("AKL", "Auckland", "", -37.008, 174.792, "NZ"),
            new Destination("CHC", "Christchurch", "", -43.489, 172.532, "NZ"),
            new Destination("NAN", "Nadi", "", -17.755, 177.443, "FJ"),
            new Destination("NOU", "Nouméa", "", -22.015, 166.213, "NC"),
            new Destination("POM", "Port Moresby", "", -9.443, 147.220, "PG"),
            new Destination("DPS", "Denpasar (Bali)", "", -8.748, 115.167, "ID"),
            new Destination("CGK", "Jakarta", "", -6.126, 106.656, "ID"),
            new Destination("SIN", "Singapore", "", 1.364, 103.991, "SG"),
            new Destination("KUL", "Kuala Lumpur", "", 2.745, 101.710, "MY"),
            new Destination("BKK", "Bangkok", "", 13.681, 100.747, "TH"),
            new Destination("SGN", "Ho Chi Minh City", "", 10.819, 106.652, "VN"),
            new Destination("MNL", "Manila", "", 14.509, 121.020, "PH"),
            new Destination("HKG", "Hong Kong", "", 22.308, 113.918, "HK"),
            new Destination("PVG", "Shanghai", "", 31.143, 121.805, "CN"),
            new Destination("ICN", "Seoul", "", 37.469, 126.451, "KR"),
            new Destination("KIX", "Osaka", "", 34.427, 135.244, "JP"),
            new Destination("NRT", "Tokyo", "", 35.769, 140.389, "JP"),
            new Destination("HNL", "Honolulu", "", 21.318, -157.926, "US"),
            new Destination("LAX", "Los Angeles", "", 33.943, -118.408, "US"),
            new Destination("DOH", "Doha", "", 25.273, 51.608, "QA"),
            new Destination("DXB", "Dubai", "", 25.253, 55.364, "AE"),
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

        /// <summary>
        /// Hospital helipads and scene sites the rescue helicopter flies to (ADR 0207). Looked up by
        /// <see cref="TryFind"/> so a save mid-mission resolves them, but kept out of <see cref="All"/>
        /// because they are not airports an airline can plan a route to. Reference points to three decimals.
        /// </summary>
        public static readonly IReadOnlyList<Destination> RescueSites = new[]
        {
            new Destination("RAH", "Royal Adelaide Hospital", "SA", -34.921, 138.587),
            new Destination("FMC", "Flinders Medical Centre", "SA", -35.023, 138.570),
            new Destination("LMH", "Lyell McEwin Hospital", "SA", -34.750, 138.674),
            new Destination("MTB", "Mount Barker Hospital", "SA", -35.067, 138.860),
            new Destination("GAW", "Gawler Hospital", "SA", -34.599, 138.745),
            new Destination("VHB", "Victor Harbor Hospital", "SA", -35.551, 138.615),
        };

        public static bool IsRescueSite(Destination destination)
        {
            foreach (var site in RescueSites)
                if (site.Equals(destination))
                    return true;
            return false;
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

            foreach (var candidate in RescueSites)
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

        /// <summary>
        /// A helicopter's climb-out and arrival are drawn and timed by its own take-off and landing states, so
        /// the en-route leg carries only a short allowance for the turn onto and off its flight line.
        /// </summary>
        public const long RotorcraftLegAllowanceSeconds = 90;

        public static long AirborneSeconds(double distanceKm, AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var cruise = distanceKm / type.CruiseKmh * 3600.0;
            return (type.IsRotorcraft ? RotorcraftLegAllowanceSeconds : ClimbDescentAllowanceSeconds)
                   + (long)Math.Round(cruise);
        }
    }
}
