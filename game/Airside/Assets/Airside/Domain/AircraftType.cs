using System;

namespace Airside.Domain
{
    /// <summary>
    /// Performance facts the airline layer needs: how far a type can go and how fast
    /// it gets there. Circuit speeds near the runway live in CircuitProfile.
    /// </summary>
    public sealed class AircraftType
    {
        public AircraftType(string id, string name, double cruiseKmh, double practicalRangeKm)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An aircraft type id is required.", nameof(id));
            if (cruiseKmh <= 0)
                throw new ArgumentOutOfRangeException(nameof(cruiseKmh));
            if (practicalRangeKm <= 0)
                throw new ArgumentOutOfRangeException(nameof(practicalRangeKm));

            Id = id;
            Name = name;
            CruiseKmh = cruiseKmh;
            PracticalRangeKm = practicalRangeKm;
        }

        public string Id { get; }
        public string Name { get; }
        public double CruiseKmh { get; }

        /// <summary>
        /// Airline planning range with a typical payload and reserves, not the brochure
        /// figure. Legs longer than this are locked on the destinations map.
        /// </summary>
        public double PracticalRangeKm { get; }

        /// <summary>
        /// ATR 42-600: ~300 kt (556 km/h) cruise. Brochure range is ~1,326 km; planned
        /// with passengers and reserves it is nearer 1,100 km, which keeps Sydney,
        /// Hobart and Alice Springs out of reach from Adelaide — as in real service.
        /// </summary>
        public static readonly AircraftType Atr42 = new("ATR42", "ATR 42-600", 556, 1100);

        /// <summary>
        /// Saab 340B, Rex's regional workhorse: ~270 kt (500 km/h) cruise; planned with a
        /// full cabin and reserves, about 1,000 km. Drawn with the ATR model for now.
        /// </summary>
        public static readonly AircraftType Saab340 = new("SF34", "Saab 340B", 500, 1000);

        /// <summary>
        /// De Havilland Canada Dash 8-400 (QantasLink): ~360 kt (667 km/h) cruise, about
        /// 1,800 km planned range. Drawn with the ATR model for now.
        /// </summary>
        public static readonly AircraftType Dash8Q400 = new("DH8D", "Dash 8-400", 667, 1800);

        /// <summary>
        /// Boeing 737-8 (MAX 8) class: roughly 453 kt / 839 km/h cruise. The
        /// 5,200 km planning range keeps a reserve/payload margin below the brochure
        /// maximum. AIR-005 is a fictional, unbranded visual asset; it becomes a
        /// moving fleet type only when terminal-gate operations are implemented.
        /// </summary>
        public static readonly AircraftType Boeing7378 = new("B38M", "Boeing 737-8", 839, 5200);

        private static readonly AircraftType[] Known = { Atr42, Saab340, Dash8Q400, Boeing7378 };

        public bool CanReach(double legKm) => legKm <= PracticalRangeKm;

        public static bool TryFromId(string id, out AircraftType type)
        {
            type = null;
            foreach (var known in Known)
                if (string.Equals(id, known.Id, StringComparison.Ordinal))
                    type = known;
            return type != null;
        }
    }
}
