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

        // The named types live in AircraftCatalogue (ADR 0048), the one place their facts are kept.
        // Properties, not fields, so the two classes never initialise each other in a cycle.
        public static AircraftType Atr42 => AircraftCatalogue.Atr42.Type;
        public static AircraftType Saab340 => AircraftCatalogue.Saab340.Type;
        public static AircraftType Dash8Q400 => AircraftCatalogue.Dash8Q400.Type;
        public static AircraftType Boeing7378 => AircraftCatalogue.Boeing7378.Type;
        public static AircraftType AirbusA321Neo => AircraftCatalogue.AirbusA321Neo.Type;
        public static AircraftType AirbusA350900 => AircraftCatalogue.AirbusA350900.Type;

        public bool CanReach(double legKm) => legKm <= PracticalRangeKm;

        public static bool TryFromId(string id, out AircraftType type)
        {
            type = null;
            foreach (var spec in AircraftCatalogue.All)
                if (string.Equals(id, spec.Id, StringComparison.Ordinal))
                    type = spec.Type;
            return type != null;
        }
    }
}
