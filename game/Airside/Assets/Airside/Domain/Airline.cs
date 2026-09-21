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
        public string Name { get; private set; }

        /// <summary>
        /// Paint-friendly fuselage wordmark. Real titles are short ("REX",
        /// "VIRGIN") — the legal name is too long for the side of an ATR.
        /// </summary>
        public string FuselageTitle
        {
            get
            {
                switch (Id.Value)
                {
                    case "REX": return "REX";
                    case "QLK": return "QANTASLINK";
                    case "VOZ": return "VIRGIN";
                    case "QFA": return "QANTAS";
                    case "JST": return "JETSTAR";
                    case "ANZ": return "AIR NZ";
                    case "SIA": return "SINGAPORE";
                    case "CPA": return "CATHAY";
                    case "MAS": return "MALAYSIA";
                    case "UAE": return "EMIRATES";
                    case "QTR": return "QATAR";
                    case "FJI": return "FIJI";
                    case "RFDS": return "RFDS";
                    default: return Wordmark(Name);
                }
            }
        }

        /// <summary>Primary livery colour as #RRGGBB, kept UnityEngine-free.</summary>
        public string LiveryHex { get; private set; }

        public bool IsPlayer { get; }

        /// <summary>Emergency medical flights — exempt from the Adelaide curfew.</summary>
        public bool IsEmergency => Id.Value == "RFDS";

        /// <summary>
        /// Renames the airline in place — every existing reference (fleet aircraft, save
        /// records) already holds this same instance, so nothing needs re-wiring. Player-only
        /// by convention (<see cref="AirlineOperations.RenameAirline"/> enforces it); AI
        /// operators use real airline names that should not change mid-game.
        /// </summary>
        /// <exception cref="ArgumentException">The name is empty or longer than 24 characters —
        /// the same fuselage-title/HUD-width budget as the authored AI names.</exception>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An airline name is required.", nameof(name));
            var trimmed = name.Trim();
            if (trimmed.Length > 24)
                throw new ArgumentException("An airline name must be 24 characters or fewer.", nameof(name));
            Name = trimmed;
        }

        /// <summary>Repaints the livery in place — see <see cref="Rename"/> for why in place.</summary>
        public void Repaint(string liveryHex)
        {
            if (!IsValidHex(liveryHex))
                throw new ArgumentException("Livery colour must be #RRGGBB.", nameof(liveryHex));
            LiveryHex = liveryHex.ToUpperInvariant();
        }

        /// <summary>Regional Express — Adelaide's main regional operator (Saab 340s).</summary>
        public static Airline Rex() => new("REX", "Rex", "#D2491E", isPlayer: false);

        /// <summary>QantasLink regional services from Adelaide (Dash 8-400s).</summary>
        public static Airline QantasLink() => new("QLK", "QantasLink", "#D8141E", isPlayer: false);

        /// <summary>Virgin Australia domestic services from Adelaide (737-8).</summary>
        public static Airline VirginAustralia() => new("VOZ", "Virgin Australia", "#D71964", isPlayer: false);

        /// <summary>Qantas mainline domestic services from Adelaide (737-8).</summary>
        public static Airline Qantas() => new("QFA", "Qantas", "#E4002B", isPlayer: false);

        /// <summary>Jetstar domestic services from Adelaide (A321neo).</summary>
        public static Airline Jetstar() => new("JST", "Jetstar", "#F26623", isPlayer: false);

        /// <summary>Air New Zealand trans-Tasman services from Adelaide (A321neo).</summary>
        public static Airline AirNewZealand() => new("ANZ", "Air New Zealand", "#111111", isPlayer: false);

        public static Airline SingaporeAirlines() => new("SIA", "Singapore Airlines", "#1B3F8B", isPlayer: false);

        public static Airline CathayPacific() => new("CPA", "Cathay Pacific", "#006564", isPlayer: false);

        /// <summary>Malaysia Airlines daily Kuala Lumpur service (A350 stand-in for the A330neo).</summary>
        public static Airline MalaysiaAirlines() => new("MAS", "Malaysia Airlines", "#ED1B2F", isPlayer: false);

        /// <summary>Emirates Dubai service from Adelaide.</summary>
        public static Airline Emirates() => new("UAE", "Emirates", "#D71921", isPlayer: false);

        /// <summary>Qatar Airways Doha service — back on the 2026 Adelaide field.</summary>
        public static Airline QatarAirways() => new("QTR", "Qatar Airways", "#5C0632", isPlayer: false);

        /// <summary>Fiji Airways Nadi service.</summary>
        public static Airline FijiAirways() => new("FJI", "Fiji Airways", "#1B4F9C", isPlayer: false);

        /// <summary>
        /// Royal Flying Doctor Service from Adelaide. Real metal is a PC-12 / King Air;
        /// the sim flies a Saab 340 until that type exists. Exempt from the 23:00–06:00 curfew.
        /// </summary>
        public static Airline Rfds() => new("RFDS", "Royal Flying Doctor Service", "#C8102E", isPlayer: false);

        public static Airline Player(string name, string liveryHex) => new("PLAYER", name, liveryHex, isPlayer: true);

        internal static string Wordmark(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var trimmed = name.Trim();
            if (trimmed.Length <= 16)
                return trimmed.ToUpperInvariant();
            var parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var two = parts[0] + " " + parts[1];
                if (two.Length <= 18)
                    return two.ToUpperInvariant();
            }

            return parts[0].ToUpperInvariant();
        }

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
