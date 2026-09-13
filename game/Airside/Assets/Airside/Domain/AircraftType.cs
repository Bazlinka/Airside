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

        public bool CanReach(double legKm) => legKm <= PracticalRangeKm;
    }
}
