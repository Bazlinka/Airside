using System;

namespace Airside.Domain
{
    /// <summary>
    /// A real-world point the airport occupies. For now it carries identity and the
    /// facts a later build needs — time zone for the companion app and real flight
    /// schedules, latitude for daylight length and climate. The prototype only uses
    /// the name and the day/night cycle.
    /// </summary>
    public readonly struct AirportLocation : IEquatable<AirportLocation>
    {
        public AirportLocation(string id, string name, string region, int utcOffsetHours, float latitudeDegrees)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A location id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A location name is required.", nameof(name));

            Id = id;
            Name = name;
            Region = region ?? string.Empty;
            UtcOffsetHours = utcOffsetHours;
            LatitudeDegrees = latitudeDegrees;
        }

        public string Id { get; }
        public string Name { get; }
        public string Region { get; }
        public int UtcOffsetHours { get; }
        public float LatitudeDegrees { get; }

        // Small starter set of real regional airfields. The first playable airport
        // ships with one; the picker and the full dataset come later.
        public static readonly AirportLocation Kingscote =
            new("KGC", "Kingscote", "Kangaroo Island, South Australia", 9, -35.71f);

        public static readonly AirportLocation PortLincoln =
            new("PLO", "Port Lincoln", "Eyre Peninsula, South Australia", 9, -34.60f);

        public static readonly AirportLocation CooberPedy =
            new("CPD", "Coober Pedy", "Outback South Australia", 9, -29.04f);

        public static readonly AirportLocation[] Presets = { Kingscote, PortLincoln, CooberPedy };

        public static AirportLocation Default => Kingscote;

        public static AirportLocation FromId(string id)
        {
            foreach (var location in Presets)
            {
                if (string.Equals(location.Id, id, StringComparison.OrdinalIgnoreCase))
                    return location;
            }

            return Default;
        }

        public bool Equals(AirportLocation other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AirportLocation other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => $"{Name} ({Region})";
    }
}
