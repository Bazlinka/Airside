using System;
using System.Globalization;

namespace Airside.Domain
{
    /// <summary>
    /// An airline operating at the airport. The player chooses their own name; AI
    /// operators use real airline names that serve Adelaide.
    /// </summary>
    public sealed class Airline
    {
        public Airline(string id, string name, string liveryHex, bool isPlayer)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An airline id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An airline name is required.", nameof(name));
            if (!IsValidHex(liveryHex))
                throw new ArgumentException("Livery colour must be #RRGGBB.", nameof(liveryHex));

            Id = new StableId(id);
            Name = name.Trim();
            LiveryHex = liveryHex.ToUpperInvariant();
            IsPlayer = isPlayer;
        }

        public StableId Id { get; }
        public string Name { get; }

        /// <summary>Primary livery colour as #RRGGBB, kept UnityEngine-free.</summary>
        public string LiveryHex { get; }

        public bool IsPlayer { get; }

        /// <summary>Regional Express — Adelaide's main regional operator (Saab 340s).</summary>
        public static Airline Rex() => new("REX", "Rex", "#D2491E", isPlayer: false);

        /// <summary>QantasLink regional services from Adelaide (Dash 8-400s).</summary>
        public static Airline QantasLink() => new("QLK", "QantasLink", "#D8141E", isPlayer: false);

        /// <summary>Virgin Australia domestic services from Adelaide (737-8).</summary>
        public static Airline VirginAustralia() => new("VOZ", "Virgin Australia", "#D71964", isPlayer: false);

        /// <summary>Air New Zealand trans-Tasman services from Adelaide (A321neo).</summary>
        public static Airline AirNewZealand() => new("ANZ", "Air New Zealand", "#111111", isPlayer: false);

        public static Airline Player(string name, string liveryHex) => new("PLAYER", name, liveryHex, isPlayer: true);

        public (byte r, byte g, byte b) LiveryRgb()
        {
            var value = int.Parse(LiveryHex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        private static bool IsValidHex(string hex) =>
            hex != null
            && hex.Length == 7
            && hex[0] == '#'
            && int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

        public override string ToString() => Name;
    }
}
