using System;

namespace Airside.Domain
{
    /// <summary>Capability band of a route from Adelaide (ADR 0056). Player types unlock bands.</summary>
    public enum RouteBand
    {
        Regional = 0,
        Domestic = 1,
        National = 2,
        Tasman = 3,
        /// <summary>ADR 0140: the near Pacific and Bali, between the Tasman and long-haul.</summary>
        Pacific = 4,
        LongHaul = 5
    }

    /// <summary>
    /// Which destinations a type may operate for the player, and how those legs pay.
    /// AI traffic still uses range alone.
    /// </summary>
    public static class RouteAccess
    {
        public static RouteBand BandOf(string destinationCode)
        {
            if (string.IsNullOrEmpty(destinationCode))
                return RouteBand.Regional;
            switch (destinationCode.ToUpperInvariant())
            {
                case "KGC":
                case "PLO":
                case "WYA":
                case "MGB":
                case "CED":
                case "CPD":
                case "MQL":
                case "BHQ":
                    return RouteBand.Regional;
                case "MEL":
                case "CBR":
                case "SYD":
                case "HBA":
                    return RouteBand.Domestic;
                case "BNE":
                case "OOL":
                case "ASP":
                case "PER":
                case "CNS":
                case "DRW":
                    return RouteBand.National;
                case "AKL":
                case "CHC":
                    return RouteBand.Tasman;
                case "NAN":
                case "NOU":
                case "POM":
                case "DPS":
                    return RouteBand.Pacific;
                default:
                    return RouteBand.LongHaul;
            }
        }

        public static RouteBand BandOf(Destination destination) => BandOf(destination.Code);

        /// <summary>Highest band this type is allowed to file for the player.</summary>
        public static RouteBand Ceiling(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type.Id == AircraftType.Atr42.Id || type.Id == AircraftType.Saab340.Id)
                return RouteBand.Regional;
            if (type.Id == AircraftType.Dash8Q400.Id || type.Id == AircraftType.EmbraerE190.Id)
                return RouteBand.Domestic;
            if (type.Id == AircraftType.Boeing7378.Id || type.Id == AircraftType.Boeing737800.Id
                || type.Id == AircraftType.AirbusA320200.Id)
                return RouteBand.National;
            // ADR 0140: the Tasman narrowbodies also fly the near Pacific when it is in range.
            if (type.Id == AircraftType.AirbusA321Neo.Id || type.Id == AircraftType.AirbusA220300.Id)
                return RouteBand.Pacific;
            return RouteBand.LongHaul;
        }

        public static bool Allows(AircraftType type, Destination destination) =>
            type != null && BandOf(destination) <= Ceiling(type);

        /// <summary>
        /// Australian cities are Regional/Domestic/National; only Tasman and beyond are
        /// international. One definition for the career, the market and network flying.
        /// </summary>
        public static bool IsInternational(string destinationCode) => BandOf(destinationCode) >= RouteBand.Tasman;

        /// <summary>The operating tier the player needs before filing a route in this band.</summary>
        public static OperatingTier RequiredTier(RouteBand band) =>
            band >= RouteBand.Tasman ? OperatingTier.International : OperatingTier.Provisional;

        /// <summary>Revenue multiplier on top of the type weight — bigger bands pay more.</summary>
        public static double PayMultiplier(RouteBand band) => band switch
        {
            RouteBand.Domestic => 1.25,
            RouteBand.National => 1.45,
            RouteBand.Tasman => 1.60,
            RouteBand.Pacific => 1.70,
            RouteBand.LongHaul => 1.80,
            _ => 1.0
        };

        /// <summary>
        /// A couple of named destinations in this band, for HUD copy that used to say only
        /// "flies Regional routes" or "Domestic capability" — a real answer to "where can it
        /// fly", not just the abstract band name. Not exhaustive (<see cref="BandOf"/> is the
        /// source of truth); picked to be recognisable, real places from that switch. Kept to
        /// two names — a HUD line drawing this used to clip against its own box at the widest
        /// band (four names plus a suffix ran past a ~735px detail pane).
        /// </summary>
        /// <summary>The band in a sentence: "regional", "long-haul", "Tasman".</summary>
        public static string Label(RouteBand band) => band switch
        {
            RouteBand.Domestic => "domestic",
            RouteBand.National => "national",
            RouteBand.Tasman => "Tasman",
            RouteBand.Pacific => "Pacific",
            RouteBand.LongHaul => "long-haul",
            _ => "regional"
        };

        public static string ExampleDestinations(RouteBand band) => band switch
        {
            RouteBand.Domestic => "Melbourne, Sydney",
            RouteBand.National => "Perth, Brisbane",
            RouteBand.Tasman => "Auckland, Christchurch",
            RouteBand.Pacific => "Bali, Nadi",
            RouteBand.LongHaul => "further afield",
            _ => "Kingscote, Port Lincoln"
        };
    }
}
