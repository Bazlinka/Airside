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
        LongHaul = 4
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
                    return RouteBand.National;
                case "AKL":
                case "CHC":
                    return RouteBand.Tasman;
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
            if (type.Id == AircraftType.Dash8Q400.Id)
                return RouteBand.Domestic;
            if (type.Id == AircraftType.Boeing7378.Id)
                return RouteBand.National;
            if (type.Id == AircraftType.AirbusA321Neo.Id)
                return RouteBand.Tasman;
            return RouteBand.LongHaul;
        }

        public static bool Allows(AircraftType type, Destination destination) =>
            type != null && BandOf(destination) <= Ceiling(type);

        /// <summary>Revenue multiplier on top of the type weight — bigger bands pay more.</summary>
        public static double PayMultiplier(RouteBand band) => band switch
        {
            RouteBand.Domestic => 1.25,
            RouteBand.National => 1.45,
            RouteBand.Tasman => 1.60,
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
        public static string ExampleDestinations(RouteBand band) => band switch
        {
            RouteBand.Domestic => "Melbourne, Sydney",
            RouteBand.National => "Perth, Brisbane",
            RouteBand.Tasman => "Auckland, Christchurch",
            RouteBand.LongHaul => "further afield",
            _ => "Kingscote, Port Lincoln"
        };
    }
}
