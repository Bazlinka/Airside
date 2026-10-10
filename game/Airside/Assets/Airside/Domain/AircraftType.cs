using System;

namespace Airside.Domain
{
    /// <summary>
    /// Performance facts the airline layer needs: how far a type can go and how fast
    /// it gets there. Circuit speeds near the runway live in CircuitProfile.
    /// </summary>
    public sealed class AircraftType
    {
        public AircraftType(string id, string name, double cruiseKmh, double practicalRangeKm,
            bool isRotorcraft = false)
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
            IsRotorcraft = isRotorcraft;
        }

        public string Id { get; }
        public string Name { get; }
        public double CruiseKmh { get; }

        /// <summary>
        /// A helicopter: it lifts off and lands vertically from a helipad stand, never uses a runway,
        /// the taxi network or the tower's strip queue, and flies short legs at helicopter speeds.
        /// </summary>
        public bool IsRotorcraft { get; }

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
        public static AircraftType EmbraerE190 => AircraftCatalogue.EmbraerE190.Type;
        public static AircraftType AirbusA220300 => AircraftCatalogue.AirbusA220300.Type;
        public static AircraftType AirbusA320200 => AircraftCatalogue.AirbusA320200.Type;
        public static AircraftType Boeing737800 => AircraftCatalogue.Boeing737800.Type;
        public static AircraftType Boeing7378 => AircraftCatalogue.Boeing7378.Type;
        public static AircraftType AirbusA321Neo => AircraftCatalogue.AirbusA321Neo.Type;
        public static AircraftType AirbusA350900 => AircraftCatalogue.AirbusA350900.Type;
        public static AircraftType Boeing78710 => AircraftCatalogue.Boeing78710.Type;
        public static AircraftType AirbusA330900 => AircraftCatalogue.AirbusA330900.Type;
        public static AircraftType Boeing7879 => AircraftCatalogue.Boeing7879.Type;
        public static AircraftType Boeing7478 => AircraftCatalogue.Boeing7478.Type;
        public static AircraftType AirbusA380800 => AircraftCatalogue.AirbusA380800.Type;
        public static AircraftType Bell412 => AircraftCatalogue.Bell412.Type;

        public bool CanReach(double legKm) => legKm <= PracticalRangeKm;

        public static bool TryFromId(string id, out AircraftType type)
        {
            type = null;
            foreach (var spec in AircraftCatalogue.All)
            {
                if (!string.Equals(id, spec.Id, StringComparison.Ordinal))
                    continue;
                type = spec.Type;
                return true;
            }
            foreach (var spec in AircraftCatalogue.Rotorcraft)
            {
                if (!string.Equals(id, spec.Id, StringComparison.Ordinal))
                    continue;
                type = spec.Type;
                return true;
            }
            return false;
        }
    }
}
