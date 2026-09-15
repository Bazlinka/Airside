using System;
using System.Globalization;

namespace Airside.Domain
{
    /// <summary>
    /// An airline operating at the airport. Names are fictional; routes are real
    /// (ADR 0045). The player runs exactly one; the rest are AI-operated.
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

        /// <summary>The fictional AI carrier whose livery decal already ships.</summary>
        // Brown and gold, matching dc_livery_emu_air_v01.
        public static Airline EmuAir() => new("EMU", "Emu Air", "#A66F32", isPlayer: false);

        /// <summary>Regional Express — Adelaide's main regional operator (Saab 340s).</summary>
        public static Airline Rex() => new("REX", "Rex", "#D2491E", isPlayer: false);

        /// <summary>QantasLink regional services from Adelaide (Dash 8-400s).</summary>
        public static Airline QantasLink() => new("QLK", "QantasLink", "#D8141E", isPlayer: false);

        /// <summary>
        /// Wattlebird Jet — the project's own fictional mainland jet operator (ADR 0047), flying
        /// one unbranded 737-8 from Adelaide's terminal. No real airline, logo or livery.
        /// </summary>
        public static Airline WattlebirdJet() => new("WTB", "Wattlebird Jet", "#2F7F86", isPlayer: false);

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
